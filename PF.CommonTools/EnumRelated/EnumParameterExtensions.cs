using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PF.CommonTools.EnumRelated
{
    /// <summary>
    /// 解析结果实体类
    /// </summary>
    public class EnumParamInfo
    {
        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Category
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Default值
        /// </summary>
        public object DefaultValue { get; set; }
        /// <summary>
        /// 初始化实例
        /// </summary>
        public string TypeFullName => DefaultValue?.GetType().FullName;
    }

    /// <summary>
    /// 高性能、全通用的枚举扩展类
    /// </summary>
    public static class EnumParameterExtensions
    {
        /// <summary>
        /// 内部私有泛型缓存类：为每个枚举类型 T 创建一个独立的字典，彻底避免装箱！
        /// (注意：where T : struct, Enum 语法需要 C# 7.3 及以上版本支持)
        /// </summary>
        private static class EnumCache<T> where T : struct, Enum
        {
            public static readonly ConcurrentDictionary<T, EnumParamInfo> Dict = new ConcurrentDictionary<T, EnumParamInfo>();
        }

        /// <summary>
        /// 核心解析方法：获取枚举的所有附加信息（完全泛型化）
        /// </summary>
        public static EnumParamInfo GetParamInfo<T>(this T value) where T : struct, Enum
        {
            // 从当前枚举类型 T 专属的字典中获取或添加
            return EnumCache<T>.Dict.GetOrAdd(value, key =>
            {
                var info = new EnumParamInfo
                {
                    Description = key.ToString(),
                    Category = "未分类",
                    DefaultValue = null
                };

                // 获取当前枚举字段
                FieldInfo field = typeof(T).GetField(key.ToString());
                if (field == null) return info;

                // 提取 Description
                if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute descAttr)
                {
                    info.Description = descAttr.Description;
                }

                // 提取 Category
                if (Attribute.GetCustomAttribute(field, typeof(CategoryAttribute)) is CategoryAttribute catAttr)
                {
                    info.Category = catAttr.Category;
                }

                // 提取 DefaultValue
                if (Attribute.GetCustomAttribute(field, typeof(DefaultValueAttribute)) is DefaultValueAttribute defAttr)
                {
                    info.DefaultValue = defAttr.Value;
                }

                return info;
            });
        }

        // --- 快捷调用方法（全部改为泛型支持） ---

        /// <summary>
        /// 初始化实例
        /// </summary>
        public static string GetDescription<T>(this T value) where T : struct, Enum
            => value.GetParamInfo().Description;

        /// <summary>
        /// 初始化实例
        /// </summary>
        public static string GetCategory<T>(this T value) where T : struct, Enum
            => value.GetParamInfo().Category;

        /// <summary>
        /// 初始化实例
        /// </summary>
        public static object GetDefaultValue<T>(this T value) where T : struct, Enum
            => value.GetParamInfo().DefaultValue;

        /// <summary>
        /// 获取默认值并强转为指定类型 TResult
        /// </summary>
        /// <typeparam name="T">枚举类型</typeparam>
        /// <typeparam name="TResult">目标转换类型（如 int, double）</typeparam>
        public static TResult GetDefaultValueAs<T, TResult>(this T value, TResult fallback = default) where T : struct, Enum
        {
            var val = value.GetParamInfo().DefaultValue;
            if (val == null) return fallback;

            try
            {
                return (TResult)Convert.ChangeType(val, typeof(TResult));
            }
            catch
            {
                return fallback;
            }
        }
    }
}
