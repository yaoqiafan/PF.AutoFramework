using log4net.Appender;
using NPOI.SS.UserModel;
using PF.Core.Attributes;
using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Mechanisms;
using PF.Workstation.AutoOcr.CostParam;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    ///   · CanMoveZAxesAsync               — 判断Z轴是否具备运动条件 (防撞互锁)
    ///   · CanMoveXAxesAsync               — 判断X轴是否具备运动条件 (防撞互锁)
    ///   · SearchLayerAsync                — Z轴扫描寻层，返回实际晶圆层数
    ///   · SwitchToLayerAsync              — 切换到指定层
    ///   · CanPullOutMaterialAsync         — 判断是否具备拉出物料条件
    ///   · WaitUntilMaterialPulledOutAsync — 阻塞等待物料拉出完成 (带超时防死等)
    ///   · WaitUntilMaterialReturnedAsync  — 阻塞等待物料回退完成 (带超时防死等)
    ///
    /// 硬件获取策略（代理委托模式）：
    ///   构造函数仅注入 IHardwareManagerService 和 ILogService，
    ///   实际硬件实例在 InternalInitializeAsync 中通过 DeviceId 延迟解析，
    ///   确保 HardwareManagerService.LoadAndInitializeAsync() 完成后才访问设备。
    /// </summary>
    [MechanismUI("工位1上晶圆模组", "Workstation1FeedingModelDebugView", 1)]
    public class WorkStation1FeedingModule : BaseMechanism
    {
        #region 轴点位枚举定义
        // 定义Z轴（升降轴）的关键示教点位
        public enum ZAxisPoint
        {
            正极限位置,       // 硬件/软件正极限保护位
            待机位,           // 默认的安全高度位置，通常在最上方以避让其他机构
            层1取料位_8寸,    // 8寸料盒第1层晶圆的绝对坐标基准点
            层1取料位_12寸,   // 12寸料盒第1层晶圆的绝对坐标基准点
            负极限位置,       // 硬件/软件负极限保护位
        }

        // 定义X轴（挡料/调宽轴）的关键示教点位
        public enum XAxisPoint
        {
            待机位,           // 挡料机构退回的安全位置，不干涉上下料
            挡料位_8寸,       // 适应8寸晶圆盒宽度的挡料位置
            挡料位_12寸,      // 适应12寸晶圆盒宽度的挡料位置
        }
        #endregion

        // ── 硬件实例（在底层执行 InternalInitializeAsync 后才被实例化赋值）──
        private IAxis _zAxis;      // 工位1上料Z轴：控制料盒的整体升降，实现层级对准
        private IAxis _xAxis;      // 工位1挡料X轴：控制前挡料机构的位置（适应不同尺寸料盒）
        private IIOController _io; // EtherCat IO 模块：负责读取传感器状态和控制气缸

        // ── 生产配方参数缓存（在 SwitchProductionStateAsync 后更新）──
        private E_WafeSize _currentWaferSize = E_WafeSize._8寸; // 当前正在生产的晶圆尺寸
        private int _maxLayerCount = 13; // 标准料盒的最大层数

        // 动态读取的配方参数
        private double LayerPitch = 0;                  // 晶圆层间距（每层之间的高度差，用于阵列计算）
        private double WaferScanningPositiveOffset = 0; // 扫描寻层时的正向补偿值（传感器触发延迟补偿）
        private double WaferScanningNegativeOffset = 0; // 扫描寻层时的负向补偿值

        //── 轴阵列点位缓存：用于保存计算好的所有晶圆层坐标（1~25层）── 
        public readonly ConcurrentDictionary<int, AxisPoint> PickingPosition_8;  // 8寸料盒层坐标字典
        public readonly ConcurrentDictionary<int, AxisPoint> PickingPosition_12; // 12寸料盒层坐标字典

        // ── 公开的硬件访问属性（主要提供给前端 ViewModel 绑定，用于调试面板的手动控制）──
        public IAxis ZAxis => _zAxis;
        public IAxis XAxis => _xAxis;
        public IIOController IO => _io;

        /// <summary>
        /// 构造函数：依赖注入基础服务
        /// </summary>
        public WorkStation1FeedingModule(IHardwareManagerService hardwareManagerService, IParamService paramService, ILogService logger)
            : base(E_Mechanisms.工位1上晶圆模组.ToString(), hardwareManagerService, paramService, logger)
        {
            // 初始化线程安全的字典用于存放层阵列坐标
            PickingPosition_8 = new ConcurrentDictionary<int, AxisPoint>();
            PickingPosition_12 = new ConcurrentDictionary<int, AxisPoint>();
        }


        // ── BaseMechanism 框架钩子实现 ─────────────────────────────────────

        /// <summary>
        /// 模组初始化核心逻辑：延迟解析三轴和IO → 注册报警聚合 → 连接/使能/回零
        /// </summary>
        protected override async Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            // ① 延迟解析硬件实例：通过硬件管理器及配置名称查找具体硬件对象
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

            // ② 将硬件注册到当前模组：这样当单一轴/IO发生报警时，整个模组的状态会自动同步为 Alarm
            RegisterHardwareDevice(_zAxis as IHardwareDevice);
            RegisterHardwareDevice(_xAxis as IHardwareDevice);
            RegisterHardwareDevice(_io as IHardwareDevice);

            // 确认点位枚举在轴配置中都已示教/创建
            await ConfirmEunmPoints();

            // ③ 并行/串行连接所有硬件通信
            if (!await _zAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] Z轴连接失败"); return false; }
            if (!await _xAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] X轴连接失败"); return false; }
            if (!await _io.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] IO模块连接失败"); return false; }

            // ④ 使能伺服电机（Power On）
            if (!await _zAxis.EnableAsync()) { _logger.Error($"[{MechanismName}] Z轴使能失败"); return false; }
            if (!await _xAxis.EnableAsync()) { _logger.Error($"[{MechanismName}] X轴使能失败"); return false; }

            // ⑤ 执行回原点操作，建立机械绝对坐标系----异常  回零前传感器检查，物料是否有一半放在盒子里，回零过程中监控传感器状态，确保安全；回零失败要有明确的报警信息提示
            if (!await _zAxis.HomeAsync(token)) { _logger.Error($"[{MechanismName}] Z轴回零失败"); return false; }
            if (!await _xAxis.HomeAsync(token)) { _logger.Error($"[{MechanismName}] X轴回零失败"); return false; }

            _logger.Success($"[{MechanismName}] 初始化完成，轴已回零。");
            return true;
        }

        /// <summary>
        /// 模组急停/停止钩子：安全停止所有轴运动
        /// </summary>
        protected override async Task InternalStopAsync()
        {
            if (_zAxis != null) await _zAxis.StopAsync();
            if (_xAxis != null) await _xAxis.StopAsync();
        }

        #region 晶圆上料模组核心业务流程方法

        /// <summary>
        /// 0. 初始化上料状态：将所有机构复位至安全位置，准备迎接人工或AGV放料
        /// </summary>
        public async Task InitializeFeedingStateAsync(CancellationToken token = default)
        {
            CheckReady(); // 确保模组已初始化且无报警
            _logger.Info($"[{MechanismName}] 初始化上料状态...");

            // 多轴插补或同时运动：将X轴和Z轴同时移动到待机位，节省时间
            if (await MoveMultiAxesToPointsAsync(new[] { (_xAxis, nameof(XAxisPoint.待机位)), (_zAxis, nameof(ZAxisPoint.待机位)), }, token: token))
            {
                _logger.Success($"[{MechanismName}] 上料状态初始化完成。");
            }
            else
            {
                _logger.Warn($"[{MechanismName}] 上料状态初始化失败。");
            }
        }

        /// <summary>
        /// 1. 识别晶圆料盒尺寸（防呆校验设计）
        /// 通过底部三个光电传感器的组合状态来判断是8寸料盒、12寸料盒，还是放置异常。
        /// </summary>
        public async Task<E_WafeSize> GetWaferBoxSizeAsync(CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 检测晶圆料盒尺寸...");

            // 读取底层 IO 传感器信号
            bool iscom = _io.ReadInput(E_InPutName.上晶圆左料盒公用到位检测) == true; // 公用底座传感器
            bool is8inch = _io.ReadInput(E_InPutName.上晶圆左8寸料盒到位检测) == true; // 8寸独有感应位
            bool is12inch = _io.ReadInput(E_InPutName.上晶圆左12寸料盒到位检测) == true;// 12寸独有感应位

            // 逻辑判断：公用位必须有信号，否则说明根本没放料盒或料盒悬空
            if (iscom)
            {
                // 仅触发8寸传感器：判定为8寸料盒
                if (is8inch && !is12inch)
                {
                    _logger.Success($"[{MechanismName}] 识别到 8寸 晶圆料盒。");
                    _currentWaferSize = E_WafeSize._8寸;
                    return E_WafeSize._8寸;
                }
                // 仅触发12寸传感器：判定为12寸料盒
                else if (is12inch && !is8inch)
                {
                    _logger.Success($"[{MechanismName}] 识别到 12寸 晶圆料盒。");
                    _currentWaferSize = E_WafeSize._12寸;
                    return E_WafeSize._12寸;
                }
                else
                {
                    // 两个都触发或都不触发：料盒放歪了，或者传感器损坏（防反、防错位）
                    throw new Exception($"[{MechanismName}] 料盒尺寸识别异常（8寸={is8inch}, 12寸={is12inch}），请检查传感器或料盒是否放置倾斜。");
                }
            }
            else
            {
                throw new Exception($"[{MechanismName}] 晶圆料盒公用底座未检测到物体，请检查料盒是否放入。");
            }
        }

        /// <summary>
        /// 2. 切换生产状态：根据识别到的晶圆尺寸，下发对应配方参数，并驱动硬件作出物理调整
        /// </summary>
        public async Task<bool> SwitchProductionStateAsync(E_WafeSize waferSize, CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 切换生产状态为 [{waferSize}]...");
            _currentWaferSize = waferSize; // 更新当前尺寸状态

            if (waferSize == E_WafeSize._8寸)
            {
                // 动态获取8寸产品的工艺参数
                LayerPitch = await ParamService.GetParamAsync<double>(E_Params.LayerPitch_8.ToString());
                WaferScanningPositiveOffset = await ParamService.GetParamAsync<double>(E_Params.WaferScanningPositiveOffset_8.ToString());
                WaferScanningNegativeOffset = await ParamService.GetParamAsync<double>(E_Params.WaferScanningNegativeOffset_8.ToString());

                // 物理动作：移动挡料X轴到8寸专属的夹紧/挡料位置
                await MoveToPointAndWaitAsync(_xAxis, nameof(XAxisPoint.挡料位_8寸), token: token);

                // 逻辑动作：根据8寸基准点，重新推演计算所有25层的理论坐标
                await ArrayZAxisMaterialPickingPosition(E_WafeSize._8寸);
            }
            else if (waferSize == E_WafeSize._12寸)
            {
                // 动态获取12寸产品的工艺参数
                LayerPitch = await ParamService.GetParamAsync<double>(E_Params.LayerPitch_12.ToString());
                WaferScanningPositiveOffset = await ParamService.GetParamAsync<double>(E_Params.WaferScanningPositiveOffset_12.ToString());
                WaferScanningNegativeOffset = await ParamService.GetParamAsync<double>(E_Params.WaferScanningNegativeOffset_12.ToString());

                // 物理动作：移动挡料X轴到12寸专属位置
                await MoveToPointAndWaitAsync(_xAxis, nameof(XAxisPoint.挡料位_12寸), token: token);

                // 逻辑动作：生成12寸坐标阵列
                await ArrayZAxisMaterialPickingPosition(E_WafeSize._12寸);
            }

            _logger.Success($"[{MechanismName}] 生产状态已切换为 [{waferSize}]。");
            return true;
        }

        /// <summary>
        /// 3. 安全互锁：判断是否具备移动Z轴（升降轴）的条件
        /// 目的：防止在不安全的状态下升降导致撞机或碎片。
        /// </summary>
        public async Task<bool> CanMoveZAxesAsync(CancellationToken token = default)
        {
            await Task.CompletedTask;

            if (!IsInitialized)
            {
                _logger.Warn($"[{MechanismName}] Z轴运动检查失败：模组未初始化。");
                return false;
            }

            if (_zAxis.HasAlarm)
            {
                _logger.Warn($"[{MechanismName}] Z轴运动检查失败：Z轴处于报警状态。");
                return false;
            }

            // 【关键互锁】检查料盒是否稳固在位：如果料盒未放好强行动Z轴，会顶翻料盒
            if (_io.ReadInput(E_InPutName.上晶圆左料盒公用到位检测) != true)
            {
                _logger.Warn($"[{MechanismName}] Z轴运动检查失败：料盒未到位，禁止升降以免倾覆。");
                return false;
            }

            // 【可扩展互锁】例如：检查取料机械手是否在安全区（不能在料盒内），否则Z轴升降会切断机械手
            // if(RobotIsInsideBox) return false;

            return true;
        }

        /// <summary>
        /// 3. 安全互锁：判断是否具备动X轴（挡料轴）的条件
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

            // 【关键互锁】移动挡料机构前，晶圆夹爪必须是张开状态，否则会挤碎正在夹持的晶圆
            if (_io.ReadInput(E_InPutName.晶圆夹爪左气缸张开) != true)
            {
                _logger.Warn($"[{MechanismName}] X轴运动检查失败：夹爪未张开，禁止X轴运动以防夹碎晶圆。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 4. 寻层扫描（Mapping）：Z轴从上到下慢速移动，通过传感器记录实际有晶圆的层数
        /// 用于处理半盒料或跳层（空槽位）的情况。
        /// </summary>
        public async Task<int> SearchLayerAsync(CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 开始执行Z轴寻层扫描...");

            /* * 【注：复杂的自动化扫描逻辑实现指引】
             * 实际工程中，此处通常依赖运动控制卡底层的 "高速位置锁存 (Position Capture)" 功能：
             * 当错层传感器的信号发生上升沿跳变时，底层硬件立刻记录当前Z轴实际编码器坐标。
             * 这里提供一个概念性的占位返回逻辑，你需要接入对应框架的 mapping 功能。
             */

            int foundLayers = 0;

            // 第一步：先将Z轴抬升到料盒上方或扫描起始点
            await _zAxis.MoveToPointAsync(nameof(ZAxisPoint.待机位), token: token);

            // 第二步：触发硬件锁存或开始软件轮询扫描
            // var scanTask = _zAxis.MoveToPointAsync(nameof(ZAxisPoint.负极限位置), speed: 10.0, token: token);
            // 扫描过程中计算出有效片数...

            // 第三步：假设满盒返回
            foundLayers = _maxLayerCount;

            _logger.Success($"[{MechanismName}] 寻层扫描完成，共识别到 {foundLayers} 层有效晶圆。");
            return foundLayers;
        }

        /// <summary>
        /// 5. 切换目标层：将Z轴精准定位到计算好的指定层绝对坐标，方便后续水平取料
        /// </summary>
        /// <param name="targetLayer">目标层索引（通常从0开始，0代表第1层）</param>
        public async Task<bool> SwitchToLayerAsync(int targetLayer, CancellationToken token = default)
        {
            CheckReady();

            // 范围防呆，防止越界
            if (targetLayer < 0 || targetLayer >= _maxLayerCount)
            {
                _logger.Error($"[{MechanismName}] 目标层数 {targetLayer} 超出有效范围 (0 ~ {_maxLayerCount - 1})。");
                return false;
            }

            // 执行前置互锁检查（确保安全）
            if (!await CanMoveZAxesAsync(token)) return false;

            _logger.Info($"[{MechanismName}] 准备切换至第 {targetLayer + 1} 层...");

            // 从之前生成的缓存字典中，拉取该层对应的计算坐标
            var targetPoint = await GetZAxisMaterialPickingPosition(targetLayer, _currentWaferSize);
            if (targetPoint == null)
            {
                _logger.Error($"[{MechanismName}] 未找到第 {targetLayer + 1} 层的阵列点位，可能未执行切换生产状态操作。");
                return false;
            }

            // 驱动Z轴走到该层的绝对坐标（Position）并应用示教速度（Speed）
            bool moveResult = await MoveAbsAndWaitAsync(_zAxis, targetPoint.TargetPosition, targetPoint.Speed, targetPoint.Acc, targetPoint.Dec, targetPoint.STime, token: token);
            if (moveResult)
            {
                _logger.Success($"[{MechanismName}] 成功切换至第 {targetLayer + 1} 层位置。");
            }
            return moveResult;
        }

        /// <summary>
        /// 6. 拉料互锁：判断机构是否具备“将晶圆从料盒中平抽出来”的条件
        /// </summary>
        public async Task<bool> CanPullOutMaterialAsync(CancellationToken token = default)
        {
            await Task.CompletedTask;

            if (!IsInitialized)
            {
                _logger.Warn($"[{MechanismName}] 拉料检查失败：模组未初始化。");
                return false;
            }

            /* * 【拉料安全互锁条件示例】
             * 1. 夹爪气缸是否已经闭合？
             * 2. 夹爪传感器是否感应到晶圆（防止空拉）？
             * 3. X挡料气缸/挡料轴是否已经让开位置？
             */

            // if (_io.ReadInput(E_InPutName.夹爪铁环检测) != true)
            // {
            //     _logger.Warn($"[{MechanismName}] 拉料检查失败：夹爪内未检测到晶圆/铁环。");
            //     return false;
            // }

            return true;
        }

        /// <summary>
        /// 7. 动作阻塞等待：等待外部机构（如Y轴拉料手）完成把晶圆抽出来的动作
        /// 采用带超时限制的 `while` 轮询机制，避免传感器损坏导致的程序永久假死（Deadlock）
        /// </summary>
        public async Task<bool> WaitUntilMaterialPulledOutAsync(int timeoutMilliseconds = 5000, CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 等待物料拉出到位（最大超时设定 = {timeoutMilliseconds}ms）...");

            var timeoutTime = DateTime.Now.AddMilliseconds(timeoutMilliseconds);

            // 轮询检查传感器状态
            while (DateTime.Now < timeoutTime)
            {
                // 支持外部业务随时取消（如急停拍下时及时抛出异常中断轮询）
                token.ThrowIfCancellationRequested();

                // 假设逻辑：轨道前端传感器点亮代表晶圆已经完全拉出料盒
                // bool isPulledOut = _io.ReadInput(E_InPutName.轨道物料在位检测) == true;
                bool isPulledOut = true; // 此处用 true 占位供演示

                if (isPulledOut)
                {
                    _logger.Success($"[{MechanismName}] 物料已成功拉出。");
                    return true;
                }

                // 避免死循环占满 CPU，释放线程给其他任务，每 50 毫秒检查一次
                await Task.Delay(50, token);
            }

            // 循环结束还没 return true，说明超时报警
            _logger.Warn($"[{MechanismName}] 等待物料拉出超时（{timeoutMilliseconds}ms），请检查拉料机构是否卡料或传感器是否损坏。");
            return false;
        }

        /// <summary>
        /// 8. 动作阻塞等待：等待外部机构将晶圆退回到料盒内
        /// 逻辑同上，同样具备超时防死等机制
        /// </summary>
        public async Task<bool> WaitUntilMaterialReturnedAsync(int timeoutMilliseconds = 5000, CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 等待物料退回料盒（最大超时设定 = {timeoutMilliseconds}ms）...");

            var timeoutTime = DateTime.Now.AddMilliseconds(timeoutMilliseconds);

            while (DateTime.Now < timeoutTime)
            {
                token.ThrowIfCancellationRequested();

                // 假设逻辑：轨道传感器由亮变灭，代表晶圆离开了轨道，完全进入了料盒
                // bool isReturned = _io.ReadInput(E_InPutName.轨道物料在位检测) == false;
                bool isReturned = true; // 此处占位

                if (isReturned)
                {
                    _logger.Success($"[{MechanismName}] 物料已成功回退至料盒。");
                    return true;
                }

                await Task.Delay(50, token);
            }

            _logger.Warn($"[{MechanismName}] 等待物料回退超时（{timeoutMilliseconds}ms），请检查退料动作或传感器状态。");
            return false;
        }

        #endregion

        #region 内部辅助数学/验证方法

        /// <summary>
        /// 验证当前轴的枚举点位是否在底层配置中全部存在
        /// 防止因为漏建点位导致程序在运行中引发空引用异常（NullReferenceException）
        /// </summary>
        public async Task ConfirmEunmPoints()
        {
            if (_zAxis != null) EnsurePointsExist<ZAxisPoint>(_zAxis);
            if (_xAxis != null) EnsurePointsExist<XAxisPoint>(_xAxis);

            await Task.CompletedTask; // 满足 async 签名要求
        }

        /// <summary>
        /// 核心算法：阵列生成料盒的所有晶圆层坐标点。
        /// 通过提取“第一层”物理示教点作为基准，加上当前配置的 `LayerPitch` (层距)，推算后续所有层。
        /// 这样工程师只要示教第一层高度，其余24层高度程序自动算出。
        /// </summary>
        /// <param name="wafeSize">料盒尺寸 (决定基准点和存入哪个字典)</param>
        public async Task<Dictionary<int, AxisPoint>> ArrayZAxisMaterialPickingPosition(E_WafeSize wafeSize)
        {
            // 决定操作的目标缓存字典
            var dictToFill = wafeSize == E_WafeSize._8寸 ? PickingPosition_8 : PickingPosition_12;
            dictToFill.Clear(); // 每次计算前先清空旧数据
            var resultDict = new Dictionary<int, AxisPoint>();

            // 1. 获取第一层绝对示教基准点
            string basePointName = wafeSize == E_WafeSize._8寸 ? nameof(ZAxisPoint.层1取料位_8寸) : nameof(ZAxisPoint.层1取料位_12寸);
            var basePoint = _zAxis.PointTable.FirstOrDefault(p => p.Name == basePointName);

            // 防呆校验：如果工程师没配置第一层点位，直接报错跳出
            if (basePoint == null)
            {
                _logger.Error($"[{MechanismName}] 阵列计算失败：底层硬件配置中找不到基准点位 [{basePointName}]");
                return resultDict;
            }

            // 2. 根据 LayerPitch 累加计算所有层的位置
            // 公式：第 N 层坐标 = 基准点坐标 + (N * 间距)
            for (int i = 0; i < _maxLayerCount; i++)
            {
                var point = new AxisPoint
                {
                    Name = $"第{i + 1}层取料位",
                    TargetPosition = basePoint.TargetPosition + (i * LayerPitch), // 计算核心
                    Speed = basePoint.Speed,// 沿用第一层的运动速度
                    Acc = basePoint.Acc, // 沿用第一层的运动速度
                    Dec = basePoint.Dec, // 沿用第一层的运动速度
                    STime = basePoint.STime,// 沿用第一层的运动速度
                };

                dictToFill.TryAdd(i, point); // 存入线程安全字典供全局读取
                resultDict.Add(i, point);    // 存入局部字典用于返回值
            }

            _logger.Info($"[{MechanismName}] [{wafeSize}] 阵列点位计算完毕，共生成 {_maxLayerCount} 层坐标。");
            await Task.CompletedTask;
            return resultDict;
        }

        /// <summary>
        /// 坐标查询：根据给定的层数索引和晶圆尺寸，安全地获取对应的计算坐标
        /// </summary>
        /// <param name="index">层数索引（从0开始）</param>
        public async Task<AxisPoint> GetZAxisMaterialPickingPosition(int index, E_WafeSize wafeSize)
        {
            await Task.CompletedTask;

            // 判定要查哪个字典
            var dictToSearch = wafeSize == E_WafeSize._8寸 ? PickingPosition_8 : PickingPosition_12;

            // 尝试获取，成功则返回对应 AxisPoint
            if (dictToSearch.TryGetValue(index, out var point))
            {
                return point;
            }

            // 获取失败报警
            _logger.Error($"[{MechanismName}] 获取层点位失败：找不到尺寸 [{wafeSize}] 的第 {index + 1} 层点位数据。请确认是否已执行生产状态切换。");
            return null;
        }
        #endregion
    }
}