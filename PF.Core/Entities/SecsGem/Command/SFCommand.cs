using PF.Core.Entities.SecsGem.Message;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PF.Core.Entities.SecsGem.Command
{
    public class SFCommand
    {
        public uint Stream { get; set; }
        public uint Function { get; set; }
        public string Name { get; set; }
        public string ID { get; set; }
        public SecsGemMessage Message { get; set; }

        [JsonIgnore]
        public string Key => $"S{Stream}F{Function}";

        public string ResponseID { get; set; } = string.Empty;

        /// <summary>
        /// 重写ToString方法，返回JSON格式的字符串
        /// </summary>
        public override string ToString()
        {
            return ToJson();
        }
        public string ToJson()
        {
            var dto = this.ToDbDto(includeMetadata: false);
            return JsonSerializer.Serialize(dto, JsonOptions.DatabaseOptions);
        }

        public static SFCommand FromJson(string json)
        {
            var dto = JsonSerializer.Deserialize<SFCommandDbDto>(json, JsonOptions.DatabaseOptions);
            return dto.ToEntity();
        }
    }
}
