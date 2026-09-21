using PF.Core.Attributes;
using PF.Core.Entities.Hardware;
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
    /// <para><b>像一台"设备"</b>：给起点/终点两个轴点位名，吐一张图，仅此而已。
    /// 位置/速度/加减速/S曲线时间一律来自轴自己的点位表（<see cref="AxisPoint"/>）——
    /// 机台上其它轴全部走"点位表 + 点位名"，线扫轴没有理由在这单独维护一套重复的运动参数，
    /// 也不会因为字段命名和轴的实际单位对不上而踩坑（见 <see cref="ScanGeometry"/> 的说明）。
    /// 帧长/行频/曝光这类成像参数也不在这里——它们已经配置在相机自己身上（UserSetDefault），
    /// 本模组不重新计算、不下发覆盖，只在需要临时改曝光这类个别项时，由调用方在
    /// <see cref="LineScanCameraConfig"/> 里显式指定。</para>
    ///
    /// <para><b>为什么必须有这一层</b>：线阵相机每次只曝光一行，图像的第二个维度完全由运动提供。
    /// 相机自己不知道轴走到哪了，轴也不知道相机在不在取流——两者的时序只能由机构层负责。
    /// 设备层（<see cref="ILineScanCamera"/>）刻意不引用 <see cref="IAxis"/>，
    /// 就是为了把这份时序职责收敛在这里。</para>
    ///
    /// <para><b>编码器接线</b>：行触发方式完全由调用方通过 <see cref="LineScanCameraConfig.LineTrigger"/>
    /// 显式指定——编码器可以直连相机 IO（<see cref="LineTriggerMode.Encoder"/>），也可以接在
    /// 采集卡/其它设备上、再以外部信号线的形式转发给相机（<see cref="LineTriggerMode.ExternalLine"/>，
    /// 如 CameraLink 的 <c>LinkTrigger0</c>）。调用方不指定时（<see cref="LineTriggerConfig.Mode"/>
    /// 为 null）本模组<b>不会替调用方猜一个模式</b>，相机侧整段行触发配置原样跳过、沿用当前接线——
    /// 猜错就是拿现场已经验证过的真实接线去覆盖，这不是假设性的风险：本模组早期版本会在调用方
    /// 未指定时兜底成编码器直连，结果被一次"只想改曝光"的调用悄悄把 ExternalLine 接线覆盖成了
    /// 错的 Encoder 接线。不论哪种接线，模组本身只负责「让轴以恒定速度走过扫描区」，
    /// 逐行触发都由相机侧产生，模组不参与逐行同步。</para>
    ///
    /// <para><b>时序要点</b>：开流、帧触发都必须早于轴运动，否则起始若干行会丢或被算进上一帧。
    /// 起点到终点这段行程里含不含足够的加减速余量，由起点/终点两个点位在点表里的位置自己决定——
    /// 本模组不再自动往外插一段"回退加速距离"，行程头尾几行的曝光时间会因为还没提上速/正在减速
    /// 而略有不均，如果这点亮度差异对检测有影响，请在轴点位表里把起点/终点往外多挪一点。</para>
    /// </summary>
    [MechanismUI("线扫检测模组", "LineScanDetectionModuleDebugView", 20)]
    public class LineScanDetectionModule : BaseMechanism
    {
        /// <summary>轴移动到位/走完的等待超时（ms）。不是成像相关量，不需要按扫描配置，固定给个宽裕值。</summary>
        private const int AxisTimeoutMs = 60_000;

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

            // 注意：这里不要把相机的 Parent（采集卡）也注册进本模组的硬件快照。快照复位是按注册顺序
            // 逐个来的，卡排在相机后面复位，卡重连后相机原来的流绑定就失效了，开流报 0x80000000。
            // 卡的复位由相机自己的 InternalResetAsync 负责（关相机 → 复位卡 → 重开相机），顺序才对。

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
        /// <para>时序：校验几何 → 下发相机配置 → 轴移动到起点并等停稳 → 开流 → 武装帧触发 →
        /// 启动扫描运动（不等待）→ 等一帧 → 等轴走完 → 停流。</para>
        /// </summary>
        /// <param name="startPointName">扫描起点的轴点位名（图像第一行对应的位置）。</param>
        /// <param name="endPointName">
        /// 扫描终点的轴点位名（图像最后一行对应的位置）。本次扫描实际用的速度/加速度/减速度/
        /// S曲线时间，全部取自这个点位在轴点位表里的配置。
        /// </param>
        /// <param name="baseConfig">
        /// 相机配置（像素格式、增益、编码器接线、曝光等）。为 null 时新建一份默认配置，
        /// 此时不下发任何相机侧参数，沿用相机当前值（通常是它自己的 UserSetDefault）。
        /// 帧长/行频不在这里覆盖——它们已经配置在相机自己身上，本模组不重新计算。
        /// </param>
        /// <param name="token">取消令牌。</param>
        /// <returns>扫描得到的一帧完整图像。</returns>
        /// <exception cref="KeyNotFoundException">起点或终点点位在轴点位表里不存在。</exception>
        /// <exception cref="InvalidOperationException">几何校验不通过，或轴/相机动作失败。</exception>
        /// <exception cref="TimeoutException">在帧超时内没有收到完整帧。</exception>
        public async Task<LineScanFrame> ScanAsync(string startPointName, string endPointName,
            LineScanCameraConfig? baseConfig = null, CancellationToken token = default)
        {
            CheckReady();

            if (_scanAxis == null || _camera == null)
                throw new InvalidOperationException($"模组 [{MechanismName}] 尚未解析到扫描轴或相机。");

            var startPoint = FindPoint(startPointName);
            var endPoint = FindPoint(endPointName);
            var geometry = new ScanGeometry(startPoint, endPoint);

            // ① 几何自洽性校验：起点/终点位置不对、终点没配速度，这些不拦住的话，
            //    症状都只是"轴动不起来"或"图整体偏移"，事后排查代价更高
            var problems = geometry.Validate();
            if (problems.Count > 0)
            {
                string detail = string.Join("\n  · ", problems);
                throw new InvalidOperationException($"[{MechanismName}] 扫描几何校验未通过：\n  · {detail}");
            }

            _logger.Info($"[{MechanismName}] 开始扫描：{geometry}");

            bool grabbing = false;
            try
            {
                // ② 下发相机配置（曝光等个别项由调用方指定，其余沿用相机自身当前值）
                var config = BuildConfig(baseConfig);
                await _camera.ApplyConfigAsync(config, token);

                // ③ 轴移动到起点，完全停稳（用起点自身在点表里配置的速度/加减速）
                if (!await MoveToPointAndWaitAsync(_scanAxis, startPointName, AxisTimeoutMs, token))
                    throw new InvalidOperationException($"[{MechanismName}] 轴未能到达扫描起点 '{startPointName}'。");

                // ④ 开流——必须早于轴运动，晚了会丢掉起始若干行
                if (!await _camera.ArmAsync(token))
                    throw new InvalidOperationException($"[{MechanismName}] 相机开流失败，扫描中止。");

                grabbing = true;

                // ⑤ 帧触发——先武装好帧再让轴动。不再有独立的回退加速点，
                //    轴一启动就已经在扫描区内，没有"等到达某个中间位置再触发"的必要
                if (!await _camera.SoftwareTriggerFrameAsync(token))
                    throw new InvalidOperationException($"[{MechanismName}] 帧软触发失败，扫描中止。");

                // ⑥ 启动扫描运动（用终点自身在点表里配置的速度/加减速），**不等待到位**——
                //    后面还要在运动过程中收帧
                if (!await _scanAxis.MoveToPointAsync(endPointName, token))
                    throw new InvalidOperationException($"[{MechanismName}] 扫描运动指令下发失败。");

                // ⑦ 等一帧完整图像
                LineScanFrame frame;
                try
                {
                    frame = await _camera.WaitFrameAsync(geometry.FrameTimeoutMs, token);
                }
                catch (TimeoutException)
                {
                    // 超时后帧超时远大于运动时间，此时轴应已到终点：位置到了而帧没满，
                    // 说明是行数不够/触发问题；位置没到，才是轴的问题。相机侧的诊断见"取帧超时诊断"。
                    double? pos = _scanAxis.CurrentPosition;
                    _logger.Warn($"[{MechanismName}] 取帧超时时扫描轴位置：当前 {(pos.HasValue ? pos.Value.ToString("F2") : "未知")}，"
                        + $"终点 {geometry.EndMm:F2}"
                        + (pos.HasValue ? $"（差 {Math.Abs(pos.Value - geometry.EndMm):F2}）。" : "。"));
                    throw;
                }

                // ⑧ 等轴走完，保证下一次动作从静止开始
                await WaitAxisMoveDoneAsync(_scanAxis, AxisTimeoutMs, geometry.EndMm, token);

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
        /// 组装相机配置：只补一件本模组的时序恒定依赖的事——帧触发必须打开（本模组的扫描流程
        /// 恒定走帧触发）。行触发方式完全交给调用方：<see cref="LineTriggerConfig.Mode"/> 为
        /// null 时相机侧会整段跳过、沿用当前接线，本模组不再替调用方猜一个模式——猜错就是拿
        /// 真实接线去覆盖，此前就吃过这个亏（调试页只填曝光、没碰行触发，结果被自动兜底成
        /// Encoder 模式，把现场验证过的 ExternalLine 接线覆盖掉）。其余（像素格式、增益、
        /// 曝光、帧长、行频……）一律沿用调用方传入的配置或相机自身当前值，不重新计算、不覆盖。
        /// </summary>
        private static LineScanCameraConfig BuildConfig(LineScanCameraConfig? baseConfig)
        {
            var config = baseConfig ?? new LineScanCameraConfig();

            config.FrameControl.TriggerEnable = true;

            return config;
        }

        /// <summary>按名称在扫描轴点表里查找点位；不存在则抛出，报错信息直接指路"去哪加"。</summary>
        private AxisPoint FindPoint(string pointName)
        {
            return _scanAxis!.PointTable.FirstOrDefault(p => p.Name == pointName)
                ?? throw new KeyNotFoundException($"[{MechanismName}] 扫描轴未找到点位 '{pointName}'，"
                    + "请先在轴调试页的点位表里添加（位置/速度/加减速）。");
        }

        #endregion
    }
}
