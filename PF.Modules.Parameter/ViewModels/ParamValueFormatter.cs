using PF.CommonTools.Reflection;
using PF.Modules.Parameter.Dialog.Base;
using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace PF.Modules.Parameter.ViewModels
{
    /// <summary>
    /// 把参数的 JSON 值转成一眼能看懂的单行摘要，供参数表格"值"列显示。
    /// <list type="bullet">
    ///   <item>简单类型（字符串/数值/布尔/枚举）直接显示值本身。</item>
    ///   <item>复杂类型优先复用值编辑弹窗的参数视图（<see cref="ViewFactory"/>），标签取 <see cref="DisplayNameAttribute"/>，
    ///         与弹窗里看到的一致，<see cref="BrowsableAttribute"/>(false) 的属性不显示。</item>
    ///   <item>没有注册参数视图的类型退化为按 JSON 顶层字段展开（标签为原始字段名）。</item>
    /// </list>
    /// 密码类字段一律打码。结果截断到 <see cref="MaxLength"/> 字，完整内容通过"值更改"弹窗查看。
    /// 只读静态缓存 + 无 UI 依赖，可在后台线程调用。
    /// </summary>
    internal static class ParamValueFormatter
    {
        private const int MaxLength = 300;
        private const string Separator = "；";
        private const string MaskedText = "******";

        private static readonly ConcurrentDictionary<string, Type> _typeCache = new();
        private static readonly ConcurrentDictionary<Type, PropertyEntry[]> _propertyCache = new();

        private sealed record PropertyEntry(PropertyInfo Property, string Label, bool IsSensitive);

        /// <summary>生成参数值摘要</summary>
        /// <param name="typeFullName">参数值的类型全名（ParamItemViewModel.TypeFullName）</param>
        /// <param name="json">参数值 JSON</param>
        public static string Format(string typeFullName, string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            try
            {
                var type = ResolveType(typeFullName);
                var summary = type != null ? FormatWithParamView(type, json) : null;
                return Truncate(summary ?? FormatJson(json));
            }
            catch
            {
                // 非法 JSON：原样显示，便于发现脏数据
                return Truncate(json);
            }
        }

        private static Type ResolveType(string typeFullName)
        {
            if (string.IsNullOrWhiteSpace(typeFullName))
                return null;

            if (_typeCache.TryGetValue(typeFullName, out var cached))
                return cached;

            // GetTypeFromAnyAssembly 会遍历所有程序集，必须缓存；找不到的不缓存，留给后加载的程序集
            var type = TypeClassExtensions.GetTypeFromAnyAssembly(typeFullName);
            if (type != null)
                _typeCache[typeFullName] = type;
            return type;
        }

        private static string FormatWithParamView(Type type, string json)
        {
            object data;
            try
            {
                data = JsonSerializer.Deserialize(json, type);
            }
            catch
            {
                return null;
            }

            if (data == null)
                return null;

            if (IsScalar(type))
                return FormatValue(data);

            object view;
            try
            {
                view = ViewFactory.GetViewInstanceWithData(type, data);
            }
            catch
            {
                return null;
            }

            if (view == null)
                return null;

            var parts = new List<string>();
            foreach (var entry in GetProperties(view.GetType()))
            {
                string text = FormatValue(entry.Property.GetValue(view));
                if (string.IsNullOrEmpty(text))
                    continue;

                parts.Add($"{entry.Label}={(entry.IsSensitive ? MaskedText : text)}");
            }

            return parts.Count > 0 ? string.Join(Separator, parts) : null;
        }

        private static PropertyEntry[] GetProperties(Type viewType)
        {
            return _propertyCache.GetOrAdd(viewType, t => t
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .Where(p => p.GetCustomAttribute<BrowsableAttribute>()?.Browsable != false)
                .Select(p => new PropertyEntry(
                    p,
                    p.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? p.Name,
                    IsSensitiveName(p.Name) || p.GetCustomAttribute<PasswordPropertyTextAttribute>()?.Password == true))
                .ToArray());
        }

        private static bool IsScalar(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
        }

        private static string FormatValue(object value)
        {
            return value switch
            {
                null => null,
                string s => s,
                bool b => b ? "是" : "否",
                Enum e => e.ToString(),
                IFormattable f => f.ToString(null, CultureInfo.CurrentCulture),
                ICollection c => $"{c.Count}项",
                // 嵌套对象在单行摘要里展开意义不大，跳过，完整内容看弹窗
                _ => null
            };
        }

        #region 无参数视图时的 JSON 兜底

        // 深度 0 = 根对象（字段用"；"平铺），深度 1 = 根的字段值（对象用 {k=v, …} 展开一层，
        // 典型如 HardwareConfig.ConnectionParameters 字典），深度 2 及以上的对象折叠为 {…}
        private const int MaxExpandDepth = 1;

        private static string FormatJson(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return FormatJsonValue(doc.RootElement, depth: 0);
        }

        private static string FormatJsonValue(JsonElement value, int depth)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "是",
                JsonValueKind.False => "否",
                JsonValueKind.Array => $"{value.GetArrayLength()}项",
                JsonValueKind.Object => depth <= MaxExpandDepth ? FormatJsonObject(value, depth) : "{…}",
                _ => null
            };
        }

        private static string FormatJsonObject(JsonElement obj, int depth)
        {
            var parts = new List<string>();
            foreach (var prop in obj.EnumerateObject())
            {
                string text = FormatJsonValue(prop.Value, depth + 1);
                if (string.IsNullOrEmpty(text))
                    continue;

                parts.Add($"{prop.Name}={(IsSensitiveName(prop.Name) ? MaskedText : text)}");
            }

            return depth == 0 ? string.Join(Separator, parts) : $"{{{string.Join(", ", parts)}}}";
        }

        #endregion

        private static bool IsSensitiveName(string name)
            => name.Contains("password", StringComparison.OrdinalIgnoreCase)
            || name.Contains("pwd", StringComparison.OrdinalIgnoreCase);

        private static string Truncate(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length > MaxLength ? text.Substring(0, MaxLength) + "…" : text;
        }
    }
}
