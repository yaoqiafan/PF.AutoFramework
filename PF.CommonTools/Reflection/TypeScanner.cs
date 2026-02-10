using System.Collections.Concurrent;
using System.Reflection;

namespace PF.Infrastructure.Utilities.Reflection
{
  

    /// <summary>
    /// 泛型类型查找工具类
    /// </summary>
    public static class TypeScanner<TBaseType>
    {
        private static readonly ConcurrentDictionary<TypeScanOptions, List<Type>> _typeCache = new();
        private static readonly ConcurrentDictionary<string, List<Type>> _assemblyTypeCache = new();
        private static readonly object _initializationLock = new();
        private static bool _isInitialized = false;

        /// <summary>
        /// 类型扫描选项
        /// </summary>
        public class TypeScanOptions : IEquatable<TypeScanOptions>
        {
            public bool IncludeAbstract { get; set; }
            public bool IncludeInterface { get; set; }
            public bool IncludeGenericDefinitions { get; set; }
            public bool CacheResults { get; set; } = true;
            public string[] AssemblyNames { get; set; }
            public string[] Namespaces { get; set; }
            public Func<Type, bool> CustomFilter { get; set; }

            public bool Equals(TypeScanOptions other)
            {
                if (other is null) return false;
                if (ReferenceEquals(this, other)) return true;

                return IncludeAbstract == other.IncludeAbstract &&
                       IncludeInterface == other.IncludeInterface &&
                       IncludeGenericDefinitions == other.IncludeGenericDefinitions &&
                       CacheResults == other.CacheResults &&
                       Equals(AssemblyNames, other.AssemblyNames) &&
                       Equals(Namespaces, other.Namespaces) &&
                       Equals(CustomFilter, other.CustomFilter);
            }

