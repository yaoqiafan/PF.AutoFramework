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


        /// <summary>创建二进制节点</summary>
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

        /// <summary>创建I4类型节点</summary>
        public static SecsGemNodeMessage CreateI4Node(int value)
        {
            byte[] data = ToBigEndian(BitConverter.GetBytes(value));
            return new SecsGemNodeMessage
            {
                DataType = DataType.I4,
                Data = data,
                Length = 4,
                TypedValue = value // 提前填充强类型值
            };
        }

        /// <summary>创建U2类型节点</summary>
        public static SecsGemNodeMessage CreateU2Node(ushort value)
        {
            byte[] data = ToBigEndian(BitConverter.GetBytes(value));
            return new SecsGemNodeMessage
            {
                DataType = DataType.U2,
                Data = data,
                Length = 2,
                TypedValue = value // 提前填充强类型值
            };
        }

        /// <summary>创建U4类型节点</summary>
        public static SecsGemNodeMessage CreateU4Node(uint value)
        {
            byte[] data = ToBigEndian(BitConverter.GetBytes(value));
            return new SecsGemNodeMessage
            {
                DataType = DataType.U4,
                Data = data,
                Length = 4,
                TypedValue = value // 提前填充强类型值
            };
        }

        /// <summary>创建U8类型节点</summary>
        public static SecsGemNodeMessage CreateU8Node(ulong value)
        {
            byte[] data = ToBigEndian(BitConverter.GetBytes(value));
            return new SecsGemNodeMessage
            {
                DataType = DataType.U8,
                Data = data,
                Length = 8,
                TypedValue = value // 提前填充强类型值
            };
        }

        /// <summary>创建F4类型节点</summary>
        public static SecsGemNodeMessage CreateF4Node(float value)
        {
            byte[] data = ToBigEndian(BitConverter.GetBytes(value));
            return new SecsGemNodeMessage
            {
                DataType = DataType.F4,
                Data = data,
                Length = 4,
                TypedValue = value // 提前填充强类型值
            };
        }

        /// <summary>
        /// 主机序转大端序（SECS-II 线格式）；node.Data 按约定一律存大端字节
        /// </summary>
        private static byte[] ToBigEndian(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return bytes;
        }

        /// <summary>创建列表节点</summary>
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

        /// <summary>创建指定长度列表节点</summary>
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

        /// <summary>生成SECS二进制数据</summary>
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
        private static byte[] ConvertTobyteSecsNodeMessage(SecsGemNodeMessage node)
        {
            if (node == null)
                return Array.Empty<byte>();

            // node.Data 按约定已是大端序线格式，直接原样输出（与 SecsGemMessageProcessor.SerializeMessageNode 一致）
            byte[] dataPart;
            int length;

            if (node.DataType == DataType.LIST)
            {
                List<byte> subContent = new List<byte>();
                foreach (var subNode in node.SubNode)
                {
                    subContent.AddRange(ConvertTobyteSecsNodeMessage(subNode));
                }
                dataPart = subContent.ToArray();
                length = node.SubNode.Count; // LIST 的长度是元素个数
            }
            else
            {
                dataPart = node.Data ?? Array.Empty<byte>();
                length = dataPart.Length;
            }

            // 长度字段占用字节数：≤255 用 1 字节，≤65535 用 2 字节，≤16777215 用 3 字节
            if (length > 0xFFFFFF) throw new Exception($"传输数据长度有误（{length} 超过 SECS 单项上限 16777215），请确认");
            byte numLenBytes = (byte)(length > 0xFFFF ? 3 : (length > 0xFF ? 2 : 1));

            List<byte> nodeBytes = new List<byte>();
            // 类型字节：格式码 | 长度字节数
            nodeBytes.Add((byte)((byte)node.DataType | numLenBytes));
            // 长度字段（大端序）
            for (int i = numLenBytes - 1; i >= 0; i--)
            {
                nodeBytes.Add((byte)((length >> (i * 8)) & 0xFF));
            }
            // 数据
            nodeBytes.AddRange(dataPart);
            return nodeBytes.ToArray();
        }

        #endregion  消息生成（Message对象 → 字节数组）

        #region 辅助方法
        /// <summary>字节数组转十六进制字符串</summary>
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


        // SystemBytes 计数器：随机种子起点，避免进程重启后与上一轮在途事务撞号
        private static int _systemBytesCounter = new Random().Next();

        /// <summary>
        /// 生成SystemBytes（4字节，大端序）。
        /// 使用原子递增计数器保证并发下唯一；共享 Random 并发调用会损坏内部状态且可能撞号，不可用于此处。
        /// </summary>
        public static List<byte> GenerateSystemBytes()
        {
            uint next = (uint)System.Threading.Interlocked.Increment(ref _systemBytesCounter);
            byte[] bytes = BitConverter.GetBytes(next);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return bytes.ToList();
        }


        /// <summary>解析Stream/Function</summary>
        public static string[]? ParseSF(string input)
        {
            // 正则表达式匹配 S数字F数字 的格式
            var pattern = @"^S(\d+)F(\d+)$";
            var match = Regex.Match(input, pattern);

            if (match.Success)
            {
                return new string[] { match.Groups[1].Value, match.Groups[2].Value };
            }
            else
            {
                return null;
            }
        }




        #endregion


    }
}
