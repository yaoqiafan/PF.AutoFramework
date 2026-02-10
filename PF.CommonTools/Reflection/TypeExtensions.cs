using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PF.CommonTools.Reflection
{
    public static class TypeClassExtensions
    {
        public static Type GetTypeFromAnyAssembly( string typeName)
        {
            // 尝试直接获取
            Type type = Type.GetType(typeName);
            if (type != null) return type;

            // 在当前域所有程序集中查找
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            // 尝试加载程序集限定名
            int commaIndex = typeName.LastIndexOf(',');
            if (commaIndex > 0)
            {
                string assemblyName = typeName.Substring(commaIndex + 1).Trim();
                string shortTypeName = typeName.Substring(0, commaIndex).Trim();

                try
                {
                    Assembly assembly = Assembly.Load(assemblyName);
                    return assembly.GetType(shortTypeName);
                }
                catch { }
            }

            return null;
        }

        public static Type GetTypeWithAssembly(string typeName, string assemblyPath)
        {
            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            return assembly.GetType(typeName);
        }
    }
}
