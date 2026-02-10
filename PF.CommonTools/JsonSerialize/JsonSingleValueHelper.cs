using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PF.CommonTools.JsonSerialize
{
    public static class JsonSingleValueHelper
    {
        // 序列化单个值
        public static string SerializeSingleValue<T>(T value)
        {
            return JsonSerializer.Serialize(value);
        }

        // 反序列化单个值
        public static T DeserializeSingleValue<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json);
        }

        // 动态反序列化，根据 JSON 内容自动判断类型
        public static object DeserializeDynamic(string json)
        {
            JsonElement element = JsonSerializer.Deserialize<JsonElement>(json);

            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out int intValue)
                    ? intValue : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => throw new InvalidOperationException("Unsupported JSON value kind")
            };
        }
    }
}
