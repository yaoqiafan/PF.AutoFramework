using PF.Common.Data.Admin;
using PF.Common.Interface.Param;
using PF.Common.Interface.Param.Attributes;
using PF.Common.Param.Dialog.Mappers;
using PF.Common.Param.ViewModels.Models;
using System.Collections.Concurrent;
using System.Reflection;

namespace PF.Common.Param.Dialog.Base
{
    /// <summary>
    /// 通用视图工厂
    /// 支持系统类型和自定义类型的视图创建和映射
    /// </summary>
    public static class ViewFactory
    {
        // 系统类型到视图类型的映射
        private static readonly Dictionary<Type, Type> _systemTypeViews = new Dictionary<Type, Type>
        {
            // 字符串类型
            { typeof(string), typeof(StringParamView) },
            
            // 数值类型
            { typeof(int), typeof(NumericParamView<int>) },
            { typeof(long), typeof(NumericParamView<long>) },
            { typeof(short), typeof(NumericParamView<short>) },
            { typeof(byte), typeof(NumericParamView<byte>) },
            { typeof(float), typeof(NumericParamView<float>) },
            { typeof(double), typeof(NumericParamView<double>) },
            { typeof(decimal), typeof(NumericParamView<decimal>) },
            
            // 布尔类型
            { typeof(bool), typeof(BooleanParamView) },

        };

        // 系统类型到映射器的映射
        private static readonly Dictionary<Type, IViewDataMapper> _systemTypeMappers = new Dictionary<Type, IViewDataMapper>
        {
            // 字符串类型
            { typeof(string), new StringParamViewMapper() },
            
            // 数值类型
            { typeof(int), new NumericParamViewMapper<int>() },
            { typeof(long), new NumericParamViewMapper<long>() },
            { typeof(short), new NumericParamViewMapper<short>() },
            { typeof(byte), new NumericParamViewMapper<byte>() },
            { typeof(float), new NumericParamViewMapper<float>() },
            { typeof(double), new NumericParamViewMapper<double>() },
            { typeof(decimal), new NumericParamViewMapper<decimal>() },
            
            // 布尔类型
            { typeof(bool), new BooleanParamViewMapper() },

            { typeof(UserInfo), new UserParamViewMapper() },
        };

        // 自定义类型的视图类型缓存
        private static readonly ConcurrentDictionary<Type, Type> _customViewTypeCache = new ConcurrentDictionary<Type, Type>();

        // 自定义类型的映射器缓存
        private static readonly ConcurrentDictionary<Type, IViewDataMapper> _customMapperCache = new ConcurrentDictionary<Type, IViewDataMapper>();

        // 视图类型到数据类型的反向映射
        private static readonly ConcurrentDictionary<Type, Type> _viewToDataTypeCache = new ConcurrentDictionary<Type, Type>();

        /// <summary>
        /// 获取类型对应的视图实例
        /// </summary>
        /// <param name="parameterType">参数类型</param>
        /// <returns>视图实例，如果找不到则返回null</returns>
        public static object GetViewInstance(Type parameterType)
        {
            if (parameterType == null)
                return null;

