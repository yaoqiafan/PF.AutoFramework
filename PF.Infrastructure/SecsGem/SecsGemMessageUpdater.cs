using PF.Infrastructure.SecsGem.Tools;
using PF.Core.Interfaces.SecsGem.Params;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PF.Core.Entities.SecsGem.Message;
using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.Core.Enums;

namespace PF.Core.Interfaces.SecsGem
{
    /// <summary>
    /// SecsGem消息更新器
    /// </summary>
    public class SecsGemMessageUpdater: ISecsGemMessageUpdater
    {
        private readonly IParams paramConfig;

        /// <summary>
        /// 构造消息更新器
        /// </summary>
        public SecsGemMessageUpdater(IParams @params)
        {
            paramConfig = @params;
        }

        /// <summary>
        /// 更新消息中所有IsVariableNode为true的节点的值
        /// </summary>
        public void UpdateVariableNodesWithVIDValues(SecsGemMessage message)
        {
            if (message?.RootNode == null)
                return;

            var validate = paramConfig.GetParam<ValidateConfiguration>(ParamType.Validate);
            UpdateNodeRecursively(message.RootNode, validate);
        }

        /// <summary>
        /// 递归遍历所有节点，更新IsVariableNode为true的节点
        /// </summary>
        private void UpdateNodeRecursively(SecsGemNodeMessage node, ValidateConfiguration validate)
        {
            if (node == null)
                return;

            // 如果当前节点是变量节点，更新其值
            if (node.IsVariableNode)
            {
                UpdateVariableNodeValue(node, validate);
            }

            // 递归处理子节点
            if (node.SubNode != null && node.SubNode.Any())
            {
                foreach (var subNode in node.SubNode)
                {
                    UpdateNodeRecursively(subNode, validate);
                }
            }
        }

