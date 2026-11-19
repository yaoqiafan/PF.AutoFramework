using PF.Core.Constants;
using PF.Core.Entities.Hardware;
using PF.Core.Enums.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO.Analog;
using PF.Core.Models.Device.Hardware.IO.Analog;
using PF.Infrastructure.Hardware.IO.Analog;
using System.ComponentModel;
using System.Reflection;
using PF.Core.Events;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Logging;

namespace PF.Infrastructure.Mechanisms
{
    /// <summary>
    /// 模组基类，封装硬件设备管理、报警聚合、运动控制等通用逻辑
    /// </summary>
    /// <remarks>
    /// 构造模组
    /// </remarks>
    public abstract class BaseMechanism(string name, IHardwareManagerService hardwareManagerService, IParamService paramService, ILogService logger) : IMechanism, IDisposable
    {
        /// <summary>日志记录器</summary>
        protected readonly ILogService _logger = logger;

        /// <summary>
        /// 硬件分类日志记录器（自动包装 <see cref="_logger"/> 为 Hardware 分类）。
        /// 派生类应优先使用此 Logger 记录所有机构/硬件相关日志。
        /// </summary>
        protected CategoryLogger HardwareLogger =>
            _hardwareLogger ??= CategoryLoggerFactory.Hardware(_logger);
        private CategoryLogger _hardwareLogger;
        private readonly List<IHardwareDevice> _internalHardwares = [];
        // _internalHardwares 并发保护锁：注册发生在初始化线程，报警/清警回调来自设备健康监控线程
        private readonly object _hardwaresLock = new();
        /// <summary>获取硬件管理服务</summary>
        protected IHardwareManagerService HardwareManagerService { get; } = hardwareManagerService;

        /// <summary>获取参数服务</summary>
        protected IParamService ParamService { get; } = paramService;
        /// <summary>
        /// 模组名称
        /// </summary>
        public string MechanismName { get; } = name;
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

        /// <summary>
        /// 拦截底层硬件自恢复事件：当所有内部硬件均清警后，清除模组报警并向上传递清警信号。
        /// </summary>
        private void OnHardwareAlarmAutoCleared(object sender, EventArgs e)
        {
            // 只有当模组确实处于报警状态，且所有内部硬件均已恢复时，才触发模组级清警
            if (!HasAlarm) return;
            lock (_hardwaresLock)
            {
                if (_internalHardwares.Any(h => h.HasAlarm)) return;
            }

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

            // 初始化前：抑制所有已注册硬件的健康监控，防止瞬态信号级联中断初始化。
            // 成功时保持抑制，由工站在回零等后续动作完成后调用 ResumeHealthMonitoring（StationBase 初始化结束时也会兜底恢复）
            SuppressHealthMonitoring();

            try
            {
                IsInitialized = await InternalInitializeAsync(token);

                // InternalInitializeAsync 可能注册新硬件，确保也被抑制
                SuppressHealthMonitoring();

                if (IsInitialized)
                {
                    _logger?.Success($"[模组 {MechanismName}] 初始化完成！");
                }
                else
                {
                    _logger?.Warn($"[模组 {MechanismName}] 初始化未成功完成。");
                    // 失败后不会再有回零等后续动作需要保护；不恢复的话这些设备之后掉线也不会报警
                    ResumeHealthMonitoring();
                }

                return IsInitialized;
            }
            catch (Exception ex)
            {
                HasAlarm = true;
                // 重初始化中途异常时必须清掉旧的已初始化标记，否则复位清警后 CheckReady 会放行半初始化的机构
                IsInitialized = false;
                _logger?.Error($"[模组 {MechanismName}] 初始化过程发生异常: {ex.Message}");
                ResumeHealthMonitoring();
                return false;
            }
        }

