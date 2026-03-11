using PF.Core.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PF.Core.Entities.SecsGem.Message
{
    
    public class SFCommandDbDto
    {
        public uint Stream { get; set; }
        public uint Function { get; set; }
        public string Name { get; set; }
        public string ID { get; set; }
        public string Key { get; set; } // ԭJsonIgnore������
        public SecsGemMessageDbDto Message { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        
    }

  
    public class SecsGemMessageDbDto
    {
        public int Stream { get; set; }
        public string SystemBytes { get; set; } 
        public int Function { get; set; }
        public int LinkNumber { get; set; }
        public bool WBit { get; set; }
        public SecsGemNodeMessageDbDto RootNode { get; set; }
        public string MessageId { get; set; }
        public int Depth { get; set; } // ��Ϣ��ȣ����ڲ�ѯ
    }

    // SecsGemNodeMessage�����ݿ�DTO
    public class SecsGemNodeMessageDbDto
    {
        public DataType DataType { get; set; }
        public string Data { get; set; } // Base64����
        public int Length { get; set; }
        public List<SecsGemNodeMessageDbDto> SubNode { get; set; }
        public bool IsVariableNode { get; set; }
        public uint VariableCode { get; set; }
        public string TypedValue { get; set; } // JSON�ַ����洢
        public string DataHex { get; set; } // ʮ�����Ʊ�ʾ����ѡ�����ڲ鿴��

        // �ڵ�·�������ڲ�ѯ��
        public string NodePath { get; set; }
    }

    
    public static class JsonOptions
    {
        public static readonly JsonSerializerOptions DatabaseOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false, // �洢ʱ����Ҫ����
            Converters = {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        },
            DefaultIgnoreCondition = JsonIgnoreCondition.Never // �������κ�����
        };

        public static readonly JsonSerializerOptions TypedValueOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
}
