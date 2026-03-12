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
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string JsonValue { get; set; } = string.Empty;
        public string? TypeFullName { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? DeviceId { get; set; }
        public string? RecordType { get; set; }
        public DateTime RecordTime { get; set; }
        public string? BatchId { get; set; }
        public DateTime CreateTime { get; set; }
        public string? Remarks { get; set; }

        /// <summary>反序列化 JsonValue 为目标类型（便捷方法）</summary>
        public T? Deserialize<T>() where T : class
        {
            if (string.IsNullOrEmpty(JsonValue)) return null;
            return JsonSerializer.Deserialize<T>(JsonValue);
        }
    }
}
