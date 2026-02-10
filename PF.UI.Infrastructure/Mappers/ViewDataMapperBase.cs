
using PF.Core.Interfaces.Configuration;
using System.Reflection;

namespace PF.UI.Infrastructure.Mappers
{
    // <summary>
    /// 视图数据映射器基类
    /// </summary>
    public abstract class ViewDataMapperBase : IViewDataMapper
    {
        /// <summary>
        /// 将数据映射到视图
        /// </summary>
        public virtual bool MapToView(object viewInstance, object data)
        {
            if (viewInstance == null || data == null)
                return false;

            var viewType = viewInstance.GetType();
            var dataType = data.GetType();

            // 检查是否有特定的映射逻辑
            if (HasSpecificMapping(viewInstance, data))
                return true;

            // 默认属性映射
            return MapProperties(data, viewInstance);
        }

        /// <summary>
        /// 从视图获取数据
        /// </summary>
        public virtual object MapFromView(object viewInstance)
        {
            if (viewInstance == null)
                return null;

            var viewType = viewInstance.GetType();

            // 检查是否有特定的提取逻辑
            var specificData = ExtractSpecificData(viewInstance);
            if (specificData != null)
                return specificData;

            // 默认属性提取
            return ExtractProperties(viewInstance);
        }

        /// <summary>
        /// 检查是否有特定的映射逻辑（子类可重写）
        /// </summary>
        protected virtual bool HasSpecificMapping(object viewInstance, object data)
        {
            return false;
        }

        /// <summary>
        /// 检查是否有特定的数据提取逻辑（子类可重写）
        /// </summary>
        protected virtual object ExtractSpecificData(object viewInstance)
        {
            return null;
        }

        /// <summary>
        /// 映射属性
        /// </summary>
        protected virtual bool MapProperties(object source, object target)
        {
            try
            {
                var sourceType = source.GetType();
                var targetType = target.GetType();

                var sourceProperties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead)
                    .ToDictionary(p => p.Name, p => p);

                var targetProperties = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite)
                    .ToDictionary(p => p.Name, p => p);

                bool mapped = false;

                foreach (var targetProp in targetProperties.Values)
                {
                    if (sourceProperties.TryGetValue(targetProp.Name, out var sourceProp))
                    {
                        if (TrySetProperty(target, targetProp, source, sourceProp))
                        {
                            mapped = true;
                        }
                    }
                }

                return mapped;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"属性映射失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 提取属性到新对象
        /// </summary>
        protected virtual object ExtractProperties(object viewInstance)
        {
            try
            {
                var viewType = viewInstance.GetType();
                var dataType = GetDataTypeFromViewType(viewType);

                if (dataType == null)
                    return null;

                var data = Activator.CreateInstance(dataType);
                var dataProperties = dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite)
                    .ToDictionary(p => p.Name, p => p);

                var viewProperties = viewType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead)
                    .ToDictionary(p => p.Name, p => p);

                foreach (var dataProp in dataProperties.Values)
                {
                    if (viewProperties.TryGetValue(dataProp.Name, out var viewProp))
                    {
                        TrySetProperty(data, dataProp, viewInstance, viewProp);
                    }
                }

                return data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"属性提取失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 尝试设置属性值
        /// </summary>
        protected virtual bool TrySetProperty(object target, PropertyInfo targetProp,
                                             object source, PropertyInfo sourceProp)
        {
            try
            {
                var sourceValue = sourceProp.GetValue(source);
                if (sourceValue == null)
                    return false;

                // 获取目标类型（处理Nullable类型）
                var targetType = Nullable.GetUnderlyingType(targetProp.PropertyType)
                               ?? targetProp.PropertyType;

                // 类型兼容性检查
                if (targetType.IsAssignableFrom(sourceProp.PropertyType))
                {
                    targetProp.SetValue(target, sourceValue);
                    return true;
                }

                // 尝试类型转换
                try
                {
                    var convertedValue = Convert.ChangeType(sourceValue, targetType);
                    targetProp.SetValue(target, convertedValue);
                    return true;
                }
                catch
                {
                    // 转换失败，尝试其他方式
                    return TryCustomConversion(target, targetProp, sourceValue);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置属性 {targetProp.Name} 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 尝试自定义转换（子类可重写）
        /// </summary>
        protected virtual bool TryCustomConversion(object target, PropertyInfo targetProp, object sourceValue)
        {
            return false;
        }

        /// <summary>
        /// 从视图类型获取数据类型
        /// </summary>
        protected virtual Type GetDataTypeFromViewType(Type viewType)
        {
            // 默认实现：根据 ParamViewAttribute 查找对应的数据类型
            // 这需要在 ViewFactory 中实现，这里返回 null 让子类实现
            return null;
        }
    }
}
