using PF.Core.Attributes;
using PF.Core.Entities.Hardware.Vision;
using PF.Core.Enums.Hardware.Vision;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Camera.LineScan;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;

namespace PF.Infrastructure.Mechanisms.Vision
{
    /// <summary>
    /// 线扫检测模组 —— 把「轴运动」与「相机取流」编排到一起，扫出一张完整图像。
    ///
    /// <para><b>为什么必须有这一层</b>：线阵相机每次只曝光一行，图像的第二个维度完全由运动提供。
    /// 相机自己不知道轴走到哪了，轴也不知道相机在不在取流——两者的时序只能由机构层负责。
    /// 设备层（<see cref="ILineScanCamera"/>）刻意不引用 <see cref="IAxis"/>，
    /// 就是为了把这份时序职责收敛在这里。</para>
    ///
    /// <para><b>编码器接线</b>：本模组假定编码器（外置磁栅读数头）**直连相机 IO**，
    /// 而不是从轴卡取编码器信号。因此模组只负责「让轴以恒定速度走过扫描区」，
    /// 行触发由相机自己按编码器脉冲产生，模组不参与逐行同步。</para>
    ///
    /// <para><b>时序要点</b>：开流必须早于轴运动，否则起始若干行会丢；
    /// 而扫描区必须完整落在匀速段内，加减速段留在余量里（见
    /// <see cref="ScanProfile.EffectiveApproachMarginMm"/>）。</para>
    /// </summary>
    [MechanismUI("线扫检测模组", "LineScanDetectionModuleDebugView", 20)]
    public class LineScanDetectionModule : BaseMechanism
    {
        /// <summary>等待轴到达指定位置时的轮询间隔（ms）。</summary>
        private const int ReachPollIntervalMs = 5;

        private readonly string _scanAxisDeviceId;
        private readonly string _cameraDeviceId;

        private IAxis? _scanAxis;
        private ILineScanCamera? _camera;

        /// <summary>
        /// 构造线扫检测模组。
        /// </summary>
        /// <param name="name">模组名称。</param>
        /// <param name="scanAxisDeviceId">扫描轴的设备 ID（带动相机或工件走过扫描区的那根轴）。</param>
        /// <param name="cameraDeviceId">线阵相机的设备 ID。</param>
        /// <param name="hardwareManagerService">硬件管理服务。</param>
        /// <param name="paramService">参数服务。</param>
        /// <param name="logger">日志服务。</param>
        public LineScanDetectionModule(string name, string scanAxisDeviceId, string cameraDeviceId,
            IHardwareManagerService hardwareManagerService, IParamService paramService, ILogService logger)
            : base(name, hardwareManagerService, paramService, logger)
        {
            _scanAxisDeviceId = scanAxisDeviceId;
            _cameraDeviceId = cameraDeviceId;
        }

        /// <summary>扫描轴实例（初始化后可用）。</summary>
        public IAxis? ScanAxis => _scanAxis;

        /// <summary>线阵相机实例（初始化后可用）。</summary>
        public ILineScanCamera? Camera => _camera;

        #region 生命周期

        /// <summary>
        /// 延迟解析扫描轴与相机并纳入模组的报警聚合与批量复位。
        /// 设备在构造函数里是取不到的（硬件尚未初始化），必须在这里解析。
        /// </summary>
        protected override Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            _scanAxis = HardwareManagerService?.GetDevice(_scanAxisDeviceId) as IAxis;
            if (_scanAxis == null)
            {
                _logger.Error($"[{MechanismName}] 未找到扫描轴 '{_scanAxisDeviceId}'，请确认硬件配置。");
                return Task.FromResult(false);
            }

            _camera = HardwareManagerService?.GetDevice(_cameraDeviceId) as ILineScanCamera;
            if (_camera == null)
            {
                _logger.Error($"[{MechanismName}] 未找到线阵相机 '{_cameraDeviceId}'，请确认硬件配置。");
                return Task.FromResult(false);
            }

            RegisterHardwareDevice(_scanAxis as IHardwareDevice);
            RegisterHardwareDevice(_camera);

            _logger.Info($"[{MechanismName}] 初始化完成：扫描轴 '{_scanAxisDeviceId}'，相机 '{_cameraDeviceId}'"
                + $"（{(_camera.HasFrameGrabber ? "经采集卡" : "直连")}）。");

            return Task.FromResult(true);
        }

