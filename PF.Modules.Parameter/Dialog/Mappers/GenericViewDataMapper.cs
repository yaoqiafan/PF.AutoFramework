using PF.Common.Core.Param.Base;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PF.Common.Param.Dialog.Mappers
{
    /// <summary>
    /// 通用视图数据映射器
    /// </summary>
    public class GenericViewDataMapper : ViewDataMapperBase
    {
        // 缓存反射信息
        private static readonly ConcurrentDictionary<Type, PropertyInfo> _valuePropertyCache =
            new ConcurrentDictionary<Type, PropertyInfo>();

        private static readonly ConcurrentDictionary<Type, bool> _hasValuePropertyCache =
            new ConcurrentDictionary<Type, bool>();

        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            var viewType = viewInstance.GetType();

            // 检查是否有 Value 属性
            if (HasValueProperty(viewType))
            {
                var valueProperty = GetValueProperty(viewType);

                if (valueProperty != null && valueProperty.CanWrite)
                {
                    try
                    {
                        var dataType = data.GetType();

                        // 类型兼容检查
                        if (valueProperty.PropertyType.IsAssignableFrom(dataType))
                        {
                            valueProperty.SetValue(viewInstance, data);
                            return true;
                        }
                        else if (IsSimpleType(dataType) && IsSimpleType(valueProperty.PropertyType))
                        {
                            // 简单类型转换
                            var convertedValue = Convert.ChangeType(data, valueProperty.PropertyType);
                            valueProperty.SetValue(viewInstance, convertedValue);
                            return true;
                        }
                    }
                    catch
                    {
                        // 转换失败，回退到属性映射
                    }
                }
            }

            return false;
        }

        protected override object ExtractSpecificData(object viewInstance)
        {
            var viewType = viewInstance.GetType();

            // 如果有 Value 属性，直接返回其值
            if (HasValueProperty(viewType))
            {
                var valueProperty = GetValueProperty(viewType);

                if (valueProperty != null && valueProperty.CanRead)
                {
                    return valueProperty.GetValue(viewInstance);
                }
            }

            return null;
        }

        /// <summary>
        /// 检查类型是否有 Value 属性
        /// </summary>
        private bool HasValueProperty(Type viewType)
        {
            return _hasValuePropertyCache.GetOrAdd(viewType, type =>
            {
                return type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance) != null;
            });
        }

        /// <summary>
        /// 获取 Value 属性
        /// </summary>
        private PropertyInfo GetValueProperty(Type viewType)
        {
            return _valuePropertyCache.GetOrAdd(viewType, type =>
            {
                return type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            });
        }

        /// <summary>
        /// 判断是否为简单类型
        /// </summary>
        private bool IsSimpleType(Type type)
        {
            return type.IsPrimitive ||
                   type == typeof(string) ||
                   type == typeof(decimal) ||
                   type == typeof(DateTime) ||
                   type == typeof(TimeSpan) ||
                   type.IsEnum;
        }
    }
}
