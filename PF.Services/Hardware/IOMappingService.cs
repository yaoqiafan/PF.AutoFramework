using PF.Core.Interfaces.Device.Hardware.IO;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace PF.Services.Hardware
{
    /// <summary>
    /// 全局 IO 别名映射服务实现
    /// 用于解耦业务层与通用 UI，支持动态注册和查询 IO 引脚名称
    /// </summary>
    public class IOMappingService : IIOMappingService
    {
        // 结构：DeviceId -> (PortIndex -> ShowName)
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, string>> _inputMap = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, string>> _outputMap = new();

        /// <summary>
        /// 注册输入枚举（将 Enum 的 Int 值和名称映射绑定到指定设备）
        /// </summary>
        public void RegisterInputEnum<TEnum>(string deviceId) where TEnum : Enum
        {
            var map = _inputMap.GetOrAdd(deviceId, _ => new ConcurrentDictionary<int, string>());
            ParseEnumToMap<TEnum>(map);
        }

        /// <summary>
        /// 注册输出枚举
        /// </summary>
        public void RegisterOutputEnum<TEnum>(string deviceId) where TEnum : Enum
        {
            var map = _outputMap.GetOrAdd(deviceId, _ => new ConcurrentDictionary<int, string>());
            ParseEnumToMap<TEnum>(map);
        }

        /// <summary>
        /// 获取指定设备、指定输入引脚的 UI 显示名称
        /// </summary>
        public string GetInputName(string deviceId, int portIndex)
        {
            if (_inputMap.TryGetValue(deviceId, out var map) && map.TryGetValue(portIndex, out var name))
                return name;
            return null; // 未注册则返回 null
        }

        /// <summary>
        /// 获取指定设备、指定输出引脚的 UI 显示名称
        /// </summary>
        public string GetOutputName(string deviceId, int portIndex)
        {
            if (_outputMap.TryGetValue(deviceId, out var map) && map.TryGetValue(portIndex, out var name))
                return name;
            return null;
        }

        // 内部解析工具（支持获取 [Description] 特性或直接使用变量名）
        private void ParseEnumToMap<TEnum>(ConcurrentDictionary<int, string> targetMap) where TEnum : Enum
        {
            foreach (var value in Enum.GetValues(typeof(TEnum)))
            {
                int index = (int)value;
                string name = value.ToString();

                // 如果枚举标了 [Description("xxx")]，优先使用 Description
                var fieldInfo = typeof(TEnum).GetField(name);
                if (fieldInfo != null)
                {
                    var attr = fieldInfo.GetCustomAttribute<DescriptionAttribute>();
                    if (attr != null) name = attr.Description;
                }

                targetMap[index] = name;
            }
        }
    }
}
