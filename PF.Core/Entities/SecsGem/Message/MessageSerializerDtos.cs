using PF.Core.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PF.Core.Entities.SecsGem.Message
{

    /// <summary>
    /// SFCommand数据库DTO
    /// </summary>
    public class SFCommandDbDto
    {
        /// <summary>Stream编号</summary>
        public uint Stream { get; set; }
        /// <summary>Function编号</summary>
        public uint Function { get; set; }
        /// <summary>命令名称</summary>
        public string Name { get; set; }
        /// <summary>命令ID</summary>
        public string ID { get; set; }
        /// <summary>命令键值</summary>
        public string Key { get; set; }
        /// <summary>关联消息DTO</summary>
        public SecsGemMessageDbDto Message { get; set; }
        /// <summary>创建时间</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>更新时间</summary>
        public DateTime? UpdatedAt { get; set; }


    }


    /// <summary>
    /// SecsGem消息数据库DTO
    /// </summary>
    public class SecsGemMessageDbDto
    {
        /// <summary>Stream编号</summary>
        public int Stream { get; set; }
        /// <summary>系统字节</summary>
        public string SystemBytes { get; set; }
        /// <summary>Function编号</summary>
        public int Function { get; set; }
        /// <summary>链接编号</summary>
        public int LinkNumber { get; set; }
        /// <summary>等待位</summary>
        public bool WBit { get; set; }
        /// <summary>根节点</summary>
        public SecsGemNodeMessageDbDto RootNode { get; set; }
        /// <summary>消息ID</summary>
        public string MessageId { get; set; }
        /// <summary>消息深度</summary>
        public int Depth { get; set; }
    }

    /// <summary>
    /// SecsGem节点消息数据库DTO
    /// </summary>
    public class SecsGemNodeMessageDbDto
    {
        /// <summary>数据类型</summary>
        public DataType DataType { get; set; }
        /// <summary>数据（Base64编码）</summary>
        public string Data { get; set; }
        /// <summary>数据长度</summary>
        public int Length { get; set; }
        /// <summary>子节点列表</summary>
        public List<SecsGemNodeMessageDbDto> SubNode { get; set; }
        /// <summary>是否为变量节点</summary>
        public bool IsVariableNode { get; set; }
        /// <summary>变量编号</summary>
        public uint VariableCode { get; set; }
        /// <summary>类型化值</summary>
        public string TypedValue { get; set; }
        /// <summary>十六进制数据表示</summary>
        public string DataHex { get; set; }

        /// <summary>节点路径</summary>
        public string NodePath { get; set; }
    }


    /// <summary>
    /// JSON序列化选项
    /// </summary>
    public static class JsonOptions
    {
        /// <summary>数据库序列化选项</summary>
        public static readonly JsonSerializerOptions DatabaseOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            Converters = {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        },
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        /// <summary>类型化值序列化选项</summary>
        public static readonly JsonSerializerOptions TypedValueOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
}
