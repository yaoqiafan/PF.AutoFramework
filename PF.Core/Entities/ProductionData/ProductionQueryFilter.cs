using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.ProductionData
{
    /// <summary>
    /// 生产数据查询过滤器。
    /// 所有字段均为可空（null 表示不过滤该字段），按需填写即可。
    /// 不同设备/场景可能只使用其中部分字段，无须担心没有用到的字段。
    /// </summary>
    public class ProductionQueryFilter
    {
        /// <summary>采集时间起始（含）</summary>
        public DateTime? StartTime { get; set; }

        /// <summary>采集时间截止（含）</summary>
        public DateTime? EndTime { get; set; }

        /// <summary>设备标识（精确匹配，null = 不过滤）</summary>
        public string? DeviceId { get; set; }

        /// <summary>记录类型（精确匹配，null = 不过滤）</summary>
        public string? RecordType { get; set; }

        /// <summary>批次编号（精确匹配，null = 不过滤）</summary>
        public string? BatchId { get; set; }

        /// <summary>关键词（模糊匹配 Name 和 JsonValue，null = 不过滤）</summary>
        public string? Keyword { get; set; }

        /// <summary>结果数量上限（null = 无限制；建议设置防止超大数据集）</summary>
        public int? MaxCount { get; set; }
    }
}
