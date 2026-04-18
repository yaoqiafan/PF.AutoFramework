using PF.Core.Entities.SecsGem.Message;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PF.Core.Entities.SecsGem.Command
{
    /// <summary>
    /// SECS消息命令定义
    /// </summary>
    public class SFCommand
    {
        /// <summary>Stream编号</summary>
        public uint Stream { get; set; }
        /// <summary>Function编号</summary>
        public uint Function { get; set; }
        /// <summary>命令名称</summary>
        public string Name { get; set; }
        /// <summary>命令ID</summary>
        public string ID { get; set; }
        /// <summary>关联的SECS消息</summary>
        public SecsGemMessage Message { get; set; }

        /// <summary>命令键值（S{Stream}F{Function}格式）</summary>
        [JsonIgnore]
        public string Key => $"S{Stream}F{Function}";

        /// <summary>响应ID</summary>
        public string ResponseID { get; set; } = string.Empty;

        /// <summary>
        /// 重写ToString方法，返回JSON格式字符串
        /// </summary>
        public override string ToString()
        {
            return ToJson();
        }
        /// <summary>
        /// 序列化为JSON字符串
        /// </summary>
        public string ToJson()
        {
            var dto = this.ToDbDto(includeMetadata: false);
            return JsonSerializer.Serialize(dto, JsonOptions.DatabaseOptions);
        }

        /// <summary>
        /// 从JSON字符串反序列化为SFCommand
        /// </summary>
        public static SFCommand FromJson(string json)
        {
            var dto = JsonSerializer.Deserialize<SFCommandDbDto>(json, JsonOptions.DatabaseOptions);
            return dto.ToEntity();
        }
    }
}