        /// <summary>
        /// 更新单个变量节点的值
        /// </summary>
        private void UpdateVariableNodeValue(SecsGemNodeMessage node, ValidateConfiguration validate)
        {
            if (node.VariableCode is uint vidId)
            {
                try
                {
                    // 通过VID ID获取VID对象
                    var vid = validate.GetVID(Convert.ToUInt32(vidId));
                    if (vid != null && vid.Value != null)
                    {
                        // 根据VID的DataType和Value更新节点
                        UpdateNodeWithVID(node, vid);
                    }
                }
                catch (Exception ex)
                {
                    // 记录日志或处理异常
                    SecsGemTrace.Warn($"更新VID节点失败，VID ID: {vidId}, 错误: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 使用VID的信息更新节点。
        /// node.Data 按约定必须写入大端序（SECS-II 线格式）字节，
        /// 序列化器会原样输出，直接写 BitConverter.GetBytes 的小端结果会导致数值字节序颠倒。
        /// </summary>
        private void UpdateNodeWithVID(SecsGemNodeMessage node, VID vid)
        {
            // 根据VID的DataType和Value创建新的节点值。
            // 每个成功写值的分支都必须同时把 node.DataType 改成 VID 的类型：此前只有 U2/ASCII 改了，
            // I4/U4/F4/F8/Boolean/Binary 只改数据——模板节点类型与 VID 不同时（如 ASCII 占位、VID 为 I4），
            // Host 收到"ASCII 类型、内容是 4 个二进制字节"的错误报文
            string text = vid.Value?.ToString() ?? string.Empty;
            switch (vid.DataType)
            {
                // 此前 I1/I2/I8/U1/U8 落到 default：只记一条警告，节点保留模板占位值，Host 收到的是假数据
                case DataType.I1:
                    if (sbyte.TryParse(text, out sbyte i1)) SetNode(node, DataType.I1, new[] { unchecked((byte)i1) }, i1);
                    break;
                case DataType.U1:
                    if (byte.TryParse(text, out byte u1)) SetNode(node, DataType.U1, new[] { u1 }, u1);
                    break;
                case DataType.I2:
                    if (short.TryParse(text, out short i2)) SetNode(node, DataType.I2, ToBigEndian(BitConverter.GetBytes(i2)), i2);
                    break;
                case DataType.I8:
                    if (long.TryParse(text, out long i8)) SetNode(node, DataType.I8, ToBigEndian(BitConverter.GetBytes(i8)), i8);
                    break;
                case DataType.U8:
                    if (ulong.TryParse(text, out ulong u8)) SetNode(node, DataType.U8, ToBigEndian(BitConverter.GetBytes(u8)), u8);
                    break;

                case DataType.I4:
                    if (vid.Value != null && int.TryParse(vid.Value.ToString(), out int intValue))
                    {
                        node.DataType = DataType.I4;
                        node.Data = ToBigEndian(BitConverter.GetBytes(intValue));
                        node.TypedValue = intValue;
                        node.Length = 4;
                    }
                    break;

                case DataType.U2:
                    if (vid.Value != null && ushort.TryParse(vid.Value.ToString(), out ushort ushortValue))
                    {
                        node.DataType = DataType.U2;
                        node.Data = ToBigEndian(BitConverter.GetBytes(ushortValue));
                        node.TypedValue = ushortValue;
                        node.Length = 2;
                    }
                    break;

                case DataType.U4:
                    if (vid.Value != null && uint.TryParse(vid.Value.ToString(), out uint uintValue))
                    {
                        node.DataType = DataType.U4;
                        node.Data = ToBigEndian(BitConverter.GetBytes(uintValue));
                        node.TypedValue = uintValue;
                        node.Length = 4;
                    }
                    break;

                case DataType.ASCII:
                    if (vid.Value is string asciiValue)
                    {
                        node.DataType = DataType.ASCII;
                        node.Data = Encoding.ASCII.GetBytes(asciiValue);
                        node.Length = node.Data.Length;
                        node.TypedValue = asciiValue;
                    }
                    break;

                case DataType.F4:
                    if (vid.Value != null && float.TryParse(vid.Value.ToString(), out float floatValue))
                    {
                        node.DataType = DataType.F4;
                        node.Data = ToBigEndian(BitConverter.GetBytes(floatValue));
                        node.TypedValue = floatValue;
                        node.Length = 4;
                    }
                    break;

                case DataType.F8:
                    if (vid.Value != null && double.TryParse(vid.Value.ToString(), out double doubleValue))
                    {
                        node.DataType = DataType.F8;
                        node.Data = ToBigEndian(BitConverter.GetBytes(doubleValue));
                        node.TypedValue = doubleValue;
                        node.Length = 8;
                    }
                    break;

                case DataType.Boolean:
                    if (vid.Value != null)
                    {
                        bool boolVal = vid.Value is bool b ? b : Convert.ToBoolean(vid.Value);
                        node.DataType = DataType.Boolean;
                        node.Data = new byte[] { (byte)(boolVal ? 0x01 : 0x00) };
                        node.TypedValue = boolVal;
                        node.Length = 1;
                    }
                    break;

                case DataType.Binary:
                    if (vid.Value is byte[] binaryValue)
                    {
                        node.DataType = DataType.Binary;
                        node.Data = binaryValue;
                        node.TypedValue = binaryValue;
                        node.Length = binaryValue.Length;
                    }
                    else if (vid.Value != null)
                    {
                        node.DataType = DataType.Binary;
                        node.Data = new byte[] { 0X00 };
                        node.TypedValue = new byte[] { 0X00 };
                        node.Length = 1;
                    }
                    break;

                default:
                    // 对于未明确处理的数据类型，尝试通用处理
                    if (vid.Value != null)
                    {
                        // 可以添加更多类型的处理逻辑
                        SecsGemTrace.Warn($"未处理的VID数据类型: {vid.DataType}, VID ID: {vid.ID}");
                    }
                    break;
            }
        }

        private static void SetNode(SecsGemNodeMessage node, DataType type, byte[] bigEndianData, object typedValue)
        {
            node.DataType = type;
            node.Data = bigEndianData;
            node.TypedValue = typedValue;
            node.Length = bigEndianData.Length;
        }

        /// <summary>
        /// 主机序转大端序（SECS-II 线格式）；就地转换并返回
        /// </summary>
        private static byte[] ToBigEndian(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return bytes;
        }
    }
}
