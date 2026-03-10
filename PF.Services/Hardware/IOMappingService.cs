using PF.Core.Interfaces.Device.Hardware.IO;
using PF.Core.Models.Device.Hardware.IO;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace PF.Services.Hardware
{
    /// <summary>
    /// 全局 IO 别名映射服务实现
    /// 用于解耦业务层与通用 UI，支持动态注册和查询 IO 引脚名称
    /// 支持 [Description] 和 [Browsable] 特性
    /// </summary>
    public class IOMappingService : IIOMappingService
    {
        // 结构：DeviceId -> (PortIndex -> IOMapInfo)
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, IOMapInfo>> _inputMap = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, IOMapInfo>> _outputMap = new();

        /// <summary>
        /// 注册输入枚举（将 Enum 的 Int 值和名称映射绑定到指定设备）
        /// </summary>
        public void RegisterInputEnum<TEnum>(string deviceId) where TEnum : Enum
        {
            var map = _inputMap.GetOrAdd(deviceId, _ => new ConcurrentDictionary<int, IOMapInfo>());
            ParseEnumToMap<TEnum>(map);
        }

        /// <summary>
        /// 注册输出枚举
        /// </summary>
        public void RegisterOutputEnum<TEnum>(string deviceId) where TEnum : Enum
        {
            var map = _outputMap.GetOrAdd(deviceId, _ => new ConcurrentDictionary<int, IOMapInfo>());
            ParseEnumToMap<TEnum>(map);
        }

        /// <summary>
        /// 获取指定设备、指定输入引脚的 UI 显示名称
        /// </summary>
        public string GetInputName(string deviceId, int portIndex)
        {
            var info = GetInputInfo(deviceId, portIndex);
            return info?.Name;
        }

        /// <summary>
        /// 获取指定设备、指定输出引脚的 UI 显示名称
        /// </summary>
        public string GetOutputName(string deviceId, int portIndex)
        {
            var info = GetOutputInfo(deviceId, portIndex);
            return info?.Name;
        }

        /// <summary>
        /// 获取指定设备、指定输入引脚的完整信息（包含名称和可见性）
        /// </summary>
        public IOMapInfo GetInputInfo(string deviceId, int portIndex)
        {
            if (_inputMap.TryGetValue(deviceId, out var map) && map.TryGetValue(portIndex, out var info))
                return info;
            return null;
        }

        /// <summary>
        /// 获取指定设备、指定输出引脚的完整信息（包含名称和可见性）
        /// </summary>
        public IOMapInfo GetOutputInfo(string deviceId, int portIndex)
        {
            if (_outputMap.TryGetValue(deviceId, out var map) && map.TryGetValue(portIndex, out var info))
                return info;
            return null;
        }

        // 内部解析工具（支持获取 [Description] 和 [Browsable] 特性）
        private void ParseEnumToMap<TEnum>(ConcurrentDictionary<int, IOMapInfo> targetMap) where TEnum : Enum
        {
            foreach (var value in Enum.GetValues(typeof(TEnum)))
            {
                int index = (int)value;
                string name = value.ToString();
                bool isBrowsable = true;

                var fieldInfo = typeof(TEnum).GetField(name);
                if (fieldInfo != null)
                {
                    // 解析 [Description] 特性
                    var descAttr = fieldInfo.GetCustomAttribute<DescriptionAttribute>();
                    if (descAttr != null) name = descAttr.Description;

                    // 解析 [Browsable] 特性
                    var browsableAttr = fieldInfo.GetCustomAttribute<BrowsableAttribute>();
                    if (browsableAttr != null) isBrowsable = browsableAttr.Browsable;
                }

                targetMap[index] = new IOMapInfo { Name = name, IsBrowsable = isBrowsable };
            }
        }
    }
}
