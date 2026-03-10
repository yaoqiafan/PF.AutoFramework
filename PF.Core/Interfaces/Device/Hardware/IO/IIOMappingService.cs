using System;

namespace PF.Core.Interfaces.Device.Hardware.IO
{
    /// <summary>
    /// 全局 IO 别名映射服务，用于解耦业务层与通用 UI
    /// </summary>
    public interface IIOMappingService
    {
        /// <summary>
        /// 注册输入枚举（将 Enum 的 Int 值和名称映射绑定到指定设备）
        /// </summary>
        void RegisterInputEnum<TEnum>(string deviceId) where TEnum : Enum;

        /// <summary>
        /// 注册输出枚举
        /// </summary>
        void RegisterOutputEnum<TEnum>(string deviceId) where TEnum : Enum;

        /// <summary>
        /// 获取指定设备、指定引脚的 UI 显示名称
        /// </summary>
        string GetInputName(string deviceId, int portIndex);

        /// <summary>
        /// 获取指定设备、指定输出引脚的 UI 显示名称
        /// </summary>
        string GetOutputName(string deviceId, int portIndex);
    }
}
