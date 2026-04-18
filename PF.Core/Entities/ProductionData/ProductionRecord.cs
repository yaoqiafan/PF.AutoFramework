using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PF.Core.Entities.ProductionData
{
    /// <summary>
    /// 查询结果 DTO。轻量级，不含 EF Core 追踪，适合直接绑定到 UI。
    /// </summary>
    public class ProductionRecord
    {
        /// <summary>记录ID</summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>记录名称</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>JSON序列化值</summary>
        public string JsonValue { get; set; } = string.Empty;
        /// <summary>类型全名</summary>
        public string? TypeFullName { get; set; }
        /// <summary>分类</summary>
        public string Category { get; set; } = string.Empty;
        /// <summary>设备ID</summary>
        public string? DeviceId { get; set; }
        /// <summary>记录类型</summary>
        public string? RecordType { get; set; }
        /// <summary>记录时间</summary>
        public DateTime RecordTime { get; set; }
        /// <summary>批次ID</summary>
        public string? BatchId { get; set; }
        /// <summary>创建时间</summary>
        public DateTime CreateTime { get; set; }
        /// <summary>备注</summary>
        public string? Remarks { get; set; }

        /// <summary>反序列化 JsonValue 为目标类型（便捷方法）</summary>
        public T? Deserialize<T>() where T : class
        {
            if (string.IsNullOrEmpty(JsonValue)) return null;
            return JsonSerializer.Deserialize<T>(JsonValue);
        }
    }
}