            try
            {
                // 获取视图类型
                Type viewType = GetViewType(parameterType);

                if (viewType == null)
                    return null;

                // 如果是枚举类型且使用泛型视图，需要特殊处理
                if (viewType.IsGenericType && viewType.GetGenericTypeDefinition() == typeof(EnumParamView<>))
                {
                    // 获取枚举类型
                    var enumType = viewType.GetGenericArguments()[0];

                    // 创建泛型类型实例
                    var genericViewType = typeof(EnumParamView<>).MakeGenericType(enumType);
                    return Activator.CreateInstance(genericViewType);
                }

                // 创建视图实例
                return Activator.CreateInstance(viewType);
            }
            catch (Exception ex)
            {
                // 记录日志
                System.Diagnostics.Debug.WriteLine($"创建视图实例失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取类型对应的视图实例并绑定数据
        /// </summary>
        /// <param name="parameterType">参数类型</param>
        /// <param name="data">要绑定的数据对象</param>
        /// <returns>视图实例</returns>
        public static object GetViewInstanceWithData(Type parameterType, object data)
        {
            var viewInstance = GetViewInstance(parameterType);

            if (viewInstance != null && data != null)
            {
                // 获取映射器并绑定数据
                var mapper = GetMapper(parameterType);
                if (mapper != null)
                {
                    mapper.MapToView(viewInstance, data);
                }
            }

            return viewInstance;
        }

        /// <summary>
        /// 从视图实例提取数据
        /// </summary>
        /// <param name="viewInstance">视图实例</param>
        /// <returns>数据对象</returns>
        public static object ExtractDataFromView(object viewInstance)
        {
            if (viewInstance == null)
                return null;

            var viewType = viewInstance.GetType();

            // 获取对应的数据类型
            Type dataType = null;

            // 首先从反向映射缓存中查找
            if (_viewToDataTypeCache.TryGetValue(viewType, out dataType))
            {
                var mapper = GetMapper(dataType);
                return mapper?.MapFromView(viewInstance);
            }

            // 遍历自定义类型缓存查找
            foreach (var entry in _customViewTypeCache)
            {
                if (entry.Value == viewType)
                {
                    dataType = entry.Key;
                    var mapper = GetMapper(dataType);
                    return mapper?.MapFromView(viewInstance);
                }
            }

            // 遍历系统类型映射查找
            foreach (var entry in _systemTypeViews)
            {
                if (entry.Value == viewType)
                {
                    dataType = entry.Key;
                    var mapper = GetMapper(dataType);
                    return mapper?.MapFromView(viewInstance);
                }
            }

            return null;
        }

        /// <summary>
        /// 获取类型对应的映射器
        /// </summary>
        public static IViewDataMapper GetMapper(Type parameterType)
        {
            if (parameterType == null)
                return null;

            // 处理可空类型
            if (Nullable.GetUnderlyingType(parameterType) is Type underlyingType)
            {
                return GetMapper(underlyingType);
            }

            // 检查是否为系统类型
            if (_systemTypeMappers.TryGetValue(parameterType, out var systemMapper))
            {
                return systemMapper;
            }

            // 检查是否为自定义类型（从特性中获取）
            if (_customMapperCache.TryGetValue(parameterType, out var customMapper))
            {
                return customMapper;
            }

            // 尝试从特性中获取映射器
            var attribute = parameterType.GetCustomAttribute<ParamViewAttribute>();
            if (attribute != null)
            {
                try
                {
                    // 创建映射器实例
                    customMapper = Activator.CreateInstance(attribute.MapperType) as IViewDataMapper;
                    if (customMapper != null)
                    {
                        _customMapperCache[parameterType] = customMapper;
                        return customMapper;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"创建映射器实例失败: {ex.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// 获取类型对应的视图类型
        /// </summary>
        /// <param name="parameterType">参数类型</param>
        /// <returns>视图类型，如果找不到则返回null</returns>
        public static Type GetViewType(Type parameterType)
        {
            if (parameterType == null)
                return null;

            // 1. 处理可空类型
            if (Nullable.GetUnderlyingType(parameterType) is Type underlyingType)
            {
                return GetViewType(underlyingType);
            }

            // 2. 检查是否为枚举类型
            if (parameterType.IsEnum)
            {
                // 先检查是否有自定义特性
                var customViewType = GetCustomTypeViewType(parameterType);
                if (customViewType != null)
                    return customViewType;

                // 使用泛型枚举视图
                return typeof(EnumParamView<>).MakeGenericType(parameterType);
            }

            // 3. 检查是否为系统类型
            if (IsSystemType(parameterType))
            {
                return GetSystemTypeViewType(parameterType);
            }

            // 4. 自定义类型，使用特性
            return GetCustomTypeViewType(parameterType);
        }

        /// <summary>
        /// 获取系统类型的视图类型
        /// </summary>
        private static Type GetSystemTypeViewType(Type systemType)
        {
            // 直接查找映射
            if (_systemTypeViews.TryGetValue(systemType, out Type viewType))
            {
                return viewType;
            }

            // 处理泛型类型
            if (systemType.IsGenericType)
            {
                var genericDefinition = systemType.GetGenericTypeDefinition();
                if (_systemTypeViews.TryGetValue(genericDefinition, out viewType))
                {
                    return viewType;
                }
            }

            // 默认系统类型视图
            return typeof(StringParamView);
        }

        /// <summary>
        /// 获取自定义类型的视图类型
        /// </summary>
        private static Type GetCustomTypeViewType(Type customType)
        {
            // 检查缓存
            if (_customViewTypeCache.TryGetValue(customType, out Type viewType))
            {
                return viewType;
            }

            // 查找特性
            var attribute = customType.GetCustomAttribute<ParamViewAttribute>();
            if (attribute?.ViewType != null)
            {
                try
                {
                    // 验证视图类型是否有无参构造函数
                    if (attribute.ViewType.GetConstructor(Type.EmptyTypes) != null)
                    {
                        // 加入缓存
                        _customViewTypeCache[customType] = attribute.ViewType;

                        // 建立反向映射
                        _viewToDataTypeCache[attribute.ViewType] = customType;

                        return attribute.ViewType;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"视图类型 {attribute.ViewType.Name} 必须有无参构造函数");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"获取自定义类型视图失败: {ex.Message}");
                }
            }

            // 如果不是自定义类型或者没有特性，返回null
            return null;
        }

        /// <summary>
        /// 判断是否为系统类型
        /// </summary>
        private static bool IsSystemType(Type type)
        {
            return type.Namespace?.StartsWith("System") == true ||
                   type.Namespace?.StartsWith("Microsoft") == true;
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public static void ClearCache()
        {
            _customViewTypeCache.Clear();
            _customMapperCache.Clear();
            _viewToDataTypeCache.Clear();
        }

        /// <summary>
        /// 预加载程序集中的所有自定义类型特性
        /// </summary>
        public static void PreloadAssemblies(params System.Reflection.Assembly[] assemblies)
        {
            var assembliesToScan = assemblies.Length > 0
                ? assemblies
                : AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assembliesToScan)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        // 提前获取视图类型和映射器，加入缓存
                        if (!IsSystemType(type))
                        {
                            GetViewType(type);
                            GetMapper(type);
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // 忽略无法加载的类型
                    continue;
                }
            }
        }


        /// <summary>
        /// 手动注册自定义类型映射（替代特性方式）
        /// </summary>
        /// <typeparam name="TData">数据类型</typeparam>
        /// <typeparam name="TView">视图类型</typeparam>
        /// <typeparam name="TMapper">映射器类型</typeparam>
        public static void RegisterCustomType<TData, TView, TMapper>()
            where TView : class, new()
            where TMapper : IViewDataMapper, new()
        {
            var dataType = typeof(TData);
            var viewType = typeof(TView);
            var mapper = new TMapper();

            _customViewTypeCache[dataType] = viewType;
            _customMapperCache[dataType] = mapper;
            _viewToDataTypeCache[viewType] = dataType;
        }

        /// <summary>
        /// 手动注册自定义类型映射（非泛型版本）
        /// </summary>
        public static void RegisterCustomType(Type dataType, Type viewType, IViewDataMapper mapper)
        {
            if (dataType == null) throw new ArgumentNullException(nameof(dataType));
            if (viewType == null) throw new ArgumentNullException(nameof(viewType));
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));

            _customViewTypeCache[dataType] = viewType;
            _customMapperCache[dataType] = mapper;
            _viewToDataTypeCache[viewType] = dataType;
        }
    }
}