using PF.Core.Entities.SecsGem.Message;
using PF.Core.Enums;
using System.Text.Json;

namespace PF.Core.Entities.SecsGem.Command
{
    internal static class DbDtoConverter
    {
        #region 转换为DTO的方法

        public static SFCommandDbDto ToDbDto(this SFCommand command, bool includeMetadata = true)
        {
            if (command == null) return null;

            var dto = new SFCommandDbDto
            {
                Stream = command.Stream,
                Function = command.Function,
                Name = command.Name,
                ID = command.ID,
                Key = command.Key, // 手动包含被JsonIgnore的属性
                Message = command.Message?.ToDbDto()
            };

            if (includeMetadata)
            {
                dto.CreatedAt = DateTime.UtcNow;
                dto.UpdatedAt = DateTime.UtcNow;
            }

            return dto;
        }

        public static SecsGemMessageDbDto ToDbDto(this SecsGemMessage message)
        {
            if (message == null) return null;

            var dto = new SecsGemMessageDbDto
            {
                Stream = message.Stream,
                SystemBytes = message.SystemBytes != null && message.SystemBytes.Count > 0
                    ? Convert.ToBase64String(message.SystemBytes.ToArray())
                    : null,
                Function = message.Function,
                LinkNumber = message.LinkNumber,
                WBit = message.WBit,
                RootNode = FromEntity(message.RootNode),
                MessageId = message.MessageId,
                Depth = CalculateMessageDepth(message.RootNode)
            };

            return dto;
        }

        public static SecsGemNodeMessageDbDto FromEntity(this SecsGemNodeMessage entity, string parentPath = "")
        {
            if (entity == null) return null;

            var nodeId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var currentPath = string.IsNullOrEmpty(parentPath)
                ? nodeId
                : $"{parentPath}.{nodeId}";

            var dto = new SecsGemNodeMessageDbDto
            {
                DataType = entity.DataType,
                Data = entity.Data != null && entity.Data.Length > 0
                    ? Convert.ToBase64String(entity.Data)
                    : null,
                DataHex = entity.Data != null && entity.Data.Length > 0
                    ? BitConverter.ToString(entity.Data).Replace("-", "")
                    : null,
                Length = entity.Length,
                IsVariableNode = entity.IsVariableNode,
                VariableCode = entity.VariableCode,
                TypedValue = SerializeTypedValue(entity.TypedValue),
                SubNode = entity.SubNode?.Select((node, index) =>
                    node.FromEntity($"{currentPath}.{index}")).ToList(),
                NodePath = currentPath
            };

            return dto;
        }

        #endregion

        #region 从DTO恢复实体的方法

        public static SFCommand ToEntity(this SFCommandDbDto dto)
        {
            if (dto == null) return null;

            return new SFCommand
            {
                Stream = dto.Stream,
                Function = dto.Function,
                Name = dto.Name,
                ID = dto.ID,
                Message = dto.Message?.ToEntity()
                // Key属性是只读的，会自动计算
            };
        }

        public static SecsGemMessage ToEntity(this SecsGemMessageDbDto dto)
        {
            if (dto == null) return null;

            return new SecsGemMessage
            {
                Stream = dto.Stream,
                SystemBytes = !string.IsNullOrEmpty(dto.SystemBytes)
                    ? new List<byte>(Convert.FromBase64String(dto.SystemBytes))
                    : new List<byte>(),
                Function = dto.Function,
                LinkNumber = dto.LinkNumber,
                WBit = dto.WBit,
                RootNode = dto.RootNode?.ToEntity(),
                MessageId = dto.MessageId
            };
        }

        public static SecsGemNodeMessage ToEntity(this SecsGemNodeMessageDbDto dto)
        {
            if (dto == null) return null;

            var entity = new SecsGemNodeMessage
            {
                DataType = dto.DataType,
                Data = !string.IsNullOrEmpty(dto.Data)
                    ? Convert.FromBase64String(dto.Data)
                    : null,
                Length = dto.Length,
                IsVariableNode = dto.IsVariableNode,
                VariableCode = dto.VariableCode,
                TypedValue = DeserializeTypedValue(dto.TypedValue, dto.DataType),
                SubNode = dto.SubNode?.Select(node => node.ToEntity()).ToList()
            };

            return entity;
        }

        #endregion

