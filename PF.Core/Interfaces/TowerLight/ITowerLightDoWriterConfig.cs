namespace PF.Core.Interfaces.TowerLight
{
    /// <summary>
    /// 三色灯 DO 点映射配置接口：将逻辑 tag（如 "Red"）解析为物理 IO 板卡端口号。
    /// 由 PF.WorkStation.AutoOcr.CostParam 层提供具体实现，实现依赖倒置。
    /// </summary>
    public interface ITowerLightDoWriterConfig
    {
        /// <summary>绑定的 IO 板卡 DeviceId，用于从 IHardwareManagerService 解析设备实例</summary>
        string IoDeviceId { get; }

        /// <summary>
        /// 通过逻辑 tag 获取对应的端口索引。
        /// tag 未配置时返回 -1，调用方应跳过本次写入。
        /// </summary>
        int GetPort(string tag);
    }
}
