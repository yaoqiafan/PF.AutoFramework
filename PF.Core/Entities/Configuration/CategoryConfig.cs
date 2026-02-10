using PF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.Configuration
{
    /// <summary>
    /// 日志分类配置
    /// </summary>
    // 在CategoryConfig类中添加
    public class CategoryConfig
    {
        /// <summary>
        /// 该分类的最低日志记录级别
        /// </summary>
        public LogLevel MinLevel { get; set; } = LogLevel.Info;

        /// <summary>
        /// 该分类的日志是否写入文件
        /// </summary>
        public bool EnableFileLog { get; set; } = true;

        /// <summary>
        /// 文件名前缀
        /// </summary>
        public string FileNamePrefix { get; set; } = "General";

        /// <summary>
        /// 最大文件大小（MB）
        /// </summary>
        public int MaxFileSizeMB { get; set; } = 10;

        /// <summary>
        /// 最大备份文件数
        /// </summary>
        public int MaxBackups { get; set; } = 30;

        /// <summary>
        /// 文件滚动方式
        /// </summary>
        public RollingMode RollingMode { get; set; } = RollingMode.Date;
    }

    /// <summary>
    /// 文件滚动方式
    /// </summary>
    public enum RollingMode
    {
        /// <summary>
        /// 按日期滚动
        /// </summary>
        Date,

        /// <summary>
        /// 按文件大小滚动
        /// </summary>
        Size,

        /// <summary>
        /// 按日期和文件大小滚动
        /// </summary>
        Composite
    }
}
