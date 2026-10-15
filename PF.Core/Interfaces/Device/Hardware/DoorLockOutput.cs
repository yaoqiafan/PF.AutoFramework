namespace PF.Core.Interfaces.Device.Hardware
{
    /// <summary>
    /// 安全门门锁输出点（只读）：由安全门监控统一驱动的输出口。
    /// 调试页据此识别哪些输出口不能直接手动写出。
    /// </summary>
    /// <param name="deviceId">门锁所在 IO 板卡的 DeviceId（即 IPanelIoConfig.IoDeviceId）。</param>
    /// <param name="port">输出口索引。</param>
    /// <param name="doorName">所属安全门名称，对应 IHardwareInputConfig.Name。</param>
    public sealed class DoorLockOutput(string deviceId, int port, string doorName)
    {
        /// <summary>门锁所在 IO 板卡的 DeviceId。</summary>
        public string DeviceId { get; } = deviceId;

        /// <summary>输出口索引。</summary>
        public int Port { get; } = port;

        /// <summary>所属安全门名称。</summary>
        public string DoorName { get; } = doorName;
    }
}