        /// <summary>
        /// 单独初始化（调试页手动初始化模组用）：初始化后立即恢复健康监控。
        /// <para>工站流程里应调用 <see cref="InitializeAsync"/>，回零完成后再恢复；
        /// 调试页没有后续流程，若也调用 InitializeAsync，这些设备的健康监控会一直处于抑制状态。</para>
        /// </summary>
        public async Task<bool> InitializeStandaloneAsync(CancellationToken token = default)
        {
            try { return await InitializeAsync(token).ConfigureAwait(false); }
            finally { ResumeHealthMonitoring(); }
        }

        private void SuppressHealthMonitoring()
        {
            foreach (var hw in SnapshotHardwares())
                SetSuppressed(hw, true);
        }

        // 以本模组为持有者抑制，多个模组共用同一设备时互不干扰（见 BaseDevice.SetHealthMonitoringSuppressed）
        private void SetSuppressed(IHardwareDevice hw, bool suppressed)
        {
            if (hw is Hardware.BaseDevice device) device.SetHealthMonitoringSuppressed(this, suppressed);
            else hw.SuppressHealthMonitoring = suppressed;
        }

        /// <summary>
        /// 异步复位模组并清除报警
        /// </summary>
        public async Task<bool> ResetAsync(CancellationToken token = default)
        {
            _logger?.Info($"[模组 {MechanismName}] 正在复位清除报警...");

            bool allResetOk = true;
            foreach (var hw in SnapshotHardwares())
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
            foreach (var hw in SnapshotHardwares())
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
            foreach (var hw in SnapshotHardwares())
                SetSuppressed(hw, false);
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

        /// <summary>模组是否在安全位置（子类覆写，返回所有关键轴是否在待机点）</summary>
        public virtual bool IsInSafePosition => false;

        /// <summary>检查指定轴是否在目标点位（含容差），轴为 null 时返回 true</summary>
        protected static bool IsAxisAtPoint(IAxis? axis, string pointName, double tolerance = 0.5)
        {
            if (axis == null) return true;
            var pos = axis.CurrentPosition;
            if (pos == null) return false;
            var point = axis.PointTable.FirstOrDefault(p => p.Name == pointName);
            return point != null && Math.Abs(pos.Value - point.TargetPosition) <= tolerance;
        }

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
            if (device == null) return;
            IHardwareDevice? stale = null;
            lock (_hardwaresLock)
            {
                if (_internalHardwares.Contains(device)) return;

                // 硬件热重载后同一 DeviceId 是新实例：替换掉旧实例。此前旧实例一直留在列表里，
                // 它残留的 HasAlarm 让模组永远无法自动清警，复位也会作用到已释放的设备上
                stale = _internalHardwares.FirstOrDefault(h => h.DeviceId == device.DeviceId);
                if (stale != null) _internalHardwares.Remove(stale);

                _internalHardwares.Add(device);
            }
            if (stale != null)
            {
                stale.AlarmTriggered -= OnHardwareAlarmTriggered;
                stale.HardwareAlarmAutoCleared -= OnHardwareAlarmAutoCleared;
                SetSuppressed(stale, false);
            }
            device.AlarmTriggered += OnHardwareAlarmTriggered;
            device.HardwareAlarmAutoCleared += OnHardwareAlarmAutoCleared;
        }

        /// <summary>锁内复制内部硬件列表快照，供含 await 的遍历在锁外安全迭代。</summary>
        private List<IHardwareDevice> SnapshotHardwares()
        {
            lock (_hardwaresLock) { return [.. _internalHardwares]; }
        }

