using PF.Core.Entities.SecsGem.Message;
using PF.Core.Enums;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.SecsGem.Tools
{



    /// <summary>
    /// SecsGem消息处理器，负责消息的序列化和反序列化
    /// </summary>
    public class SecsGemMessageProcessor
    {
        private readonly CategoryLogger _secsLogger;
        private const int HeaderLength = 10;
        private readonly Encoding _jis8Encoding;

        /// <summary>
        /// 构造消息处理器
        /// </summary>
        public SecsGemMessageProcessor(ILogService logService)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                _jis8Encoding = Encoding.GetEncoding("iso-2022-jp");
            }
            catch
            {
                _jis8Encoding = Encoding.ASCII;
            }
            _secsLogger = CategoryLoggerFactory.SecsGem(logService);
        }

        #region 消息生成
        /// <summary>
        /// 生成SECS消息字节数组
        /// </summary>
        public byte[] GenerateSecsBytes(SecsGemMessage secsMessage, byte[] deviceId, byte[] systemBytes)
        {
            try
            {
                if (secsMessage == null) throw new ArgumentNullException(nameof(secsMessage));

                // 1. 序列化消息体
                byte[] bodyBytes = secsMessage.RootNode != null ? SerializeMessageNode(secsMessage.RootNode) : Array.Empty<byte>();

                // 2. 计算长度：HSMS Header(10) + Body长度
                int totalLength = HeaderLength + bodyBytes.Length;
                byte[] lengthHeader = BitConverter.GetBytes(totalLength);
                if (BitConverter.IsLittleEndian) Array.Reverse(lengthHeader);

                // 3. 构建完整报文
                byte[] packet = new byte[4 + totalLength];

                // 前4字节：消息长度
                Buffer.BlockCopy(lengthHeader, 0, packet, 0, 4);

                // Header[0-1]: Device ID
                packet[4] = deviceId[0];
                packet[5] = deviceId[1];

                // Header[2]: Stream & W-Bit
                packet[6] = (byte)((secsMessage.Stream & 0x7F) | (secsMessage.WBit ? 0x80 : 0x00));

                // Header[3]: Function
                packet[7] = (byte)secsMessage.Function;

                // Header[4-5]: PType & SType (HSMS Data Message 为 0x00 0x00)
                packet[8] = 0x00;
                packet[9] = (byte)secsMessage.LinkNumber; // 通常为 SessionID/LinkNumber

                // Header[6-9]: System Bytes
                Buffer.BlockCopy(systemBytes, 0, packet, 10, 4);

                // Body
                if (bodyBytes.Length > 0)
                {
                    Buffer.BlockCopy(bodyBytes, 0, packet, 14, bodyBytes.Length);
                }

                return packet;
            }
            catch (Exception ex)
            {
                _secsLogger.Error($"生成SECS消息失败: S{secsMessage.Stream}F{secsMessage.Function}", ex);
                return null;
            }
        }

        private byte[] SerializeMessageNode(SecsGemNodeMessage node)
        {
            List<byte> bytes = new List<byte>();

            // 1. 处理长度字节数 (Length Byte Count: 1, 2, or 3)
            int length = 0;
            byte[] dataPart = Array.Empty<byte>();

            if (node.DataType == DataType.LIST)
            {
                List<byte> subContent = new List<byte>();
                foreach (var sub in node.SubNode) subContent.AddRange(SerializeMessageNode(sub));
                dataPart = subContent.ToArray();
                length = node.SubNode.Count; // List 长度是元素个数
            }
            else
            {
                dataPart = node.Data ?? Array.Empty<byte>();
                length = dataPart.Length;
            }

            // 决定长度占用几个字节
            byte numLenBytes = (byte)(length > 65535 ? 3 : (length > 255 ? 2 : 1));

            // 类型字节：类型码 | 长度字节数
            bytes.Add((byte)((byte)node.DataType | numLenBytes));

            // 写入长度 (大端序)
            for (int i = numLenBytes - 1; i >= 0; i--)
            {
                bytes.Add((byte)((length >> (i * 8)) & 0xFF));
            }

            // 写入数据
            bytes.AddRange(dataPart);
            return bytes.ToArray();
        }
        #endregion

        #region 消息解析
        /// <summary>
        /// 解析SECS消息字节数组
        /// </summary>
        public SecsGemMessage ParseSecsBytes(byte[] secsBytes)
        {
            try
            {
                if (secsBytes == null || secsBytes.Length < 14) throw new ArgumentException("消息长度不足");

                SecsGemMessage msg = new SecsGemMessage();

                // 1. 解析 Header (跳过前4字节长度位)
                // Stream & W-Bit 在 secsBytes[6]
                byte sWbit = secsBytes[6];
                msg.Stream = sWbit & 0x7F;
                msg.WBit = (sWbit & 0x80) == 0x80;

                // Function 在 secsBytes[7]
                msg.Function = secsBytes[7];

                // System Bytes 在 secsBytes[10-13]
                msg.SystemBytes = secsBytes.Skip(10).Take(4).ToList();

                // 2. 解析 Body (从第 14 字节开始)
                int offset = 14;
                if (secsBytes.Length > 14)
                {
                    msg.RootNode = DeserializeMessageNode(secsBytes, ref offset, depth: 0);
                }

                return msg;
            }
            catch (Exception ex)
            {
                _secsLogger.Error("解析SECS报文失败", ex);
                return null;
            }
        }

        //private SecsGemNodeMessage DeserializeMessageNode(byte[] bytes, ref int offset)
        //{
        //    if (offset >= bytes.Length) return null;

        //    SecsGemNodeMessage node = new SecsGemNodeMessage();
        //    byte formatByte = bytes[offset++];

        //    // 低2位是长度字节的个数
        //    int numLenBytes = formatByte & 0x03;
        //    node.DataType = (DataType)(formatByte & 0xFC);

        //    // 读取数据长度
        //    int dataLength = 0;
        //    for (int i = 0; i < numLenBytes; i++)
        //    {
        //        dataLength = (dataLength << 8) | bytes[offset++];
        //    }
        //    node.Length = dataLength;

        //    // 根据类型解析内容
        //    switch (node.DataType)
        //    {
        //        case DataType.LIST:
        //            node.TypedValue = node.SubNode;
        //            for (int i = 0; i < dataLength; i++)
        //            {
        //                node.SubNode.Add(DeserializeMessageNode(bytes, ref offset));
        //            }
        //            break;

        //        case DataType.ASCII:
        //            node.Data = ReadRawData(bytes, ref offset, dataLength);
        //            node.TypedValue = Encoding.ASCII.GetString(node.Data);
        //            break;

        //        case DataType.Binary:
        //            node.Data = ReadRawData(bytes, ref offset, dataLength);
        //            node.TypedValue = node.Data;
        //            break;

        //        case DataType.Boolean:
        //            node.Data = ReadRawData(bytes, ref offset, dataLength);
        //            node.TypedValue = dataLength > 0 && node.Data[0] == 0x01;
        //            break;

        //        case DataType.U4:
        //            node.Data = ReadRawData(bytes, ref offset, dataLength);
        //            if (dataLength == 4)
        //            {
        //                // 大端转小端
        //                byte[] temp = new byte[] { node.Data[3], node.Data[2], node.Data[1], node.Data[0] };
        //                node.TypedValue = BitConverter.ToUInt32(temp, 0);
        //            }
        //            break;

        //        // ... 其他类型 (I1, U1, I2, U2 等) 参考 U4 逻辑实现 ...
        //        default:
        //            node.Data = ReadRawData(bytes, ref offset, dataLength);
        //            break;
        //    }

        //    return node;
        //}

        //private byte[] ReadRawData(byte[] source, ref int offset, int length)
        //{
        //    if (length <= 0) return Array.Empty<byte>();
        //    byte[] data = new byte[length];
        //    Buffer.BlockCopy(source, offset, data, 0, length);
        //    offset += length;
        //    return data;
        //}




        /// <summary>
        /// 列表最大嵌套深度。SEMI 实际报文很少超过 5~6 层，64 层足够宽裕。
        /// 递归解析不限深度时，畸形报文（每层仅 2 字节的 L,1 嵌套）可在 10MB 内堆出数百万层，
        /// 触发 StackOverflowException——该异常不可捕获，直接终止进程（SECS 服务写协议日志时会解析每条 Host 报文）。
        /// </summary>
        private const int MaxListDepth = 64;

        private SecsGemNodeMessage DeserializeMessageNode(byte[] bytes, ref int offset, int depth)
        {
            if (offset >= bytes.Length) return null;
            if (depth > MaxListDepth)
                throw new FormatException($"SECS 报文列表嵌套超过 {MaxListDepth} 层，按畸形报文丢弃");

            SecsGemNodeMessage node = new SecsGemNodeMessage();

            // 1. 解析格式字节
            byte formatByte = bytes[offset++];
            // 低 2 位表示长度字节的个数 (1, 2, 或 3)
            int numLenBytes = formatByte & 0x03;
            // 高 6 位表示数据类型码
            node.DataType = (DataType)(formatByte & 0xFC);

            // 2. 读取数据长度 (Big-Endian)
            int dataLength = 0;
            for (int i = 0; i < numLenBytes; i++)
            {
                dataLength = (dataLength << 8) | bytes[offset++];
            }
            node.Length = dataLength;

            // 3. 根据类型解析内容
            switch (node.DataType)
            {
                case DataType.LIST:
                    node.TypedValue = node.SubNode;
                    // List 的 dataLength 表示子项目的个数。每个子项至少占 2 字节（格式字节 + 1 字节长度），
                    // 声明的个数超过剩余字节能容纳的上限即为畸形报文。此前不校验：读到末尾后每项返回 null，
                    // 照样循环最多约 1600 万次往列表里塞 null，下游遍历 SubNode 时 NRE
                    if (dataLength > (bytes.Length - offset) / 2 + 1)
                        throw new FormatException($"SECS 报文列表声明 {dataLength} 项，超出剩余报文长度，按畸形报文丢弃");
                    for (int i = 0; i < dataLength; i++)
                    {
                        var child = DeserializeMessageNode(bytes, ref offset, depth + 1)
                                    ?? throw new FormatException($"SECS 报文在列表第 {i + 1}/{dataLength} 项处提前结束，按畸形报文丢弃");
                        node.SubNode.Add(child);
                    }
                    break;

                case DataType.ASCII:
                case DataType.JIS8:
                    node.Data = ReadRawData(bytes, ref offset, dataLength);
                    // 根据 IRecipeService 可能需要的编码进行转换
                    node.TypedValue = (node.DataType == DataType.JIS8)
                        ? _jis8Encoding.GetString(node.Data)
                        : Encoding.ASCII.GetString(node.Data);
                    break;

                case DataType.Binary:
                    node.Data = ReadRawData(bytes, ref offset, dataLength);
                    node.TypedValue = node.Data; // 直接保存字节数组
                    break;

                case DataType.Boolean:
                    node.Data = ReadRawData(bytes, ref offset, dataLength);
                    if (dataLength == 1)
                        node.TypedValue = node.Data[0] != 0x00;
                    else
                        node.TypedValue = node.Data.Select(b => b != 0x00).ToArray();
                    break;

                // 1 字节整数
                case DataType.I1:
                    node.Data = ReadRawData(bytes, ref offset, dataLength);
                    node.TypedValue = dataLength == 1 ? (object)(sbyte)node.Data[0] : node.Data.Select(b => (sbyte)b).ToArray();
                    break;

                case DataType.U1:
                    node.Data = ReadRawData(bytes, ref offset, dataLength);
                    node.TypedValue = dataLength == 1 ? (object)node.Data[0] : node.Data;
                    break;

                // 2 字节整数 (Big-Endian)
                case DataType.I2:
                    node.Data = ReadRawData(bytes, ref offset, dataLength);
                    node.TypedValue = ParseNumbers<short>(node.Data, 2, b => BitConverter.ToInt16(ReverseIfLittle(b), 0));
                    break;

                case DataType.U2:
                    node.Data = ReadRawData(bytes, ref offset, dataLength);
                    node.TypedValue = ParseNumbers<ushort>(node.Data, 2, b => BitConverter.ToUInt16(ReverseIfLittle(b), 0));
                    break;

                // 4 字节整数
                case DataType.I4:
                    node.Data = ReadRawData(bytes, ref offset, dataLength);
                    node.TypedValue = ParseNumbers<int>(node.Data, 4, b => BitConverter.ToInt32(ReverseIfLittle(b), 0));
                    break;

                case DataType.U4:
                    node.Data = ReadRawData(bytes, ref offset, dataLength);
                    node.TypedValue = ParseNumbers<uint>(node.Data, 4, b => BitConverter.ToUInt32(ReverseIfLittle(b), 0));
                    break;

                // 8 字节整数
                case DataType.I8:
                    node.Data = ReadRawData(bytes, ref offset, dataLength);
                    node.TypedValue = ParseNumbers<long>(node.Data, 8, b => BitConverter.ToInt64(ReverseIfLittle(b), 0));
                    break;

                case DataType.U8:
                    node.Data = ReadRawData(bytes, ref offset, dataLength);
                    node.TypedValue = ParseNumbers<ulong>(node.Data, 8, b => BitConverter.ToUInt64(ReverseIfLittle(b), 0));
                    break;

                // 浮点数
                case DataType.F4:
                    node.Data = ReadRawData(bytes, ref offset, dataLength);
                    node.TypedValue = ParseNumbers<float>(node.Data, 4, b => BitConverter.ToSingle(ReverseIfLittle(b), 0));
                    break;

                case DataType.F8:
                    node.Data = ReadRawData(bytes, ref offset, dataLength);
                    node.TypedValue = ParseNumbers<double>(node.Data, 8, b => BitConverter.ToDouble(ReverseIfLittle(b), 0));
                    break;

                default:
                    _secsLogger.Error($"发现不支持的数据类型: {node.DataType}");
                    node.Data = ReadRawData(bytes, ref offset, dataLength);
                    break;
            }

            return node;
        }

        /// <summary>
        /// 通用数值解析辅助方法，支持单值和数组。
        /// 必须把每个元素拷贝出来再交给 converter（其内部会就地反转字节序），
        /// 否则会把 node.Data 里的大端原始字节破坏成小端，导致该节点再次序列化时数值翻转。
        /// </summary>
        private object ParseNumbers<T>(byte[] data, int elementSize, Func<byte[], T> converter)
        {
            int count = data.Length / elementSize;
            if (count == 0) return default(T);

            T[] result = new T[count];
            for (int i = 0; i < count; i++)
            {
                byte[] element = new byte[elementSize];
                Buffer.BlockCopy(data, i * elementSize, element, 0, elementSize);
                result[i] = converter(element);
            }
            return count == 1 ? (object)result[0] : result;
        }

        /// <summary>
        /// 处理大端序 (SECS 标准) 到主机序 (C#) 的转换
        /// </summary>
        private byte[] ReverseIfLittle(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return bytes;
        }

        private byte[] ReadRawData(byte[] source, ref int offset, int length)
        {
            if (length <= 0) return Array.Empty<byte>();
            byte[] data = new byte[length];
            Buffer.BlockCopy(source, offset, data, 0, length);
            offset += length;
            return data;
        }
        #endregion
    }
}
