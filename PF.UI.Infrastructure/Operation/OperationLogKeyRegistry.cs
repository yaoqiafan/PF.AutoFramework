using PF.Core.Attributes;
using System.ComponentModel;
using System.Reflection;

namespace PF.UI.Infrastructure.Operation
{
    /// <summary>
    /// 操作日志键注册表：反射扫描带 <see cref="OperationLogKeyCatalogAttribute"/> 标记的静态类，
    /// 收集"页面（内层嵌套类名）→ Key（const 字段名）→ 默认描述（字段上的 DescriptionAttribute）"。
    /// 用法与 NavigationMenuService.RegisterAssembly 一致：框架各模块、消费项目各自在自己的
    /// Prism 模块 OnInitialized 里调用一次 RegisterAssembly(Assembly.GetExecutingAssembly())。
    /// </summary>
    public static class OperationLogKeyRegistry
    {
        private static readonly Dictionary<string, Dictionary<string, (string Description, bool Critical)>> _catalog = new();

        /// <summary>扫描指定程序集，收集其中的操作日志键目录</summary>
        public static void RegisterAssembly(Assembly assembly)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

            var catalogTypes = types.Where(t => t.IsDefined(typeof(OperationLogKeyCatalogAttribute), false));

            foreach (var catalogType in catalogTypes)
            {
                foreach (var pageType in catalogType.GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
                {
                    var pageName = pageType.Name;
                    if (!_catalog.TryGetValue(pageName, out var keys))
                    {
                        keys = new Dictionary<string, (string, bool)>();
                        _catalog[pageName] = keys;
                    }

                    var fields = pageType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                        .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string));

                    foreach (var field in fields)
                    {
                        var key = (string)field.GetRawConstantValue();
                        var description = field.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;
                        var critical = field.IsDefined(typeof(OperationLogCriticalAttribute), false);
                        keys[key] = (description, critical);
                    }
                }
            }
        }

        /// <summary>
        /// 查询某个页面下某个 Key 是否已登记：拿到默认描述文本，以及是否标记为"关键操作"
        /// （<see cref="OperationLogCriticalAttribute"/>）——决定它首次出现时默认是否启用记录。
        /// </summary>
        public static bool TryGetEntry(string pageName, string key, out string description, out bool critical)
        {
            description = string.Empty;
            critical = false;
            if (_catalog.TryGetValue(pageName, out var keys) && keys.TryGetValue(key, out var entry))
            {
                description = entry.Description;
                critical = entry.Critical;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 拿到某个页面下已登记的全部键（Key、默认描述、是否关键），供页面首次加载时整页预写配置用。
        /// 页面未登记时返回空集合。
        /// </summary>
        public static IReadOnlyList<(string Key, string Description, bool Critical)> GetPageEntries(string pageName)
        {
            return _catalog.TryGetValue(pageName, out var keys)
                ? keys.Select(kv => (kv.Key, kv.Value.Description, kv.Value.Critical)).ToList()
                : Array.Empty<(string, string, bool)>();
        }
    }
}