        /// <summary>停止：先停流再停轴。相机还在取流时停轴会留下一帧残图，顺序不能反。</summary>
        protected override async Task InternalStopAsync()
        {
            if (_camera != null)
            {
                try { await _camera.StopAsync(); }
                catch (Exception ex) { _logger.Warn($"[{MechanismName}] 停止取流异常：{ex.Message}"); }
            }

            if (_scanAxis != null)
            {
                try { await _scanAxis.StopAsync(); }
                catch (Exception ex) { _logger.Warn($"[{MechanismName}] 停止扫描轴异常：{ex.Message}"); }
            }
        }

        #endregion

        #region 扫描

        /// <summary>
        /// 执行一次完整扫描并返回图像。
        ///
        /// <para>九步时序：校验配方 → 下发相机配置 → 轴回到起点（含加速余量）→ 开流 →
        /// 轴启动扫描运动（不等待）→ 到达扫描起点时发帧触发 → 等一帧 → 等轴走完 → 停流。</para>
        /// </summary>
        /// <param name="profile">扫描配方。会先经 <see cref="ScanProfile.Validate"/> 校验。</param>
        /// <param name="baseConfig">
        /// 相机基础配置（像素格式、增益、编码器接线等）。为 null 时新建一份默认配置。
        /// 其中的帧长、帧超时、曝光、帧触发开关会被 <paramref name="profile"/> 覆盖——
        /// 这几项由扫描几何决定，不该由调用方另填一遍。
        /// </param>
        /// <param name="token">取消令牌。</param>
        /// <returns>扫描得到的一帧完整图像。</returns>
        /// <exception cref="InvalidOperationException">配方校验不通过，或轴/相机动作失败。</exception>
        /// <exception cref="TimeoutException">在帧超时内没有收到完整帧。</exception>
        public async Task<LineScanFrame> ScanAsync(ScanProfile profile,
            LineScanCameraConfig? baseConfig = null, CancellationToken token = default)
        {
            CheckReady();

            ArgumentNullException.ThrowIfNull(profile);
            if (_scanAxis == null || _camera == null)
                throw new InvalidOperationException($"模组 [{MechanismName}] 尚未解析到扫描轴或相机。");

            // ① 配方自洽性校验：配错的症状都是"图不对"，但原因分别在轴、相机、光学，
            //    与其扫完一张废图再回头猜，不如在这里一次把问题全列出来
            var problems = profile.Validate();
            if (problems.Count > 0)
            {
                string detail = string.Join("\n  · ", problems);
                throw new InvalidOperationException($"[{MechanismName}] 扫描配方校验未通过：\n  · {detail}");
            }

            _logger.Info($"[{MechanismName}] 开始扫描：{profile}");

            bool grabbing = false;
            try
            {
                // ② 下发相机配置
                var config = BuildConfig(profile, baseConfig);
                await _camera.ApplyConfigAsync(config, token);

                // ③ 轴回到起点（比扫描起点多退一个加速余量，让扫描区完整落在匀速段）
                if (!await MoveAbsAndWaitAsync(_scanAxis, profile.MoveStartMm,
                        profile.EffectivePositioningVelocity, profile.AccelerationMmPerSec2,
                        profile.EffectiveDeceleration, profile.SCurveTimeMs, profile.AxisTimeoutMs, token))
                {
                    throw new InvalidOperationException($"[{MechanismName}] 轴未能回到扫描起始位 {profile.MoveStartMm:F2}mm。");
                }

                // ④ 开流——必须早于轴运动，晚了会丢掉起始若干行
                if (!await _camera.ArmAsync(token))
                    throw new InvalidOperationException($"[{MechanismName}] 相机开流失败，扫描中止。");

                grabbing = true;

                // ⑤ 启动扫描运动，**不等待到位**：后面还要在运动过程中触发和收帧
                if (!await _scanAxis.MoveAbsoluteAsync(profile.MoveEndMm, profile.ScanVelocityMmPerSec,
                        profile.AccelerationMmPerSec2, profile.EffectiveDeceleration, profile.SCurveTimeMs, token))
                {
                    throw new InvalidOperationException($"[{MechanismName}] 扫描运动指令下发失败。");
                }

                // ⑥ 帧触发模式：等轴真正进入扫描区再起帧，
                //    否则加速段的编码器脉冲会被算进这一帧，图像整体偏移
                if (profile.UseFrameTrigger)
                {
                    await WaitAxisReachAsync(_scanAxis, profile.ScanStartMm, profile.Direction,
                        profile.AxisTimeoutMs, token);

                    if (!await _camera.SoftwareTriggerFrameAsync(token))
                        throw new InvalidOperationException($"[{MechanismName}] 帧软触发失败，扫描中止。");
                }

                // ⑦ 等一帧完整图像
                var frame = await _camera.WaitFrameAsync(profile.FrameTimeoutMs, token);

                // ⑧ 等轴走完（含减速余量），保证下一次动作从静止开始
                await WaitAxisMoveDoneAsync(_scanAxis, profile.AxisTimeoutMs, profile.MoveEndMm, token);

                _logger.Success($"[{MechanismName}] 扫描完成：{frame.Width}×{frame.Height}，"
                    + $"{frame.SizeBytes / 1024.0 / 1024.0:F2}MB，帧号 {frame.FrameNumber}。");

                return frame;
            }
            finally
            {
                // ⑨ 无论成败都要收尾：相机留在取流状态会占着缓存，轴留在运动中更危险
                if (grabbing)
                {
                    try { await _camera.StopAsync(CancellationToken.None); }
                    catch (Exception ex) { _logger.Warn($"[{MechanismName}] 收尾停流异常：{ex.Message}"); }
                }

                if (token.IsCancellationRequested)
                {
                    try { await _scanAxis.StopAsync(CancellationToken.None); }
                    catch (Exception ex) { _logger.Warn($"[{MechanismName}] 取消时停轴异常：{ex.Message}"); }
                }
            }
        }

