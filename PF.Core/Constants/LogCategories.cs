using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Constants
{
    /// <summary>
    /// 日志分类常量
    /// </summary>
    public static class LogCategories
    {
        // 核心分类
        public const string System = "System";
        public const string Database = "Database";
        public const string UI = "UI";
        public const string Communication = "Communication";

        public const string Custom = "Custom";


        public const string HaraWare = "Hardware";


        public const string Recipe = "Recipe";
        /// <summary>
        /// 获取所有内置分类
        /// </summary>
        public static string[] GetBuiltInCategories()
        {
            return new[]
            {
                System,
                Database,
                UI,
                Communication,
               Custom,
               HaraWare,
               Recipe,
            };
        }
    }
}
