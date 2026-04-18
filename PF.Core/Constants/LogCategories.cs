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
        /// <summary>系统日志分类</summary>
        public const string System = "System";
        /// <summary>数据库日志分类</summary>
        public const string Database = "Database";
        /// <summary>UI日志分类</summary>
        public const string UI = "UI";
        /// <summary>通讯日志分类</summary>
        public const string Communication = "Communication";

        /// <summary>自定义日志分类</summary>
        public const string Custom = "Custom";


        /// <summary>硬件日志分类</summary>
        public const string HaraWare = "Hardware";


        /// <summary>配方日志分类</summary>
        public const string Recipe = "Recipe";

        /// <summary>SecsGem日志分类</summary>
        public const string SecsGem = "SecsGem";
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
               SecsGem,
            };
        }
    }
}
