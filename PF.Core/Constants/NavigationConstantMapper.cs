using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Constants
{
    public static class NavigationConstantMapper
    {
        private static readonly Dictionary<string, Type> _map;

        static NavigationConstantMapper()
        {
            _map = new Dictionary<string, Type>();
            var constantsType = typeof(NavigationConstants);
            var nestedTypes = constantsType.GetNestedTypes(BindingFlags.Public | BindingFlags.Static);

            foreach (var nested in nestedTypes)
            {
                var fields = nested.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                                   .Where(f => f.IsLiteral && !f.IsInitOnly); // const 字段

                foreach (var field in fields)
                {
                    var value = field.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        _map[value] = nested; // 或存储 nested.Name
                    }
                }
            }
        }

        public static string GetCategory(string constantValue)
        {
            return _map.TryGetValue(constantValue, out var type) ? type.Name : null;
        }
    }
}
