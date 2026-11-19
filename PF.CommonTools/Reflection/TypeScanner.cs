using System.Collections.Concurrent;
using System.Reflection;

namespace PF.CommonTools.Reflection
{
  

    /// <summary>
    /// 泛型类型查找工具类
    /// </summary>
    public static class TypeScanner<TBaseType>
    {
        // 值里带上缓存时 AppDomain 的程序集数量：Prism 模块等程序集是启动后才加载的，
        // 早于它们的扫描结果不能一直沿用，程序集数量变化时重新扫描
        private static readonly ConcurrentDictionary<TypeScanOptions, (int AssemblyCount, List<Type> Types)> _typeCache = new();

        // 按程序集缓存其全部类型（未经筛选）。此前按 "程序集名_选项哈希" 缓存筛选后的结果，
        // 而哈希不含 CustomFilter，两个不同筛选条件会互相拿到对方的结果；筛选本身很便宜，每次重做即可
        private static readonly ConcurrentDictionary<Assembly, Type[]> _assemblyTypeCache = new();
        private static readonly object _initializationLock = new();
        private static bool _isInitialized = false;

        /// <summary>
        /// 类型扫描选项
        /// </summary>
        public class TypeScanOptions : IEquatable<TypeScanOptions>
        {
            /// <summary>
            /// IncludeAbstract
            /// </summary>
            public bool IncludeAbstract { get; set; }
            /// <summary>
            /// IncludeInterface
            /// </summary>
            public bool IncludeInterface { get; set; }
            /// <summary>
            /// IncludeGenericDefinitions
            /// </summary>
            public bool IncludeGenericDefinitions { get; set; }
            /// <summary>
            /// CacheResults
            /// </summary>
            public bool CacheResults { get; set; } = true;
            /// <summary>
            /// Assemblys名称
            /// </summary>
            public string[] AssemblyNames { get; set; }
            /// <summary>
            /// spaces名称
            /// </summary>
            public string[] Namespaces { get; set; }
            /// <summary>
            /// CustomFilter
            /// </summary>
            public Func<Type, bool> CustomFilter { get; set; }

            /// <summary>
            /// 判断是否相等
            /// </summary>
            public bool Equals(TypeScanOptions other)
            {
                if (other is null) return false;
                if (ReferenceEquals(this, other)) return true;

                return IncludeAbstract == other.IncludeAbstract &&
                       IncludeInterface == other.IncludeInterface &&
                       IncludeGenericDefinitions == other.IncludeGenericDefinitions &&
                       CacheResults == other.CacheResults &&
                       SequenceEquals(AssemblyNames, other.AssemblyNames) &&
                       SequenceEquals(Namespaces, other.Namespaces) &&
                       Equals(CustomFilter, other.CustomFilter);
            }

            /// <summary>
            /// 判断是否相等
            /// </summary>
            public override bool Equals(object obj) => Equals(obj as TypeScanOptions);

            /// <summary>
            /// 获取HashCode
            /// </summary>
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + IncludeAbstract.GetHashCode();
                    hash = hash * 23 + IncludeInterface.GetHashCode();
                    hash = hash * 23 + IncludeGenericDefinitions.GetHashCode();
                    hash = hash * 23 + CacheResults.GetHashCode();
                    hash = hash * 23 + SequenceHash(AssemblyNames);
                    hash = hash * 23 + SequenceHash(Namespaces);
                    hash = hash * 23 + (CustomFilter?.GetHashCode() ?? 0);
                    return hash;
                }
            }

            // 数组按内容比较：调用方每次都 new 一个新数组传进来，按引用比较时缓存永远命中不了，
            // 而且每次都往缓存里新增一项，字典只增不减
            private static bool SequenceEquals(string[] a, string[] b)
            {
                if (ReferenceEquals(a, b)) return true;
                if (a is null || b is null) return false;
                return a.SequenceEqual(b, StringComparer.OrdinalIgnoreCase);
            }

            private static int SequenceHash(string[] items)
            {
                if (items is null) return 0;
                unchecked
                {
                    int h = 19;
                    foreach (var s in items) h = h * 31 + (s is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(s));
                    return h;
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

            int assemblyCount = AppDomain.CurrentDomain.GetAssemblies().Length;
            if (options.CacheResults && _typeCache.TryGetValue(options, out var cached)
                && cached.AssemblyCount == assemblyCount)
            {
                return cached.Types.ToList(); // 返回副本
            }

            var types = ScanTypesInternal(options).ToList();

            if (options.CacheResults)
            {
                _typeCache[options] = (assemblyCount, types);
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
            try
            {
                if (!_assemblyTypeCache.TryGetValue(assembly, out var types))
                {
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t != null).ToArray()!;
                    }
                    _assemblyTypeCache[assembly] = types;
                }

                return FilterTypes(types, options).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[TypeScanner] 程序集类型扫描失败: {ex.Message}");
                return new List<Type>();
            }
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
