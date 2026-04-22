using PF.Core.Constants;
using PF.Core.Entities.Hardware;
using PF.Core.Events;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;

namespace PF.Infrastructure.Mechanisms
{
    /// <summary>
    /// 模组基类，封装硬件设备管理、报警聚合、运动控制等通用逻辑
    /// </summary>
    public abstract class BaseMechanism : IMechanism, IDisposable
    {
        /// <summary>日志记录器</summary>
        protected readonly ILogService _logger;
        private readonly List<IHardwareDevice> _internalHardwares = new List<IHardwareDevice>();
        /// <summary>获取硬件管理服务</summary>
        protected IHardwareManagerService HardwareManagerService { get; }

        /// <summary>获取参数服务</summary>
        protected IParamService ParamService { get; }
        /// <summary>
        /// 模组名称
        /// </summary>
        public string MechanismName { get; }
        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized { get; protected set; }
        /// <summary>
        /// 是否存在报警
        /// </summary>
        public bool HasAlarm { get; protected set; }

        /// <summary>
        /// 暂停感知委托：由工站层注入。当轴运动等待循环检测到轴停止但未到位时调用。
        /// 若工站处于暂停状态，此委托会挂起直到恢复后返回 true；非暂停时立即返回 false。
        /// </summary>
        /// <summary>
        /// 暂停感知委托
        /// </summary>
        public Func<CancellationToken, Task<bool>>? PauseCheckAsync { get; set; }

        // 实现接口事件
        /// <summary>
        /// 模组报警事件
        /// </summary>
        public event EventHandler<MechanismAlarmEventArgs> AlarmTriggered;
        /// <summary>
        /// 模组报警自动清除事件
        /// </summary>
        public event EventHandler AlarmAutoCleared;

        // 构造函数：删除了 IEventAggregator
        /// <summary>
        /// 构造模组
        /// </summary>
        protected BaseMechanism(string name, IHardwareManagerService hardwareManagerService, IParamService paramService, ILogService logger)
        {
            MechanismName = name;
            _logger = logger;
            HardwareManagerService = hardwareManagerService;
            ParamService = paramService;
        }