            public override bool Equals(object obj) => Equals(obj as TypeScanOptions);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + IncludeAbstract.GetHashCode();
                    hash = hash * 23 + IncludeInterface.GetHashCode();
                    hash = hash * 23 + IncludeGenericDefinitions.GetHashCode();
                    hash = hash * 23 + CacheResults.GetHashCode();
                    hash = hash * 23 + (AssemblyNames?.GetHashCode() ?? 0);
                    hash = hash * 23 + (Namespaces?.GetHashCode() ?? 0);
                    return hash;
                }
            }
        }

        /// <summary>
        /// 默认扫描选项
        /// </summary>
        public static TypeScanOptions DefaultOptions { get; } = new TypeScanOptions
        {
            IncludeAbstract = false,
            IncludeInterface = false,
            IncludeGenericDefinitions = false,
            CacheResults = true
        };

        /// <summary>
        /// 获取所有继承自/实现TBaseType的类型
        /// </summary>
        public static List<Type> GetAllTypes(TypeScanOptions options = null)
        {
            options ??= DefaultOptions;

            if (options.CacheResults && _typeCache.TryGetValue(options, out var cachedTypes))
            {
                return cachedTypes.ToList(); // 返回副本
            }

            var types = ScanTypesInternal(options).ToList();

            if (options.CacheResults)
            {
                _typeCache[options] = types;
            }

            return types.ToList();
        }

        /// <summary>
        /// 异步获取所有类型
        /// </summary>
        public static Task<List<Type>> GetAllTypesAsync(TypeScanOptions options = null)
        {
            return Task.Run(() => GetAllTypes(options));
        }

        /// <summary>
        /// 获取按程序集分组的类型
        /// </summary>
        public static Dictionary<string, List<Type>> GetTypesByAssembly(TypeScanOptions options = null)
        {
            options ??= DefaultOptions;

            var assemblies = GetAssembliesToScan(options.AssemblyNames);
            var result = new Dictionary<string, List<Type>>();

            Parallel.ForEach(assemblies, assembly =>
            {
                try
                {
                    var types = GetTypesFromAssembly(assembly, options);
                    if (types.Any())
                    {
                        lock (result)
                        {
                            result[assembly.FullName] = types;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 记录日志
                    System.Diagnostics.Debug.WriteLine($"扫描程序集 {assembly.FullName} 失败: {ex.Message}");
                }
            });

            return result;
        }

        /// <summary>
        /// 获取按命名空间分组的类型
        /// </summary>
        public static Dictionary<string, List<Type>> GetTypesByNamespace(TypeScanOptions options = null)
        {
            options ??= DefaultOptions;
            var allTypes = GetAllTypes(options);

            return allTypes
                .GroupBy(t => t.Namespace ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// 创建类型的实例
        /// </summary>
        public static List<TBaseType> CreateInstances(TypeScanOptions options = null)
        {
            options ??= DefaultOptions;
            var types = GetAllTypes(options);

            return types.Select(type =>
            {
                try
                {
                    return (TBaseType)Activator.CreateInstance(type);
                }
                catch
                {
                    return default;
                }
            }).Where(instance => instance != null).ToList();
        }

        /// <summary>
        /// 创建带参数的实例
        /// </summary>
        public static List<TBaseType> CreateInstancesWithArgs(object[] args, TypeScanOptions options = null)
        {
            options ??= DefaultOptions;
            var types = GetAllTypes(options);

            return types.Select(type =>
            {
                try
                {
                    return (TBaseType)Activator.CreateInstance(type, args);
                }
                catch
                {
                    return default;
                }
            }).Where(instance => instance != null).ToList();
        }

        /// <summary>
        /// 清空缓存
        /// </summary>
        public static void ClearCache()
        {
            _typeCache.Clear();
            _assemblyTypeCache.Clear();
            _isInitialized = false;
        }

        /// <summary>
        /// 预热扫描（后台初始化）
        /// </summary>
        public static void WarmUp()
        {
            if (_isInitialized) return;

            lock (_initializationLock)
            {
                if (_isInitialized) return;

                Task.Run(() =>
                {
                    try
                    {
                        GetAllTypes(DefaultOptions);
                        _isInitialized = true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"预热失败: {ex.Message}");
                    }
                });
            }
        }

        #region 私有方法

        private static IEnumerable<Type> ScanTypesInternal(TypeScanOptions options)
        {
            var assemblies = GetAssembliesToScan(options.AssemblyNames);
            var baseType = typeof(TBaseType);
            bool isInterface = baseType.IsInterface;

            foreach (var assembly in assemblies)
            {
                foreach (var type in GetTypesFromAssembly(assembly, options))
                {
                    yield return type;
                }
            }
        }

        private static List<Assembly> GetAssembliesToScan(string[] assemblyNames = null)
        {
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

            if (assemblyNames == null || assemblyNames.Length == 0)
            {
                return allAssemblies;
            }

            return allAssemblies.Where(assembly =>
            {
                var name = assembly.GetName().Name;
                return assemblyNames.Any(an =>
                    an.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    assembly.FullName.Contains(an));
            }).ToList();
        }

        private static List<Type> GetTypesFromAssembly(Assembly assembly, TypeScanOptions options)
        {
            var cacheKey = $"{assembly.FullName}_{options.GetHashCode()}";

            if (options.CacheResults && _assemblyTypeCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            List<Type> assemblyTypes;

            try
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                assemblyTypes = FilterTypes(types, options).ToList();
            }
            catch
            {
                assemblyTypes = new List<Type>();
            }

            if (options.CacheResults)
            {
                _assemblyTypeCache[cacheKey] = assemblyTypes;
            }

            return assemblyTypes;
        }

        private static IEnumerable<Type> FilterTypes(IEnumerable<Type> types, TypeScanOptions options)
        {
            var baseType = typeof(TBaseType);
            bool isInterface = baseType.IsInterface;

            foreach (var type in types)
            {
                if (type == null) continue;

                // 检查是否为类（对于接口查找，可以是类或接口）
                if (!options.IncludeInterface && !type.IsClass)
                    continue;

                // 检查抽象类
                if (!options.IncludeAbstract && type.IsAbstract)
                    continue;

                // 检查泛型定义
                if (!options.IncludeGenericDefinitions && type.IsGenericTypeDefinition)
                    continue;

                // 检查是否继承/实现目标类型
                bool isMatch;
                if (isInterface)
                {
                    isMatch = baseType.IsAssignableFrom(type) && type != baseType;
                }
                else
                {
                    isMatch = type.IsSubclassOf(baseType) || type == baseType;
                }

                if (!isMatch)
                    continue;

                // 命名空间筛选
                if (options.Namespaces != null && options.Namespaces.Length > 0)
                {
                    if (string.IsNullOrEmpty(type.Namespace) ||
                        !options.Namespaces.Any(ns => type.Namespace.StartsWith(ns, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                }

                // 自定义筛选
                if (options.CustomFilter != null && !options.CustomFilter(type))
                    continue;

                yield return type;
            }
        }

        #endregion
    }

    /// <summary>
    /// 扩展方法，提供更方便的API
    /// </summary>
    public static class TypeScannerExtensions
    {
        /// <summary>
        /// 快速获取所有非抽象实现类
        /// </summary>
        public static List<Type> GetAllConcreteTypes<T>() where T : class
        {
            return TypeScanner<T>.GetAllTypes();
        }

        /// <summary>
        /// 获取所有实现类（包括抽象类）
        /// </summary>
        public static List<Type> GetAllTypesIncludingAbstract<T>() where T : class
        {
            return TypeScanner<T>.GetAllTypes(new TypeScanner<T>.TypeScanOptions
            {
                IncludeAbstract = true,
                CacheResults = true
            });
        }

        /// <summary>
        /// 获取指定命名空间下的类型
        /// </summary>
        public static List<Type> GetTypesInNamespace<T>(string namespacePrefix) where T : class
        {
            return TypeScanner<T>.GetAllTypes(new TypeScanner<T>.TypeScanOptions
            {
                Namespaces = new[] { namespacePrefix },
                CacheResults = true
            });
        }

        /// <summary>
        /// 获取带自定义筛选的类型
        /// </summary>
        public static List<Type> GetTypesWithFilter<T>(Func<Type, bool> filter) where T : class
        {
            return TypeScanner<T>.GetAllTypes(new TypeScanner<T>.TypeScanOptions
            {
                CustomFilter = filter,
                CacheResults = true
            });
        }
    }
}