        /// <summary>
        /// 按扫描配方组装相机配置：帧长、帧超时、曝光、帧触发开关由几何决定，覆盖基础配置里的同名项。
        /// 其余（像素格式、增益、编码器接线）沿用基础配置。
        /// </summary>
        private static LineScanCameraConfig BuildConfig(ScanProfile profile, LineScanCameraConfig? baseConfig)
        {
            var config = baseConfig ?? new LineScanCameraConfig();

            // 行触发固定为编码器：本模组的前提就是编码器直连相机
            config.LineTrigger.Mode = LineTriggerMode.Encoder;
            config.LineTrigger.AcquisitionLineRateEnable = false;   // 内部行频会与编码器抢，必须关

            config.LineTrigger.Encoder ??= new EncoderConfig();

            // 行间距是配方算出来的，回填进编码器配置，使相机侧 LineSpacingUm 与实际一致
            if (config.LineTrigger.Encoder.PulseEquivalentUm <= 0)
            {
                config.LineTrigger.Encoder.PulseEquivalentUm = profile.LineSpacingUm;
                config.LineTrigger.Encoder.DividerRatio = 1.0;
            }

            if (profile.ExposureTimeUs > 0)
                config.ExposureTimeUs = profile.ExposureTimeUs;

            config.FrameControl.ImageHeight = profile.FrameHeightLines;
            config.FrameControl.FrameTimeoutMs = profile.FrameTimeoutMs;
            config.FrameControl.TriggerEnable = profile.UseFrameTrigger;

            return config;
        }

        /// <summary>
        /// 等待轴到达（或越过）指定位置。
        /// <para>按扫描方向判断"到没到"：正向时位置 ≥ 目标，反向时 ≤ 目标。
        /// 用越过而非相等来判定，是因为轮询必然会错过精确相等的瞬间。</para>
        /// </summary>
        private async Task WaitAxisReachAsync(IAxis axis, double targetMm, int direction,
            int timeoutMs, CancellationToken token)
        {
            var deadline = DateTime.Now.AddMilliseconds(timeoutMs);

            while (true)
            {
                token.ThrowIfCancellationRequested();

                double? pos = axis.CurrentPosition;
                if (pos.HasValue)
                {
                    bool reached = direction > 0 ? pos.Value >= targetMm : pos.Value <= targetMm;
                    if (reached) return;
                }

                if (DateTime.Now > deadline)
                {
                    throw new TimeoutException($"[{MechanismName}] 等待轴到达 {targetMm:F2}mm 超时"
                        + $"（{timeoutMs}ms，当前位置 {pos?.ToString("F2") ?? "未知"}mm）。"
                        + "请确认轴确实在运动、且目标位置在行程范围内。");
                }

                await Task.Delay(ReachPollIntervalMs, token);
            }
        }

        #endregion
    }
}