        /// <summary>
        /// 释放模组资源
        /// </summary>
        public virtual void Dispose()
        {
            foreach (var hw in SnapshotHardwares())
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
        /// <param name="pose">运动点位</param>
        /// <param name="token">取消令牌</param>
        public async Task<bool> WaitAxisMoveDoneAsync(IAxis axis, int timeoutMs = 30_000,double? pose = null, CancellationToken token = default)
        {
            // 修复 2：前置非空校验，尽早暴露错误
            ArgumentNullException.ThrowIfNull(axis);

            // 模拟模式：MoveXxxAsync 内部已做 Task.Delay，直接视为完成
            if (axis is IHardwareDevice { IsSimulated: true })
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
                        var currentpose = axis.CurrentPosition;

                        // 定位精度未配置（默认 0）时不校验：此前按"误差 > 0"判超限，真机上几乎每次绝对运动都会误报
                        if (pose.HasValue && axis.Param.PositioningAccuracy > 0)
                        {
                            if (currentpose.HasValue)
                            {
                                var abspose = Math.Abs(pose.Value - currentpose.Value);
                                if (abspose > axis.Param.PositioningAccuracy)
                                {
                                    HasAlarm = true;
                                    _logger?.Error($"[{MechanismName}] 轴 [{axisName}] 运动误差超限（{abspose:F3} > {axis.Param.PositioningAccuracy}）");
                                    pendingAlarm = new MechanismAlarmEventArgs
                                    {
                                        MechanismName = this.MechanismName,
                                        HardwareName = axisName,
                                        ErrorCode = AlarmCodes.Hardware.AxisMoveInaccuratePositioning,
                                        ErrorMessage = $"轴 [{axisName}] 运动误差超限（{abspose:F3} > {axis.Param.PositioningAccuracy}）"
                                    };
                                    break;
                                }

                            }
                            else
                            {
                                HasAlarm = true;
                                _logger?.Error($"[{MechanismName}] 轴 [{axisName}] 获取当前位置失败，无法校验定位精度");
                                pendingAlarm = new MechanismAlarmEventArgs
                                {
                                    MechanismName = this.MechanismName,
                                    HardwareName = axisName,
                                    ErrorCode = AlarmCodes.Hardware.AxisGetCurrentPositionFailed,
                                    ErrorMessage = $"轴 [{axisName}] 获取当前位置失败，无法校验定位精度"
                                };
                                break;
                            }

                        }

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
                    throw;
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
            if (axis is IHardwareDevice { IsSimulated: true })
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
                // 优先判断是否是外部主动取消。如果是，将异常继续往上抛（对齐 WaitAxisMoveDoneAsync 的处理）。
                if (token.IsCancellationRequested)
                {
                    _logger?.Warn($"[{MechanismName}] 轴 [{axisName}] 等待回零被外部手动取消");
                    token.ThrowIfCancellationRequested();
                    throw;
                }

                // 走到这里，说明外部没有取消，纯粹是 timeoutCts 触发的超时。
                // 先物理制动：回零超时后轴可能仍在运动（回零模式），必须立即停止，否则有撞机/超程风险。
                // 此时外部 token 尚未取消，可安全用于制动指令。
                if (timeoutCts.IsCancellationRequested)
                {
                    await axis.StopAsync(token);

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

            return await WaitAxisMoveDoneAsync(axis, timeoutMs, position, token).ConfigureAwait(false);
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

            return await WaitAxisMoveDoneAsync(axis, timeoutMs, token: token).ConfigureAwait(false);
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

            // 与 MoveAbsAndWaitAsync 一致做到位精度校验（轴参数 PositioningAccuracy > 0 时生效）。
            // 此前点位运动这条最常用的路径不传目标位置，从不校验是否真的到位
            double? target = axis.PointTable.FirstOrDefault(p => p.Name == pointName)?.TargetPosition;

            bool success = await WaitAxisMoveDoneAsync(axis, timeoutMs, target, token).ConfigureAwait(false);
            if (success) return true;

            // 运动未完成且存在暂停感知 → 暂停恢复后重新发起运动
            if (PauseCheckAsync != null && !token.IsCancellationRequested)
            {
                _logger?.Info($"[{MechanismName}] 轴 [{axisName}] 暂停恢复，重新移动到 [{pointName}]");
                if (!await axis.MoveToPointAsync(pointName, token).ConfigureAwait(false))
                    return false;
                return await WaitAxisMoveDoneAsync(axis, timeoutMs, target, token).ConfigureAwait(false);
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
                .Select(m => WaitAxisMoveDoneAsync(m.axis, timeoutMs, token: token))
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
        public static  void EnsurePointsExist<TEnum>(IAxis axis) where TEnum : struct, Enum
        {
            bool isModified = false;

            // 1. 将当前轴已有的点位名称提取成 HashSet，查询速度 O(1)
            // 假设你的 AxisPoint 实体类中包含了 Name 属性 (通常点表都会有名字)
            HashSet<string> existingPointNames = [.. axis.PointTable.Select(p => p.Name)];
            int index = 0;
            // 2. 遍历枚举中的所有定义
            foreach (TEnum enumValue in Enum.GetValues(typeof(TEnum)))
            {
                string expectedPointName = enumValue.ToString();

                // 3. 检查是否缺失
                if (!existingPointNames.Contains(expectedPointName))
                {

                    AxisPoint newPoint = new()
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

        /// <summary>
        /// 确保模拟量设备的通道表包含两个业务枚举定义的全部通道（与 <see cref="EnsurePointsExist{TEnum}"/> 同一思路）。
        /// <para>缺失的 <c>(方向, 通道号)</c> 按默认值补一行：名称取 [Description]（无则取枚举名），
        /// 0~10V 对应 0~10、仿真模式为【等待直接通过】；已有的行一律不动。
        /// 有新增时写回 <c>HardwareConfig.ConnectionParameters["Channels"]</c> 并持久化，再热更新到设备。</para>
        /// <para>与点表不同，通道表存在 HardwareConfig 里，必须经 <see cref="HardwareManagerService"/> 保存，
        /// 因此是实例方法；在 <c>InternalInitializeAsync</c> 末尾调用。</para>
        /// </summary>
        /// <typeparam name="TIn">AI 枚举（int 值 = AI 通道号）</typeparam>
        /// <typeparam name="TOut">AO 枚举（int 值 = AO 通道号）</typeparam>
        protected async Task EnsureAnalogChannelsAsync<TIn, TOut>(IAnalogIOController device)
            where TIn : struct, Enum
            where TOut : struct, Enum
        {
            ArgumentNullException.ThrowIfNull(device);

            var rows = device.InputChannels.Concat(device.OutputChannels).ToList();
            var keys = rows.Select(r => (r.Direction, r.Channel)).ToHashSet();
            int added = 0;

            void AddMissing<TEnum>(AnalogDirection direction) where TEnum : struct, Enum
            {
                foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
                {
                    int channel = Convert.ToInt32(value);
                    if (!keys.Add((direction, channel))) continue;

                    string name = typeof(TEnum).GetField(value.ToString())?
                        .GetCustomAttribute<DescriptionAttribute>()?.Description ?? value.ToString();
                    var row = new AnalogChannelConfig
                    {
                        Direction = direction,
                        Channel = channel,
                        Name = name,
                        SignalType = AnalogSignalType.Voltage0To10,
                        EngMin = 0,
                        EngMax = 10,
                        OutputMin = 0,
                        OutputMax = 10,
                        SimMode = AnalogSimMode.AlwaysPass,
                    };
                    row.ApplySignalTypeDefaults();
                    rows.Add(row);
                    added++;
                }
            }

            AddMissing<TIn>(AnalogDirection.Input);
            AddMissing<TOut>(AnalogDirection.Output);
            if (added == 0) return;

            var result = device.UpdateChannels(rows);
            if (!result.Success)
            {
                HardwareLogger.Error($"[{MechanismName}] 补全模拟量设备 [{device.DeviceName}] 通道表失败：{string.Join("；", result.Errors)}");
                return;
            }

            var config = HardwareManagerService.GetConfig(device.DeviceId);
            if (config == null)
            {
                HardwareLogger.Warn($"[{MechanismName}] 未找到模拟量设备 [{device.DeviceId}] 的硬件配置，补全的 {added} 个通道只在内存中生效");
                return;
            }

            config.ConnectionParameters[AnalogChannelJson.ChannelsKey] = AnalogChannelJson.Serialize(rows);
            await HardwareManagerService.SaveConfigAsync(config);
            HardwareLogger.Info($"[{MechanismName}] 已为模拟量设备 [{device.DeviceName}] 补全 {added} 个通道并保存");
        }
    }
}
