using NPOI.SS.Formula.Functions;
using PF.Core.Entities.SecsGem.Message;
using PF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PF.Infrastructure.SecsGem.Tools
{
    /// <summary>
    /// SecsGem消息工具类
    /// </summary>
    public static class SecsGemMessageTools
    {

        /// <summary>
        /// 随机数生成器
        /// </summary>
        public static Random SysRandom { get; set; } = new Random();


        #region 辅助方法：快速构建Message节点（自动填充TypedValue）
        /// <summary>
        /// 创建ASCII类型节点
        /// </summary>
        public static SecsGemNodeMessage CreateAsciiNode(string value)
        {
            byte[] data = Encoding.ASCII.GetBytes(value ?? string.Empty);
            return new SecsGemNodeMessage
            {
                DataType = DataType.ASCII,
                Data = data,
                Length = data.Length,
                TypedValue = value ?? string.Empty // 提前填充强类型值
            };
        }

        /// <summary>
        /// 创建Boolean类型节点
        /// </summary>
        public static SecsGemNodeMessage CreateBooleanNode(bool value)
        {
            byte[] data = new[] { (byte)(value ? 0x01 : 0x00) };
            return new SecsGemNodeMessage
            {
                DataType = DataType.Boolean,
                Data = data,
                Length = 1,
                TypedValue = value // 提前填充强类型值
            };
        }


        public static SecsGemNodeMessage CreateBinaryNode(byte[] value)
        {
            byte[] data = value;
            return new SecsGemNodeMessage
            {
                DataType = DataType.Binary,
                Data = data,
                Length = value.Length,
                TypedValue = value // 提前填充强类型值
            };
        }

        public static SecsGemNodeMessage CreateI4Node(int value)
        {
            byte[] data = BitConverter.GetBytes(value);
            return new SecsGemNodeMessage
            {
                DataType = DataType.I4,
                Data = data,
                Length = 4,
                TypedValue = value // 提前填充强类型值
            };
        }

        public static SecsGemNodeMessage CreateU2Node(ushort value)
        {
            byte[] data = BitConverter.GetBytes(value);
            return new SecsGemNodeMessage
            {
                DataType = DataType.U2,
                Data = data,
                Length = 2,
                TypedValue = value // 提前填充强类型值
            };
        }

        public static SecsGemNodeMessage CreateU4Node(uint value)
        {
            byte[] data = BitConverter.GetBytes(value);
            return new SecsGemNodeMessage
            {
                DataType = DataType.U4,
                Data = data,
                Length = 4,
                TypedValue = value // 提前填充强类型值
            };
        }

        public static SecsGemNodeMessage CreateU8Node(ulong value)
        {
            byte[] data = BitConverter.GetBytes(value);
            return new SecsGemNodeMessage
            {
                DataType = DataType.U8,
                Data = data,
                Length = 8,
                TypedValue = value // 提前填充强类型值
            };
        }

        public static SecsGemNodeMessage CreateF4Node(float value)
        {
            byte[] data = BitConverter.GetBytes(value);
            return new SecsGemNodeMessage
            {
                DataType = DataType.F4,
                Data = data,
                Length = 4,
                TypedValue = value // 提前填充强类型值
            };
        }

        public static SecsGemNodeMessage CreateListNode(params SecsGemNodeMessage[] subNodes)
        {
            return new SecsGemNodeMessage
            {
                DataType = DataType.LIST,
                SubNode = subNodes.ToList(),
                Length = subNodes.Length,
                TypedValue = subNodes.ToList() // List类型指向子节点列表
            };
        }

        public static SecsGemNodeMessage CreateListNode(int length)
        {
            return new SecsGemNodeMessage
            {
                DataType = DataType.LIST,
                Length = length,
            };
        }
        #endregion


        #region 消息生成（Message对象 → 字节数组）


        private const int HeaderLength = 10;

        public static  byte[] GenerateSecsBytes(SecsGemMessage secsMessage, byte[] deviced)
        {
            try
            {
                if (secsMessage == null)
                {
                    throw new ArgumentNullException(nameof(secsMessage), "SECS消息对象不能为空");
                }
                // 参数验证
                if (secsMessage.Stream < 0 || secsMessage.Stream > 255)
                {
                    throw new ArgumentOutOfRangeException(nameof(secsMessage.Stream), "Stream号必须在0-255之间");
                }

                if (secsMessage.Function < 0 || secsMessage.Function > 255)
                {
                    throw new ArgumentOutOfRangeException(nameof(secsMessage.Function), "Function号必须在0-255之间");
                }
                if (secsMessage.RootNode == null)
                {
                    // 计算消息总长度（头 + 体）
                    int totalLength = HeaderLength;
                    // 构建消息头
                    List<byte> headerBytes = new List<byte>();
                    // 1. 长度字段（4字节，Big-Endian）
                    headerBytes.AddRange(BitConverter.GetBytes(totalLength).Reverse());
                    headerBytes.AddRange(deviced);
                    // 2. Stream + WBit（1字节：最高位=WBit，低7位=Stream）
                    byte streamByte = (byte)((secsMessage.Stream & 0x7F) | (secsMessage.WBit ? 0x80 : 0x00));
                    headerBytes.Add(streamByte);
                    // 3. Function字段（1字节）
                    headerBytes.Add((byte)secsMessage.Function);
                    // 5. 会话ID（2字节，默认0）
                    headerBytes.AddRange(new byte[] { 0x00 });
                    headerBytes.Add((byte)secsMessage.LinkNumber);
                    headerBytes.AddRange(secsMessage .SystemBytes );
                    // 组合头和体，返回完整消息
                    return headerBytes.ToArray();
                }
                else
                {
                    // 序列化消息体（RootNode → 字节数组）
                    byte[] bodyBytes = ConvertTobyteSecsNodeMessage(secsMessage.RootNode);

                    // 计算消息总长度（头 + 体）
                    int totalLength = HeaderLength + bodyBytes.Length;

                    // 构建消息头
                    List<byte> headerBytes = new List<byte>();

                    // 1. 长度字段（4字节，Big-Endian）
                    headerBytes.AddRange(BitConverter.GetBytes(totalLength).Reverse());

                    headerBytes.AddRange(deviced);

                    // 2. Stream + WBit（1字节：最高位=WBit，低7位=Stream）
                    byte streamByte = (byte)((secsMessage.Stream & 0x7F) | (secsMessage.WBit ? 0x80 : 0x00));
                    headerBytes.Add(streamByte);

                    // 3. Function字段（1字节）
                    headerBytes.Add((byte)secsMessage.Function);
                    // 5. 会话ID（2字节，默认0）
                    headerBytes.AddRange(new byte[] { 0x00 });
                    headerBytes.Add((byte)secsMessage.LinkNumber);
                    headerBytes.AddRange(secsMessage.SystemBytes);

                    // 组合头和体，返回完整消息
                    return headerBytes.Concat(bodyBytes).ToArray();
                }

            }
            catch (Exception ex)
            {
                //PFLog.LogMgr.Instance.LogSystem($"生成SECS消息失败：Message对象信息Stream{secsMessage.Stream} Function{secsMessage.Function} ", ex);
                return null;
            }



        }
        private static  byte[] ConvertTobyteSecsNodeMessage(SecsGemNodeMessage  node)
        {
            if (node == null)
                return Array.Empty<byte>();

            List<byte> nodeBytes = new List<byte>();

            // 1. 写入数据类型标识（1字节）
            //nodeBytes.Add((byte)node.DataType);
            int len_count = Math.Max(1, (int)Math.Ceiling(node.Length * 1.0 / 256));
            if (len_count > 3) throw new Exception("传输数据长度有误，请确认");
            byte data_type = (byte)((byte)node.DataType | len_count);
            byte[] len_bytes = BitConverter.GetBytes(node.Length);
            Array.Reverse(len_bytes);
            nodeBytes.Add(data_type);// 类型

            for (int i = 4 - len_count; i < 4; i++)
            {
                nodeBytes.Add(len_bytes[i]);// 长度
            }
            switch (node.DataType)
            {
                case DataType.LIST:

                    foreach (var subNode in node.SubNode)
                    {
                        nodeBytes.AddRange(ConvertTobyteSecsNodeMessage(subNode));
                    }
                    // List类型：先写元素个数（2字节Big-Endian），再递归序列化子节点

                    break;

                case DataType.Boolean:
                    // Boolean类型：1字节（0x00=false，0x01=true）
                    nodeBytes.Add(node.Data?.Length > 0 ? node.Data[0] : (byte)0x00);
                    break;

                case DataType.ASCII:
                case DataType.JIS8:
                case DataType.Binary:
                    // 字符串/二进制类型：先写长度（2字节Big-Endian），再写数据
                    if (node.Data != null && node.Data.Length > 0)
                    {
                        nodeBytes.AddRange(node.Data);
                    }
                    break;

                case DataType.I1:
                case DataType.U1:
                    // 1字节整数：直接写数据
                    nodeBytes.Add(node.Data?.Length > 0 ? node.Data[0] : (byte)0x00);
                    break;

                case DataType.I2:
                case DataType.U2:
                    // 2字节整数（Big-Endian）
                    byte[] i2Bytes = node.Data?.Length >= 2 ? new[] { node.Data[1], node.Data[0] } : BitConverter.GetBytes((short)0).Reverse().ToArray();
                    nodeBytes.AddRange(i2Bytes);
                    break;

                case DataType.I4:
                case DataType.U4:
                    // 4字节整数（Big-Endian）
                    byte[] i4Bytes = node.Data?.Length >= 4 ? new[] { node.Data[3], node.Data[2], node.Data[1], node.Data[0] } : BitConverter.GetBytes((int)0).Reverse().ToArray();
                    nodeBytes.AddRange(i4Bytes);
                    break;

                case DataType.I8:
                case DataType.U8:
                    // 8字节整数（Big-Endian）
                    byte[] i8Bytes = node.Data?.Length >= 8 ? new[] { node.Data[7], node.Data[6], node.Data[5], node.Data[4], node.Data[3], node.Data[2], node.Data[1], node.Data[0] } : BitConverter.GetBytes((long)0).Reverse().ToArray();
                    nodeBytes.AddRange(i8Bytes);
                    break;

                case DataType.F4:
                    // 4字节浮点数（Big-Endian）
                    byte[] f4Bytes = node.Data?.Length >= 4 ? new[] { node.Data[3], node.Data[2], node.Data[1], node.Data[0] } : BitConverter.GetBytes((float)0).Reverse().ToArray();
                    nodeBytes.AddRange(f4Bytes);
                    break;

                case DataType.F8:
                    // 8字节浮点数（Big-Endian）
                    byte[] f8Bytes = node.Data?.Length >= 8 ? new[] { node.Data[7], node.Data[6], node.Data[5], node.Data[4], node.Data[3], node.Data[2], node.Data[1], node.Data[0] } : BitConverter.GetBytes((double)0).Reverse().ToArray();
                    nodeBytes.AddRange(f8Bytes);
                    break;

                default:
                    throw new NotSupportedException($"不支持的SECS数据类型：{node.DataType}");
            }

            return nodeBytes.ToArray();
        }

        #endregion  消息生成（Message对象 → 字节数组）

        #region 辅助方法
        public static string ByteArrayToHexStringWithSeparator(byte[] bytes, string separator = " ", bool upperCase = true)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            string format = upperCase ? "X2" : "x2";

            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString(format));
                if (i < bytes.Length - 1)
                {
                    sb.Append(separator);
                }
            }

            return sb.ToString();
        }


        /// <summary>
        /// 生成SystemBytes（4字节）
        /// </summary>
        public static List<byte> GenerateSystemBytes()
        {
            return new List<byte>
            {
                (byte)SysRandom.Next(0, 255),
                (byte)SysRandom.Next(0, 255),
                (byte)SysRandom.Next(0, 255),
                (byte)SysRandom.Next(0, 255)
            };
        }


        public static string[]? ParseSF(string input)
        {
            // 正则表达式匹配 S数字F数字 的格式
            var pattern = @"^S(\d+)F(\d+)$";
            var match = Regex.Match(input, pattern);

            if (match.Success)
            {
                string a = match.Groups[1].Value;
                string b = match.Groups[2].Value;

                Console.WriteLine($"a = {a}");
                Console.WriteLine($"b = {b}");

                // 如果需要转换为整数
                int aInt = int.Parse(a);
                int bInt = int.Parse(b);

                return new string[] { a, b };
            }
            else
            {
                return null;
            }
        }




        #endregion


    }
}