        /// <summary>
        /// 拦截底层硬件自恢复事件：当所有内部硬件均清警后，清除模组报警并向上传递清警信号。
        /// </summary>
        private void OnHardwareAlarmAutoCleared(object sender, EventArgs e)
        {
            // 只有当模组确实处于报警状态，且所有内部硬件均已恢复时，才触发模组级清警
            if (!HasAlarm) return;
            if (_internalHardwares.Any(h => h.HasAlarm)) return;

            HasAlarm = false;
            _logger?.Info($"[模组 {MechanismName}] 所有内部硬件已自恢复，清除模组报警状态。");
            AlarmAutoCleared?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 拦截底层硬件报警，转换为模组报警抛出
        /// </summary>
        private void OnHardwareAlarmTriggered(object sender, DeviceAlarmEventArgs e)
        {
            var device = sender as IHardwareDevice;
            HasAlarm = true;
            //IsInitialized = false;

            _logger?.Error($"[模组 {MechanismName}] 内部硬件 [{device?.DeviceName}] 发生报警: {e.ErrorMessage}");

            // 触发标准 C# 事件，通知上层（如状态机或业务控制器）
            AlarmTriggered?.Invoke(this, new MechanismAlarmEventArgs
            {
                MechanismName = this.MechanismName,
                HardwareName = device?.DeviceName ?? "未知硬件",
                ErrorCode = e.ErrorCode,
                ErrorMessage = e.ErrorMessage,
                InternalException = e.InternalException
            });
        }

        /// <summary>
        /// 异步初始化模组
        /// </summary>
        public async Task<bool> InitializeAsync(CancellationToken token = default)
        {
            _logger?.Info($"[模组 {MechanismName}] 开始初始化...");
            HasAlarm = false;

            // 初始化前：抑制所有已注册硬件的健康监控，防止瞬态信号级联中断初始化
            foreach (var hw in _internalHardwares)
                hw.SuppressHealthMonitoring = true;

            try
            {
                IsInitialized = await InternalInitializeAsync(token);

                // InternalInitializeAsync 可能注册新硬件，确保也被抑制
                foreach (var hw in _internalHardwares)
                    hw.SuppressHealthMonitoring = true;

                if (IsInitialized)
                    _logger?.Success($"[模组 {MechanismName}] 初始化完成！");
                else
                    _logger?.Warn($"[模组 {MechanismName}] 初始化未成功完成。");

                return IsInitialized;
            }
            catch (Exception ex)
            {
                HasAlarm = true;
                _logger?.Error($"[模组 {MechanismName}] 初始化过程发生异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 异步复位模组并清除报警
        /// </summary>
        public async Task<bool> ResetAsync(CancellationToken token = default)
        {
            _logger?.Info($"[模组 {MechanismName}] 正在复位清除报警...");

            bool allResetOk = true;
            foreach (var hw in _internalHardwares)
            {
                if (!await hw.ResetAsync(token))
                    allResetOk = false;
            }

            if (allResetOk)
                allResetOk = await InternalResetAsync(token);

            if (allResetOk)
            {
                HasAlarm = false;
                //IsInitialized = false; // 复位后需要重新初始化，不应标记为已初始化
            }
            return allResetOk;
        }

        /// <summary>
        /// 级联清除模组内所有硬件的报警标志（不执行回原点）。
        /// 由 BaseMasterController.OnHardwareResetRequested 在 AlarmService.ClearAlarm 后调用。
        /// </summary>
        public virtual async Task<bool> ResetHardwareAlarmAsync(CancellationToken token = default)
        {
            bool allOk = true;
            foreach (var hw in _internalHardwares)
            {
                if (!await hw.ResetHardwareAlarmAsync(token).ConfigureAwait(false))
                    allOk = false;
            }

            if (allOk)
            {
                HasAlarm = false;
                //IsInitialized = false; // 清警后需要重新初始化
            }
            return allOk;
        }

        /// <summary>
        /// 恢复所有内部硬件的健康监控（由工站在初始化完成、回零成功后调用）
        /// </summary>
        public void ResumeHealthMonitoring()
        {
            foreach (var hw in _internalHardwares)
                hw.SuppressHealthMonitoring = false;
        }

        /// <summary>
        /// 紧急停止模组
        /// </summary>
        public async Task StopAsync()
        {
            _logger?.Warn($"[模组 {MechanismName}] 触发紧急停止！");
            await InternalStopAsync();
        }

        /// <summary>内部初始化异步操作</summary>
        protected abstract Task<bool> InternalInitializeAsync(CancellationToken token);
        /// <summary>内部停止异步操作</summary>
        protected abstract Task InternalStopAsync();
        /// <summary>内部复位异步操作</summary>
        protected virtual Task<bool> InternalResetAsync(CancellationToken token) => Task.FromResult(true);

        /// <summary>检查机构是否就绪</summary>
        protected void CheckReady()
        {
            if (HasAlarm) throw new Exception($"模组 [{MechanismName}] 处于报警状态，禁止动作！");
            if (!IsInitialized) throw new Exception($"模组 [{MechanismName}] 未初始化，禁止动作！");
        }

        /// <summary>
        /// 在 InternalInitializeAsync 中延迟注册硬件设备（代理委托模式专用）。
        ///
        /// 用途：当子类通过 IHardwareManagerService 延迟解析设备（而非构造函数注入）时，
        ///   在解析到设备实例后调用本方法，将其纳入模组的：
        ///   · 报警事件聚合（AlarmTriggered 自动冒泡至模组）
        ///   · 批量复位列表（ResetAsync 遍历所有内部硬件）
        ///
        /// 重复注册保护：同一实例不会被重复添加。
        /// </summary>
        protected void RegisterHardwareDevice(IHardwareDevice device)
        {
            if (device == null || _internalHardwares.Contains(device)) return;
            _internalHardwares.Add(device);
            device.AlarmTriggered += OnHardwareAlarmTriggered;
            device.HardwareAlarmAutoCleared += OnHardwareAlarmAutoCleared;
        }

        /// <summary>
        /// 释放模组资源
        /// </summary>
        public virtual void Dispose()
        {
            foreach (var hw in _internalHardwares)
            {
                if (hw == null) continue;
                hw.AlarmTriggered -= OnHardwareAlarmTriggered;
                hw.HardwareAlarmAutoCleared -= OnHardwareAlarmAutoCleared;
                // 不在此处 Dispose 硬件实例：硬件生命周期由 HardwareManagerService 统一管理，
                // 模组提前 Dispose 会导致共享该硬件的其他模组出现 ObjectDisposedException。
            }
        }










        /// <summary>
        /// 等待轴运动完成（轮询 AxisIOStatus.MoveDone）。
        /// 自动处理模拟模式（IsSimulated 时直接返回 true）。
        /// </summary>
        /// <param name="axis">目标轴</param>
        /// <param name="timeoutMs">超时毫秒数，默认 30 秒</param>
        /// <param name="token">取消令牌</param>
        public async Task<bool> WaitAxisMoveDoneAsync(IAxis axis, int timeoutMs = 30_000, CancellationToken token = default)
        {
            // 修复 2：前置非空校验，尽早暴露错误
            ArgumentNullException.ThrowIfNull(axis);

            // 模拟模式：MoveXxxAsync 内部已做 Task.Delay，直接视为完成
            if ((axis as IHardwareDevice)?.IsSimulated == true)
                return true;

            var axisName = (axis as IHardwareDevice)?.DeviceName ?? "未知轴";

            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

            // P6 修复：将报警事件参数暂存到局部变量，在 catch 外部触发事件，
            // 避免在 catch 块中同步调用事件链导致 _stateLock 等待延迟报警响应。
            MechanismAlarmEventArgs? pendingAlarm = null;

            try
            {
                while (true)
                {
                    await Task.Delay(50, linked.Token).ConfigureAwait(false);

                    var status = axis.AxisIOStatus;
                    if (status != null && status.MoveDone && !status.Moving)
                    {
                        _logger?.Info($"[{MechanismName}] 轴 [{axisName}] 运动完成");
                        return true;
                    }

                    // 轴已停止但未到达目标（被工站暂停指令减速停止）
                    if (status != null && !status.MoveDone && !status.Moving && PauseCheckAsync != null)
                    {
                        bool wasPaused = await PauseCheckAsync(linked.Token).ConfigureAwait(false);
                        if (wasPaused)
                            return false;  // 暂停恢复后信号：由调用方重新发起运动
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 修复 1：优先判断是否是外部主动取消。如果是，将异常继续往上抛！
                if (token.IsCancellationRequested)
                {
                    _logger?.Warn($"[{MechanismName}] 轴 [{axisName}] 等待被外部手动取消");
                    token.ThrowIfCancellationRequested();
                }

                // 走到这里，说明外部没有取消，纯粹是 timeoutCts 触发的超时
                if (timeoutCts.IsCancellationRequested)
                {
                    // 先物理制动：轴仍在运动中，必须立即停止，否则有撞机风险。
                    // 此时外部 token 尚未取消，可安全用于制动指令。
                    await axis.StopAsync(token);

                    HasAlarm = true;
                    _logger?.Error($"[{MechanismName}] 轴 [{axisName}] 等待运动完成超时（{timeoutMs} ms）");
                    // 暂存报警参数，在 catch 外部触发事件
                    pendingAlarm = new MechanismAlarmEventArgs
                    {
                        MechanismName = this.MechanismName,
                        HardwareName = axisName,
                        ErrorCode = AlarmCodes.Hardware.AxisMoveTimeout,
                        ErrorMessage = $"等待轴运动完成超时（{timeoutMs} ms）"
                    };
                }
            }

            // 在 catch 外部触发报警事件，避免在异常处理上下文中同步调用事件链
            // （事件链会进入 StationBase.RaiseAlarm → Fire(Error) → 获取 _stateLock）
            if (pendingAlarm != null)
                AlarmTriggered?.Invoke(this, pendingAlarm);

            return false;
        }



        /// <summary>
        /// 等待轴回原点完成
        /// </summary>
        public  async Task<bool> WaitHomeDoneAsync(IAxis axis, int timeoutMs = 30_000, CancellationToken token = default)
        {
            if (axis == null) return false;

            // 模拟模式：MoveXxxAsync 内部已做 Task.Delay，直接视为完成
            if ((axis as IHardwareDevice)?.IsSimulated == true)
                return true;
            var axisName = (axis as IHardwareDevice)?.DeviceName ?? "未知轴";



            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

            if (!await axis.HomeAsync(token))
            {

                return false;
            }

            // P6 修复：暂存报警参数，在 catch 外部触发事件
            MechanismAlarmEventArgs? pendingAlarm = null;

            try
            {
                while (true)
                {
                    await Task.Delay(10, linked.Token).ConfigureAwait(false);
                    var status = axis.AxisIOStatus;
                    if (status != null && status.HomeDone && !status.Homing)
                    {
                        _logger?.Info($"[{MechanismName}] 轴 [{axisName}] 回原点完成");
                        return true;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts.IsCancellationRequested)
                {
                    HasAlarm = true;
                    _logger?.Error($"[{MechanismName}] 轴 [{axisName}] 等待回零完成超时（{timeoutMs} ms）");
                    pendingAlarm = new MechanismAlarmEventArgs
                    {
                        MechanismName = this.MechanismName,
                        HardwareName = axisName,
                        ErrorCode = AlarmCodes.Hardware.HomingTimeout,
                        ErrorMessage = $"等待回零完成超时（{timeoutMs} ms）"
                    };
                }
            }

            if (pendingAlarm != null)
                AlarmTriggered?.Invoke(this, pendingAlarm);

            return false;
        }

        /// <summary>
        /// 绝对位置移动并等待完成（组合方法）。
        /// </summary>
        protected async Task<bool> MoveAbsAndWaitAsync(
            IAxis axis, double position, double velocity,
            double acc, double dec, double sTime,
            int timeoutMs = 30_000, CancellationToken token = default)
        {
            var axisName = (axis as IHardwareDevice)?.DeviceName ?? "未知轴";
            _logger?.Info($"[{MechanismName}] 轴 [{axisName}] 绝对移动 → {position:F2} mm @ {velocity} mm/s");

            if (!await axis.MoveAbsoluteAsync(position, velocity, acc, dec, sTime, token).ConfigureAwait(false))
                return false;

            return await WaitAxisMoveDoneAsync(axis, timeoutMs, token).ConfigureAwait(false);
        }

        /// <summary>
        /// 相对位置移动并等待完成（组合方法）。
        /// </summary>
        protected async Task<bool> MoveRelAndWaitAsync(
            IAxis axis, double distance, double velocity,
            double acc, double dec, double sTime,
            int timeoutMs = 30_000, CancellationToken token = default)
        {
            var axisName = (axis as IHardwareDevice)?.DeviceName ?? "未知轴";
            _logger?.Info($"[{MechanismName}] 轴 [{axisName}] 相对移动 {distance:+0.##;-0.##} mm @ {velocity} mm/s");

            if (!await axis.MoveRelativeAsync(distance, velocity, acc, dec, sTime, token).ConfigureAwait(false))
                return false;

            return await WaitAxisMoveDoneAsync(axis, timeoutMs, token).ConfigureAwait(false);
        }

        /// <summary>
        /// 按点表名称移动并等待完成（组合方法，速度/加速度从点表读取）。
        /// </summary>
        public  async Task<bool> MoveToPointAndWaitAsync(
            IAxis axis, string pointName,
            int timeoutMs = 30_000, CancellationToken token = default)
        {
            var axisName = (axis as IHardwareDevice)?.DeviceName ?? "未知轴";
            _logger?.Info($"[{MechanismName}] 轴 [{axisName}] 移动到点位 [{pointName}]");

            if (!await axis.MoveToPointAsync(pointName, token).ConfigureAwait(false))
                return false;

            bool success = await WaitAxisMoveDoneAsync(axis, timeoutMs, token).ConfigureAwait(false);
            if (success) return true;

            // 运动未完成且存在暂停感知 → 暂停恢复后重新发起运动
            if (PauseCheckAsync != null && !token.IsCancellationRequested)
            {
                _logger?.Info($"[{MechanismName}] 轴 [{axisName}] 暂停恢复，重新移动到 [{pointName}]");
                if (!await axis.MoveToPointAsync(pointName, token).ConfigureAwait(false))
                    return false;
                return await WaitAxisMoveDoneAsync(axis, timeoutMs, token).ConfigureAwait(false);
            }

            return false;
        }

        /// <summary>
        /// 并发移动多个轴到各自指定点位，等待所有轴全部到位（Task.WhenAll 模式）。
        ///
        /// 用法示例：
        ///   await MoveMultiAxesToPointsAsync(new[]
        ///   {
        ///       (_xAxis, nameof(XPoints.PickAbove)),
        ///       (_yAxis, nameof(YPoints.PickAbove)),
        ///   }, token: token);
        ///
        /// 注意：所有轴同时启动运动，适用于轴间无机械干涉的场景。
        ///   若存在干涉风险，请分步调用 MoveToPointAndWaitAsync。
        /// </summary>
        /// <param name="moves">轴-点位对集合</param>
        /// <param name="timeoutMs">等待单轴到位的超时毫秒数，默认 30 秒</param>
        /// <param name="token">取消令牌</param>
        public async Task<bool>  MoveMultiAxesToPointsAsync(
            IEnumerable<(IAxis axis, string pointName)> moves,
            int timeoutMs = 30_000,
            CancellationToken token = default)
        {
            var moveList = moves.ToList();
            if (moveList.Count == 0) return true;

            // 1. 并发发出所有轴的运动指令
            var startTasks = moveList.Select(m =>
            {
                var axisName = (m.axis as IHardwareDevice)?.DeviceName ?? "未知轴";
                _logger?.Info($"[{MechanismName}] 轴 [{axisName}] 移动到点位 [{m.pointName}]（多轴并发）");
                return m.axis.MoveToPointAsync(m.pointName, token);
            }).ToList();

            bool[] startResults = await Task.WhenAll(startTasks).ConfigureAwait(false);
            if (startResults.Any(r => !r))
            {
                _logger?.Error($"[{MechanismName}] 多轴并发移动：部分轴指令发送失败");
                return false;
            }

            // 2. 并发等待所有轴到位
            var waitTasks = moveList
                .Select(m => WaitAxisMoveDoneAsync(m.axis, timeoutMs, token))
                .ToList();

            bool[] waitResults = await Task.WhenAll(waitTasks).ConfigureAwait(false);
            if (waitResults.Any(r => !r))
            {
                _logger?.Error($"[{MechanismName}] 多轴并发移动：部分轴未在超时内完成（{timeoutMs} ms）");
                return false;
            }

            _logger?.Info($"[{MechanismName}] 多轴并发移动完成（共 {moveList.Count} 轴）");
            return true;
        }


        /// <summary>
        /// 通用泛型方法：校验并补齐指定轴的点位
        /// </summary>
        /// <typeparam name="TEnum">点位枚举类型</typeparam>
        /// <param name="axis">目标轴实例</param>
        /// <summary>
        /// 校验并补齐指定轴的点位
        /// </summary>
        public void EnsurePointsExist<TEnum>(IAxis axis) where TEnum : struct, Enum
        {
            bool isModified = false;

            // 1. 将当前轴已有的点位名称提取成 HashSet，查询速度 O(1)
            // 假设你的 AxisPoint 实体类中包含了 Name 属性 (通常点表都会有名字)
            HashSet<string> existingPointNames = new HashSet<string>(axis.PointTable.Select(p => p.Name));
            int index = 0;
            // 2. 遍历枚举中的所有定义
            foreach (TEnum enumValue in Enum.GetValues(typeof(TEnum)))
            {
                string expectedPointName = enumValue.ToString();

                // 3. 检查是否缺失
                if (!existingPointNames.Contains(expectedPointName))
                {

                    AxisPoint newPoint = new AxisPoint
                    {
                        Name = expectedPointName,
                        SortOrder = index++,
                    };

                    // 4. 添加到内存点表
                    axis.AddOrUpdatePoint(newPoint);
                    isModified = true;
                }
            }

            // 5. 如果发生了任何新增，触发一次持久化保存
            if (isModified)
            {
                axis.SavePointTable();
            }
        }
    }
}
