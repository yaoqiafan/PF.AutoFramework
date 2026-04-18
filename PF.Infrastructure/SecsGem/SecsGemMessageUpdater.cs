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
                    Console.WriteLine($"更新VID节点失败，VID ID: {vidId}, 错误: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 使用VID的信息更新节点
        /// </summary>
        private void UpdateNodeWithVID(SecsGemNodeMessage node, VID vid)
        {
            // 根据VID的DataType和Value创建新的节点值
            switch (vid.DataType)
            {
                case DataType.I4:
                    if (vid.Value is int i4Value)
                    {
                        node.Data = BitConverter.GetBytes(i4Value);
                        node.TypedValue = i4Value;
                        node.Length = 4;
                    }
                    else if (vid.Value != null)
                    {
                        // 尝试转换
                        if (int.TryParse(vid.Value.ToString(), out int intValue))
                        {
                            node.Data = BitConverter.GetBytes(intValue);
                            node.TypedValue = intValue;
                            node.Length = 4;
                        }
                    }
                    break;

                case DataType.U2:
                    if (vid.Value is ushort u2Value)
                    {
                        node.DataType = DataType.U2;
                        node.Data = BitConverter.GetBytes(u2Value);
                        node.TypedValue = u2Value;
                        node.Length = 2;
                    }
                    else if (vid.Value != null)
                    {
                        if (ushort.TryParse(vid.Value.ToString(), out ushort ushortValue))
                        {
                            node.DataType = DataType.U2;
                            node.Data = BitConverter.GetBytes(ushortValue);
                            node.TypedValue = ushortValue;
                            node.Length = 2;
                        }
                    }
                    break;

                case DataType.U4:
                    if (vid.Value is uint u4Value)
                    {
                        node.Data = BitConverter.GetBytes(u4Value);
                        node.TypedValue = u4Value;
                        node.Length = 4; // Bug2 Fix: 缺少 Length 赋值
                    }
                    else if (vid.Value != null)
                    {
                        if (uint.TryParse(vid.Value.ToString(), out uint uintValue))
                        {
                            node.Data = BitConverter.GetBytes(uintValue);
                            node.TypedValue = uintValue;
                            node.Length = 4; // Bug2 Fix: 缺少 Length 赋值
                        }
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
                    if (vid.Value is float f4Value)
                    {
                        node.Data = BitConverter.GetBytes(f4Value);
                        node.TypedValue = f4Value;
                        node.Length = 4; // Bug2 Fix: 缺少 Length 赋值
                    }
                    else if (vid.Value != null)
                    {
                        if (float.TryParse(vid.Value.ToString(), out float floatValue))
                        {
                            node.Data = BitConverter.GetBytes(floatValue);
                            node.TypedValue = floatValue;
                            node.Length = 4; // Bug2 Fix: 缺少 Length 赋值
                        }
                    }
                    break;

                case DataType.F8:
                    if (vid.Value is double f8Value)
                    {
                        node.Data = BitConverter.GetBytes(f8Value);
                        node.TypedValue = f8Value;
                        node.Length = 8; // Bug2 Fix: 缺少 Length 赋值
                    }
                    else if (vid.Value != null)
                    {
                        if (double.TryParse(vid.Value.ToString(), out double doubleValue))
                        {
                            node.Data = BitConverter.GetBytes(doubleValue);
                            node.TypedValue = doubleValue;
                            node.Length = 8; // Bug2 Fix: 缺少 Length 赋值
                        }
                    }
                    break;

                case DataType.Boolean:
                    if (vid.Value is bool boolValue)
                    {
                        node.Data = new byte[] { (byte)(boolValue ? 0x01 : 0x00) };
                        node.TypedValue = boolValue;
                    }
                    else if (vid.Value != null)
                    {
                        bool boolVal = Convert.ToBoolean(vid.Value);
                        node.Data = new byte[] { (byte)(boolVal ? 0x01 : 0x00) };
                        node.TypedValue = boolVal;
                    }
                    break;

                case DataType.Binary:
                    if (vid.Value is byte[] binaryValue)
                    {
                        node.Data = binaryValue;
                        node.TypedValue = binaryValue;
                    }
                    else if (vid.Value != null)
                    {
                        node.Data = new byte[] { 0X00 };
                        node.TypedValue = new byte[] { 0X00 };
                    }
                    break;

                default:
                    // 对于未明确处理的数据类型，尝试通用处理
                    if (vid.Value != null)
                    {
                        // 可以添加更多类型的处理逻辑
                        Console.WriteLine($"未处理的VID数据类型: {vid.DataType}, VID ID: {vid.ID}");
                    }
                    break;
            }
        }
    }
}
