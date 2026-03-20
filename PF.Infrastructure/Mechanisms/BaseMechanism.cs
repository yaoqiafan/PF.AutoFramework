using PF.Core.Entities.Hardware;
using PF.Core.Events;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;

namespace PF.Infrastructure.Mechanisms
{
    public abstract class BaseMechanism : IMechanism, IDisposable
    {
        protected readonly ILogService _logger;
        private readonly List<IHardwareDevice> _internalHardwares= new List<IHardwareDevice>();
        protected IHardwareManagerService HardwareManagerService { get; }

        protected IParamService ParamService { get; }
        public string MechanismName { get; }
        public bool IsInitialized { get; protected set; }
        public bool HasAlarm { get; protected set; }

        // 实现接口事件
        public event EventHandler<MechanismAlarmEventArgs> AlarmTriggered;

        // 构造函数：删除了 IEventAggregator
        protected BaseMechanism(string name, IHardwareManagerService hardwareManagerService, IParamService paramService, ILogService logger)
        {
            MechanismName = name;
            _logger = logger;
            HardwareManagerService = hardwareManagerService;
            ParamService = paramService;
        }

        /// <summary>
        /// 拦截底层硬件报警，转换为模组报警抛出
        /// </summary>
        private void OnHardwareAlarmTriggered(object sender, DeviceAlarmEventArgs e)
        {
            var device = sender as IHardwareDevice;
            HasAlarm = true;
            IsInitialized = false;

            _logger?.Error($"[模组 {MechanismName}] 内部硬件 [{device?.DeviceName}] 发生报警: {e.ErrorMessage}");

            // 触发标准 C# 事件，通知上层（如状态机或业务控制器）
            AlarmTriggered?.Invoke(this, new MechanismAlarmEventArgs
            {
                MechanismName = this.MechanismName,
                HardwareName = device?.DeviceName ?? "未知硬件",
                ErrorMessage = e.ErrorMessage,
                InternalException = e.InternalException
            });
        }

        public async Task<bool> InitializeAsync(CancellationToken token = default)
        {
            _logger?.Info($"[模组 {MechanismName}] 开始初始化...");
            HasAlarm = false;

            try
            {
                IsInitialized = await InternalInitializeAsync(token);
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

        public async Task<bool> ResetAsync(CancellationToken token = default)
        {
            _logger?.Info($"[模组 {MechanismName}] 正在复位清除报警...");

            bool allResetOk = true;
            foreach (var hw in _internalHardwares)
            {
                if (!await hw.ResetAsync(token))
                {
                    allResetOk = false;
                }
            }

            if (allResetOk)
            {
                allResetOk = await InternalResetAsync(token);
            }

            if (allResetOk) HasAlarm = false;
            return allResetOk;
        }

        public async Task StopAsync()
        {
            _logger?.Warn($"[模组 {MechanismName}] 触发紧急停止！");
            await InternalStopAsync();
        }

        protected abstract Task<bool> InternalInitializeAsync(CancellationToken token);
        protected abstract Task InternalStopAsync();
        protected virtual Task<bool> InternalResetAsync(CancellationToken token) => Task.FromResult(true);

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
        }

        public virtual void Dispose()
        {
            foreach (var hw in _internalHardwares)
            {
                if (hw != null) hw.AlarmTriggered -= OnHardwareAlarmTriggered;
            }
        }







        


        public async Task<bool> WaitAxisMoveDone(IAxis axis, CancellationToken token)
        {
            try
            {
                Task a = Task.Run(async () =>
                {
                    while (true)
                    {
                        await Task.Delay(10, token);
                        if (axis.AxisIOStatus.MoveDone && !axis.AxisIOStatus.Moving)
                        {
                            return true;
                        }
                    }
                });
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }


        /// <summary>
        /// 通用泛型方法：校验并补齐指定轴的点位
        /// </summary>
        /// <typeparam name="TEnum">点位枚举类型</typeparam>
        /// <param name="axis">目标轴实例</param>
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
