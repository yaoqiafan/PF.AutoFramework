using PF.Modules.Parameter.ViewModels.Models;
using PF.UI.Infrastructure.Mappers;
using System.Reflection;

namespace PF.Modules.Parameter.Dialog.Mappers
{
    /// <summary>
    /// 字符串参数视图映射器
    /// </summary>
    public class StringParamViewMapper : ViewDataMapperBase
    {
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is StringParamView stringView && data is string stringValue)
            {
                stringView.Value = stringValue;
                return true;
            }

            return false;
        }

        protected override object ExtractSpecificData(object viewInstance)
        {
            if (viewInstance is StringParamView stringView)
            {
                return stringView.Value;
            }

            return null;
        }
    }

    /// <summary>
    /// 数值类型参数视图映射器
    /// </summary>
    /// <typeparam name="T">数值类型</typeparam>
    public class NumericParamViewMapper<T> : ViewDataMapperBase where T : struct
    {
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            // 使用反射获取泛型类型的Value属性
            var viewType = viewInstance.GetType();
            var valueProperty = viewType.GetProperty("Value");

            if (valueProperty != null && valueProperty.CanWrite &&
                data is T numericValue)
            {
                valueProperty.SetValue(viewInstance, numericValue);
                return true;
            }

            return false;
        }

        protected override object ExtractSpecificData(object viewInstance)
        {
            var viewType = viewInstance.GetType();
            var valueProperty = viewType.GetProperty("Value");

            if (valueProperty != null && valueProperty.CanRead)
            {
                return valueProperty.GetValue(viewInstance);
            }

            return null;
        }

        protected override bool TryCustomConversion(object target, PropertyInfo targetProp, object sourceValue)
        {
            // 处理数值类型的字符串转换
            if (typeof(T) == typeof(int) && sourceValue is string strInt)
            {
                if (int.TryParse(strInt, out int intValue))
                {
                    targetProp.SetValue(target, intValue);
                    return true;
                }
            }
            else if (typeof(T) == typeof(double) && sourceValue is string strDouble)
            {
                if (double.TryParse(strDouble, out double doubleValue))
                {
                    targetProp.SetValue(target, doubleValue);
                    return true;
                }
            }

            return base.TryCustomConversion(target, targetProp, sourceValue);
        }
    }

    /// <summary>
    /// 布尔参数视图映射器
    /// </summary>
    public class BooleanParamViewMapper : ViewDataMapperBase
    {
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is BooleanParamView boolView)
            {
                if (data is bool boolValue)
                {
                    boolView.Value = boolValue;
                    return true;
                }
                else if (data is string strBool)
                {
                    if (bool.TryParse(strBool, out bool parsedBool))
                    {
                        boolView.Value = parsedBool;
                        return true;
                    }
                }
            }

            return false;
        }

        protected override object ExtractSpecificData(object viewInstance)
        {
            if (viewInstance is BooleanParamView boolView)
            {
                return boolView.Value;
            }

            return null;
        }
    }
}
