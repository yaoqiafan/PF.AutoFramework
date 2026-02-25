namespace PF.Core.Interfaces.Device.Hardware.Card
{
    /// <summary>
    /// 运动控制卡接口
    ///
    /// 继承链：ConcreteCard → IMotionCard → IHardwareDevice
    ///
    /// 代表一块物理运动控制板卡，轴设备和IO设备均挂载于其上。
    /// 板卡负责初始化底层驱动和加载硬件参数配置文件，
    /// 子设备（IAxis / IIOController）通过 IAttachedDevice 接口引用其父板卡。
    /// </summary>
    public interface IMotionCard : IHardwareDevice
    {
        /// <summary>板卡在系统中的物理槽位/索引号（如 0, 1, 2…）</summary>
        int CardIndex { get; }

        /// <summary>该板卡支持的运动控制轴总数</summary>
        int AxisCount { get; }

        /// <summary>该板卡数字量输入端口总数</summary>
        int InputCount { get; }

        /// <summary>该板卡数字量输出端口总数</summary>
        int OutputCount { get; }

        /// <summary>
        /// 加载板卡专属硬件参数配置文件（如运动参数 INI / XML 文件）
        /// </summary>
        /// <param name="configFilePath">配置文件绝对路径</param>
        /// <returns>加载成功返回 true，否则返回 false</returns>
        Task<bool> LoadConfigAsync(string configFilePath);
    }
}
