using PF.Core.Attributes;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Mechanisms;
using PF.Workstation.AutoOcr.CostParam;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Mechanisms
{
    /// <summary>
    /// 工位1晶圆上料模组
    ///
    /// 职责：封装工位1上料流程所需的三轴（Z上料轴、X挡料轴）与IO的联动控制，
    /// 对上层工站提供语义清晰的原子操作接口：
    ///   · InitializeFeedingStateAsync     — 初始化上料状态（各轴/气缸置于安全位）
    ///   · GetWaferBoxSizeAsync            — 识别料盒尺寸（8寸/12寸）
    ///   · SwitchProductionStateAsync      — 切换对应尺寸的生产状态（轨道调宽+X轴挡料）
    ///   · CanMoveZAxesAsync               — 判断Z轴是否具备运动条件
    ///   · CanMoveXAxesAsync               — 判断X轴是否具备运动条件
    ///   · SearchLayerAsync                — Z轴扫描寻层，返回实际晶圆层数
    ///   · SwitchToLayerAsync              — 切换到指定层
    ///   · CanPullOutMaterialAsync         — 判断是否具备拉出物料条件
    ///   · WaitUntilMaterialPulledOutAsync — 阻塞等待物料拉出完成
    ///   · WaitUntilMaterialReturnedAsync  — 阻塞等待物料回退完成
    ///
    /// 硬件获取策略（代理委托模式）：
    ///   构造函数仅注入 IHardwareManagerService 和 ILogService，
    ///   实际硬件实例在 InternalInitializeAsync 中通过 DeviceId 延迟解析，
    ///   确保 HardwareManagerService.LoadAndInitializeAsync() 完成后才访问设备。
    /// </summary>
    [MechanismUI("工位1上晶圆模组", "Workstation1FeedingModelDebugView", 1)]
    public class WorkStation1FeedingModule : BaseMechanism
    {
        // ── 硬件实例（InternalInitializeAsync 后可用）────────────────────────
        private IAxis _zAxis;      // 工位1上料Z轴：控制料盒升降对层
        private IAxis _xAxis;      // 工位1挡料X轴：控制挡料位置（8寸/12寸）
        private IIOController _io; // EtherCat IO 模块

        // ── 当前生产尺寸（SwitchProductionStateAsync 后记录）────────────────
        private E_WafeSize _currentWaferSize = E_WafeSize._8寸;

        // ── 工艺参数常量（实际项目建议从 IParamService 读取以支持界面调参）───
        // Z轴扫描
        private const double ZScanStartPos  = 5.0;   // mm：扫描起始位（料盒底层上方）
        private const double ZScanEndPos    = 260.0; // mm：扫描结束位（最大行程）
        private const double ZScanSpeed     = 15.0;  // mm/s：扫层慢速
        private const double ZFastSpeed     = 80.0;  // mm/s：快速空移
        private const double LayerPitch_8   = 6.5;   // mm：8寸晶圆层间距
        private const double LayerPitch_12  = 8.0;   // mm：12寸晶圆层间距
        private const double ZFirstLayer_8  = 10.0;  // mm：8寸第0层Z轴绝对坐标
        private const double ZFirstLayer_12 = 12.0;  // mm：12寸第0层Z轴绝对坐标

        // X轴挡料位置
        private const double XBlockPos_8   = 20.0;  // mm：8寸挡料位
        private const double XBlockPos_12  = 36.0;  // mm：12寸挡料位

        // 速度/加减速通用参数
        private const double FastAcc = 500.0;
        private const double FastDec = 500.0;
        private const double SlowAcc = 200.0;
        private const double SlowDec = 200.0;
        private const double STime   = 0.1;

        // 超时与轮询
        private const int CylinderTimeoutMs = 3000; // 气缸动作超时
        private const int PollIntervalMs    = 50;   // 等待轮询间隔

        // ── 公开硬件访问（供 ViewModel 调试面板绑定）────────────────────────
        public IAxis ZAxis => _zAxis;
        public IAxis XAxis => _xAxis;
        public IIOController IO => _io;

        public WorkStation1FeedingModule(IHardwareManagerService hardwareManagerService, ILogService logger)
            : base(E_Mechanisms.工位1上晶圆模组.ToString(), hardwareManagerService, logger)
        {
        }

        // ── BaseMechanism 钩子实现 ─────────────────────────────────────────

        /// <summary>
        /// 模组初始化：延迟解析三轴和IO → 注册报警聚合 → 连接/使能/回零 → 安全初始输出
        /// </summary>
        protected override async Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            // ① 延迟解析硬件实例
            _zAxis = HardwareManagerService?.GetDevice(E_AxisName.工位1上料Z轴.ToString()) as IAxis;
            if (_zAxis == null)
            {
                _logger.Error($"[{MechanismName}] 未找到Z轴 '{E_AxisName.工位1上料Z轴}'，请确认硬件配置。");
                return false;
            }

            _xAxis = HardwareManagerService?.GetDevice(E_AxisName.工位1挡料X轴.ToString()) as IAxis;
            if (_xAxis == null)
            {
                _logger.Error($"[{MechanismName}] 未找到X轴 '{E_AxisName.工位1挡料X轴}'，请确认硬件配置。");
                return false;
            }

            _io = HardwareManagerService?.GetDevice("IO_Collectorll") as IIOController;
            if (_io == null)
            {
                _logger.Error($"[{MechanismName}] 未找到IO模块 'IO_Collectorll'，请确认硬件配置。");
                return false;
            }

            // ② 注册到模组（报警事件聚合 + 批量复位，幂等）
            RegisterHardwareDevice(_zAxis as IHardwareDevice);
            RegisterHardwareDevice(_xAxis as IHardwareDevice);
            RegisterHardwareDevice(_io   as IHardwareDevice);

            // ③ 连接所有硬件（BaseDevice 内部有3次重试）
            if (!await _zAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] Z轴连接失败"); return false; }
            if (!await _xAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] X轴连接失败"); return false; }
            if (!await _io.ConnectAsync(token))    { _logger.Error($"[{MechanismName}] IO模块连接失败"); return false; }

            // ④ 使能伺服
            if (!await _zAxis.EnableAsync()) { _logger.Error($"[{MechanismName}] Z轴使能失败"); return false; }
            if (!await _xAxis.EnableAsync()) { _logger.Error($"[{MechanismName}] X轴使能失败"); return false; }
            

            // ⑤ 回原点
            if (!await _zAxis.HomeAsync(token)) { _logger.Error($"[{MechanismName}] Z轴回零失败"); return false; }
            if (!await _xAxis.HomeAsync(token)) { _logger.Error($"[{MechanismName}] X轴回零失败"); return false; }
            

            // ⑥ 安全初始输出：夹爪张开、轨道调宽气缸收回、X轴气缸缩回
            _io.WriteOutput(E_OutPutName.夹爪气缸左张开,          true);
          

            _logger.Success($"[{MechanismName}] 初始化完成，三轴已回零，输出已初始化。");
            return true;
        }

        /// <summary>
        /// 模组急停：停止所有轴运动，保持夹爪当前状态防止物料掉落
        /// </summary>
        protected override async Task InternalStopAsync()
        {
            if (_zAxis != null) await _zAxis.StopAsync();
            if (_xAxis != null) await _xAxis.StopAsync();
           
        }

        #region 晶圆上料模组业务流程方法

        /// <summary>
        /// 0. 初始化上料状态：气缸复位至安全位，Z轴移到扫描起始位
        /// </summary>
        public async Task InitializeFeedingStateAsync(CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 初始化上料状态...");

          

            // 等待夹爪张开到位
            bool jawOpen = await _io.WaitInputAsync(E_InPutName.晶圆夹爪左气缸张开, true, CylinderTimeoutMs, token);
            if (!jawOpen)
                throw new Exception($"[{MechanismName}] 初始化失败：夹爪张开超时，请检查气缸。");

            // 等待X轴气缸缩回到位
            bool xRetracted = await _io.WaitInputAsync(E_InPutName.晶圆夹爪左X轴气缸缩回, true, CylinderTimeoutMs, token);
            if (!xRetracted)
                throw new Exception($"[{MechanismName}] 初始化失败：X轴气缸缩回超时，请检查气缸。");

            // Z轴移到扫描起始位
            if (!await _zAxis.MoveAbsoluteAsync(ZScanStartPos, ZFastSpeed, FastAcc, FastDec, STime, token))
                throw new Exception($"[{MechanismName}] 初始化失败：Z轴移动到起始位失败。");

            _logger.Success($"[{MechanismName}] 上料状态初始化完成。");
        }

        /// <summary>
        /// 1. 判断晶圆盒类型：通过8寸/12寸防反传感器识别料盒尺寸
        /// </summary>
        /// <returns>E_WafeSize._8寸 或 E_WafeSize._12寸</returns>
        public async Task<E_WafeSize> GetWaferBoxSizeAsync(CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 检测晶圆料盒尺寸...");

            await Task.CompletedTask;

            // 料盒铁环在位检测
            if (_io.ReadInput(E_InPutName.上晶圆左铁环位置检测) != true)
                throw new Exception($"[{MechanismName}] 未检测到料盒铁环，请确认料盒已放置到位。");

            // 料盒倾斜安全校验
            bool tilt1 = _io.ReadInput(E_InPutName.上晶圆左料盒倾斜检测1) == true;
            bool tilt2 = _io.ReadInput(E_InPutName.上晶圆左料盒倾斜检测2) == true;
            if (tilt1 || tilt2)
                throw new Exception($"[{MechanismName}] 料盒倾斜（tilt1={tilt1}, tilt2={tilt2}），请重新放置后再检测。");

            // 尺寸防反传感器
            bool is8inch  = _io.ReadInput(E_InPutName.上晶圆左8寸铁环防反检测)  == true;
            bool is12inch = _io.ReadInput(E_InPutName.上晶圆左12寸铁环防反检测) == true;

            if (is8inch && !is12inch)
            {
                _logger.Success($"[{MechanismName}] 识别到 8寸 晶圆料盒。");
                return E_WafeSize._8寸;
            }
            else if (is12inch && !is8inch)
            {
                _logger.Success($"[{MechanismName}] 识别到 12寸 晶圆料盒。");
                return E_WafeSize._12寸;
            }
            else
            {
                throw new Exception($"[{MechanismName}] 料盒尺寸识别异常（8寸={is8inch}, 12寸={is12inch}），请检查传感器或料盒放置。");
            }
        }

        /// <summary>
        /// 2. 切换生产状态：根据晶圆尺寸调整轨道调宽气缸和X轴挡料位置
        /// </summary>
        public async Task<bool> SwitchProductionStateAsync(E_WafeSize waferSize, CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 切换生产状态为 [{waferSize}]...");

            if (waferSize == E_WafeSize._8寸)
            {
                // 8寸：轨道调宽气缸收回（窄轨）
                _io.WriteOutput(E_OutPutName.晶圆轨道左调宽气缸伸出, false);
                _io.WriteOutput(E_OutPutName.晶圆轨道左调宽气缸收回, true);

                bool retracted = await _io.WaitInputAsync(E_InPutName.晶圆轨道左调宽气缸磁缩回, true, CylinderTimeoutMs, token);
                if (!retracted)
                    throw new Exception($"[{MechanismName}] 8寸状态切换失败：轨道调宽气缸缩回超时。");

                // X轴移至8寸挡料位
                if (!await _xAxis.MoveAbsoluteAsync(XBlockPos_8, ZFastSpeed, FastAcc, FastDec, STime, token))
                    throw new Exception($"[{MechanismName}] 8寸状态切换失败：X轴移动到8寸挡料位失败。");

                if (_io.ReadInput(E_InPutName.上晶圆左8寸料盒挡杆检测) != true)
                    _logger.Warn($"[{MechanismName}] 8寸挡杆检测未触发，请确认挡杆位置。");
            }
            else // 12寸
            {
                // 12寸：轨道调宽气缸伸出（宽轨）
                _io.WriteOutput(E_OutPutName.晶圆轨道左调宽气缸收回, false);
                _io.WriteOutput(E_OutPutName.晶圆轨道左调宽气缸伸出, true);

                bool extended = await _io.WaitInputAsync(E_InPutName.晶圆轨道左调宽气缸打开, true, CylinderTimeoutMs, token);
                if (!extended)
                    throw new Exception($"[{MechanismName}] 12寸状态切换失败：轨道调宽气缸伸出超时。");

                // X轴移至12寸挡料位
                if (!await _xAxis.MoveAbsoluteAsync(XBlockPos_12, ZFastSpeed, FastAcc, FastDec, STime, token))
                    throw new Exception($"[{MechanismName}] 12寸状态切换失败：X轴移动到12寸挡料位失败。");

                bool stopper1 = _io.ReadInput(E_InPutName.上晶圆左12寸料盒挡杆检测1) == true;
                bool stopper2 = _io.ReadInput(E_InPutName.上晶圆左12寸料盒挡杆检测2) == true;
                if (!stopper1 && !stopper2)
                    _logger.Warn($"[{MechanismName}] 12寸挡杆检测均未触发，请确认挡杆位置。");
            }

            _currentWaferSize = waferSize;
            _logger.Success($"[{MechanismName}] 生产状态已切换为 [{waferSize}]。");
            return true;
        }

        /// <summary>
        /// 3. 判断是否具备动Z轴的条件：料盒在位、未倾斜、Z轴无报警
        /// </summary>
        public async Task<bool> CanMoveZAxesAsync(CancellationToken token = default)
        {
            await Task.CompletedTask;

            if (!IsInitialized)
            {
                _logger.Warn($"[{MechanismName}] Z轴运动检查失败：模组未初始化。");
                return false;
            }

            if (_io.ReadInput(E_InPutName.上晶圆左铁环位置检测) != true)
            {
                _logger.Warn($"[{MechanismName}] Z轴运动检查失败：料盒铁环未在位。");
                return false;
            }

            if (_io.ReadInput(E_InPutName.上晶圆左料盒倾斜检测1) == true ||
                _io.ReadInput(E_InPutName.上晶圆左料盒倾斜检测2) == true)
            {
                _logger.Warn($"[{MechanismName}] Z轴运动检查失败：料盒倾斜，禁止Z轴运动。");
                return false;
            }

            if (_zAxis.HasAlarm)
            {
                _logger.Warn($"[{MechanismName}] Z轴运动检查失败：Z轴处于报警状态。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 3. 判断是否具备动X轴的条件：X轴无报警、夹爪张开（避免碰撞）
        /// </summary>
        public async Task<bool> CanMoveXAxesAsync(CancellationToken token = default)
        {
            await Task.CompletedTask;

            if (!IsInitialized)
            {
                _logger.Warn($"[{MechanismName}] X轴运动检查失败：模组未初始化。");
                return false;
            }

            if (_xAxis.HasAlarm)
            {
                _logger.Warn($"[{MechanismName}] X轴运动检查失败：X轴处于报警状态。");
                return false;
            }

            // 夹爪须张开，防止X轴运动时碰撞晶圆
            if (_io.ReadInput(E_InPutName.晶圆夹爪左气缸张开) != true)
            {
                _logger.Warn($"[{MechanismName}] X轴运动检查失败：夹爪未张开，禁止X轴运动。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 4. 寻层：Z轴从起始位向下慢速点动，通过错层传感器上升沿计数晶圆层数
        /// </summary>
        /// <returns>检测到的晶圆层总数（0 表示料盒为空）</returns>
        public async Task<int> SearchLayerAsync(CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 开始Z轴寻层扫描...");

            // 先移到扫描起始位
            if (!await _zAxis.MoveAbsoluteAsync(ZScanStartPos, ZFastSpeed, FastAcc, FastDec, STime, token))
                throw new Exception($"[{MechanismName}] 寻层失败：Z轴移动到扫描起始位失败。");

            int  layerCount = 0;
            bool lastState  = false;

            // 启动Z轴向下慢速点动
            if (!await _zAxis.JogAsync(ZScanSpeed, true, SlowAcc, SlowDec))
                throw new Exception($"[{MechanismName}] 寻层失败：Z轴点动启动失败。");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    // 超出扫描范围则停止
                    if (_zAxis.CurrentPosition.HasValue && _zAxis.CurrentPosition.Value >= ZScanEndPos)
                        break;

                    // 任一错层传感器触发即认为检测到晶圆
                    bool detected =
                        _io.ReadInput(E_InPutName.上晶圆左错层检测1) == true ||
                        _io.ReadInput(E_InPutName.上晶圆左错层检测2) == true ||
                        _io.ReadInput(E_InPutName.上晶圆左错层检测3) == true;

                    // 上升沿：无 → 有，计一层
                    if (detected && !lastState)
                        layerCount++;

                    lastState = detected;
                    await Task.Delay(PollIntervalMs, token);
                }
            }
            finally
            {
                // 无论正常结束还是异常，都先停轴
                await _zAxis.StopAsync();
            }

            // 扫描完毕，Z轴回到起始位
            await _zAxis.MoveAbsoluteAsync(ZScanStartPos, ZFastSpeed, FastAcc, FastDec, STime, token);

            _logger.Success($"[{MechanismName}] 寻层完成，共检测到 {layerCount} 层晶圆。");
            return layerCount;
        }

        /// <summary>
        /// 5. 切换层数：将Z轴移动到目标层对应的绝对坐标
        /// </summary>
        /// <param name="targetLayer">目标层序号（从0开始）</param>
        public async Task<bool> SwitchToLayerAsync(int targetLayer, CancellationToken token = default)
        {
            CheckReady();

            if (targetLayer < 0)
                throw new ArgumentOutOfRangeException(nameof(targetLayer), "层序号不能为负数。");

            double pitch    = _currentWaferSize == E_WafeSize._8寸 ? LayerPitch_8   : LayerPitch_12;
            double firstPos = _currentWaferSize == E_WafeSize._8寸 ? ZFirstLayer_8  : ZFirstLayer_12;
            double targetZ  = firstPos + targetLayer * pitch;

            _logger.Info($"[{MechanismName}] 切换至第 {targetLayer} 层，目标Z = {targetZ:F2} mm。");

            bool ok = await _zAxis.MoveAbsoluteAsync(targetZ, ZFastSpeed, FastAcc, FastDec, STime, token);
            if (!ok)
                throw new Exception($"[{MechanismName}] 切换层失败：Z轴移动到 {targetZ:F2} mm 失败。");

            _logger.Success($"[{MechanismName}] 已到达第 {targetLayer} 层（Z={targetZ:F2} mm）。");
            return true;
        }

        /// <summary>
        /// 6. 判断是否具备拉出物料的条件：
        ///    · 夹爪铁环有无检测（晶圆已在夹爪中）
        ///    · X轴气缸已打开（挡料已让开）
        ///    · 无叠片/卡料报警
        ///    · Y轴无报警
        /// </summary>
        public async Task<bool> CanPullOutMaterialAsync(CancellationToken token = default)
        {
            await Task.CompletedTask;

            if (!IsInitialized)
            {
                _logger.Warn($"[{MechanismName}] 拉料检查失败：模组未初始化。");
                return false;
            }

            // 叠片/叠料检测
            if (_io.ReadInput(E_InPutName.晶圆夹爪左叠片检测) == true ||
                _io.ReadInput(E_InPutName.夹爪左叠料检测)     == true)
            {
                _logger.Warn($"[{MechanismName}] 拉料检查失败：检测到叠片，禁止拉料。");
                return false;
            }

            // 卡料检测
            if (_io.ReadInput(E_InPutName.晶圆夹爪左卡料检测1) == true ||
                _io.ReadInput(E_InPutName.晶圆夹爪左卡料检测2) == true)
            {
                _logger.Warn($"[{MechanismName}] 拉料检查失败：检测到卡料，禁止拉料。");
                return false;
            }

            // 晶圆是否已在夹爪中
            if (_io.ReadInput(E_InPutName.晶圆夹爪左铁环有无检测) != true)
            {
                _logger.Warn($"[{MechanismName}] 拉料检查失败：夹爪未检测到晶圆铁环。");
                return false;
            }

            // X轴气缸打开（挡料已让开）
            if (_io.ReadInput(E_InPutName.晶圆夹爪左X轴气缸打开) != true)
            {
                _logger.Warn($"[{MechanismName}] 拉料检查失败：X轴气缸未打开（挡料未让开）。");
                return false;
            }

          
            return true;
        }

        /// <summary>
        /// 7. 阻塞等待物料拉出完成：轮询轨道在位检测信号（任一触发即视为到位），带超时防死等
        /// </summary>
        /// <param name="timeoutMilliseconds">最大等待时间（毫秒）</param>
        public async Task<bool> WaitUntilMaterialPulledOutAsync(int timeoutMilliseconds = 5000, CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 等待物料拉出（超时={timeoutMilliseconds}ms）...");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeoutMilliseconds);

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    bool inPos1 = _io.ReadInput(E_InPutName.晶圆轨道左晶圆在位检测1) == true;
                    bool inPos2 = _io.ReadInput(E_InPutName.晶圆轨道左晶圆在位检测2) == true;

                    if (inPos1 || inPos2)
                    {
                        _logger.Success($"[{MechanismName}] 物料已拉出到位（检测1={inPos1}, 检测2={inPos2}）。");
                        return true;
                    }

                    await Task.Delay(PollIntervalMs, cts.Token);
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                // 超时取消，非外部取消 → 返回 false
            }

            _logger.Warn($"[{MechanismName}] 等待物料拉出超时（{timeoutMilliseconds}ms），请检查Y轴动作或传感器。");
            return false;
        }

        /// <summary>
        /// 8. 阻塞等待物料回退完成：轮询轨道在位检测均变为 false（物料已完全离开轨道），带超时防死等
        /// </summary>
        /// <param name="timeoutMilliseconds">最大等待时间（毫秒）</param>
        public async Task<bool> WaitUntilMaterialReturnedAsync(int timeoutMilliseconds = 5000, CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 等待物料回退（超时={timeoutMilliseconds}ms）...");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeoutMilliseconds);

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    bool inPos1 = _io.ReadInput(E_InPutName.晶圆轨道左晶圆在位检测1) == true;
                    bool inPos2 = _io.ReadInput(E_InPutName.晶圆轨道左晶圆在位检测2) == true;

                    // 两个传感器均为 false → 物料已完全离开
                    if (!inPos1 && !inPos2)
                    {
                        _logger.Success($"[{MechanismName}] 物料已回退完成，轨道清空。");
                        return true;
                    }

                    await Task.Delay(PollIntervalMs, cts.Token);
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                // 超时取消
            }

            _logger.Warn($"[{MechanismName}] 等待物料回退超时（{timeoutMilliseconds}ms），请检查Y轴动作或传感器。");
            return false;
        }

        #endregion
    }
}