        #region 辅助方法

        private static string SerializeTypedValue(object typedValue)
        {
            if (typedValue == null) return null;

            try
            {
                // 特殊处理常见的数值类型和基础类型
                if (typedValue is string strValue) return JsonSerializer.Serialize(strValue);
                if (typedValue is byte[] byteArray) return JsonSerializer.Serialize(Convert.ToBase64String(byteArray));
                if (typedValue is List<byte> byteList) return JsonSerializer.Serialize(byteList.ToArray());
                if (typedValue is List<SecsGemNodeMessage> nodeList)
                {
                    return JsonSerializer.Serialize(nodeList.Select(n => n.FromEntity()).ToList(),
                        JsonOptions.TypedValueOptions);
                }

                // 对于其他类型，使用默认序列化
                return JsonSerializer.Serialize(typedValue, JsonOptions.TypedValueOptions);
            }
            catch (Exception ex)
            {
                // 记录日志，返回null
                Console.WriteLine($"序列化TypedValue失败: {ex.Message}");
                return null;
            }
        }

        private static object DeserializeTypedValue(string typedValueJson, DataType dataType)
        {
            if (string.IsNullOrEmpty(typedValueJson)) return null;

            try
            {
                switch (dataType)
                {
                    case DataType.Binary:
                        var base64Str = JsonSerializer.Deserialize<string>(typedValueJson);
                        return Convert.FromBase64String(base64Str);

                    case DataType.LIST:
                        var nodeList = JsonSerializer.Deserialize<List<SecsGemNodeMessageDbDto>>(typedValueJson);
                        return nodeList?.Select(n => n.ToEntity()).ToList();

                    case DataType.Boolean:
                        return JsonSerializer.Deserialize<bool>(typedValueJson);

                    case DataType.ASCII:
                    case DataType.JIS8:
                    case DataType.CHARACTER_2:
                        return JsonSerializer.Deserialize<string>(typedValueJson);

                    case DataType.I1:
                        return JsonSerializer.Deserialize<sbyte>(typedValueJson);

                    case DataType.I2:
                        return JsonSerializer.Deserialize<short>(typedValueJson);

                    case DataType.I4:
                        return JsonSerializer.Deserialize<int>(typedValueJson);

                    case DataType.I8:
                        return JsonSerializer.Deserialize<long>(typedValueJson);

                    case DataType.U1:
                        return JsonSerializer.Deserialize<byte>(typedValueJson);

                    case DataType.U2:
                        return JsonSerializer.Deserialize<ushort>(typedValueJson);

                    case DataType.U4:
                        return JsonSerializer.Deserialize<uint>(typedValueJson);

                    case DataType.U8:
                        return JsonSerializer.Deserialize<ulong>(typedValueJson);

                    case DataType.F4:
                        return JsonSerializer.Deserialize<float>(typedValueJson);

                    case DataType.F8:
                        return JsonSerializer.Deserialize<double>(typedValueJson);

                    default:
                        // 未知类型，尝试作为object反序列化
                        return JsonSerializer.Deserialize<object>(typedValueJson);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"反序列化TypedValue失败: {ex.Message}");
                return null;
            }
        }

        private static int CalculateMessageDepth(SecsGemNodeMessage rootNode)
        {
            if (rootNode == null) return 0;
            return CalculateNodeDepth(rootNode, 1);
        }

        private static int CalculateNodeDepth(SecsGemNodeMessage node, int currentDepth)
        {
            if (node.SubNode == null || node.SubNode.Count == 0)
                return currentDepth;

            int maxDepth = currentDepth;
            foreach (var child in node.SubNode)
            {
                int childDepth = CalculateNodeDepth(child, currentDepth + 1);
                if (childDepth > maxDepth)
                    maxDepth = childDepth;
            }

            return maxDepth;
        }

        #endregion

        #region 批量转换方法

        public static List<SFCommandDbDto> ToDbDtoList(this IEnumerable<SFCommand> commands)
        {
            return commands?.Select(c => c.ToDbDto()).ToList() ?? new List<SFCommandDbDto>();
        }

        public static List<SFCommand> ToEntityList(this IEnumerable<SFCommandDbDto> dtos)
        {
            return dtos?.Select(d => d.ToEntity()).ToList() ?? new List<SFCommand>();
        }

        #endregion
    }
}
