using PF.Core.Entities.SecsGem.Message;
using PF.Core.Enums;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.SecsGem.Tools
{
    /// <summary>
    /// 基于自定义Message类的SECS/GEM解析与生成器（含强类型解析）
    /// </summary>
    public  class SecsGemMessageProcessor
    {
        private  readonly ILogService _logService ;
        private static readonly object locker = new object();

        // SECS消息头固定长度（字节）：长度(4) + Stream/WBit(1) + Function(1) + 设备ID(2) + 会话ID(2)
        private const int HeaderLength = 10;

        // 静态编码对象（避免重复创建）
        private Encoding _jis8Encoding;

        public SecsGemMessageProcessor(ILogService logService)
        {
            // 注册JIS8编码（需安装System.Text.Encoding.CodePages NuGet包）
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                _jis8Encoding = Encoding.GetEncoding("iso-2022-jp");
            }
            catch
            {
                _jis8Encoding = Encoding.ASCII; // 降级处理
            }

            _logService= logService;
        }

        #region 消息生成（Message对象 → 字节数组）
        public byte[] GenerateSecsBytes(SecsGemMessage secsMessage, byte[] deviced, byte[] systembytes)
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
                    headerBytes.AddRange(systembytes);
                    // 组合头和体，返回完整消息
                    return headerBytes.ToArray();
                }
                else
                {
                    // 序列化消息体（RootNode → 字节数组）
                    byte[] bodyBytes = SerializeMessageNode(secsMessage.RootNode);

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
                    headerBytes.AddRange(systembytes);

                    // 组合头和体，返回完整消息
                    return headerBytes.Concat(bodyBytes).ToArray();
                }

            }
            catch (Exception ex)
            {
                _logService.Error($"生成SECS消息失败：Message对象信息Stream{secsMessage.Stream} Function{secsMessage.Function} ", exception: ex);
                return null;
            }



        }

        private byte[] SerializeMessageNode(SecsGemNodeMessage node)
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
                        nodeBytes.AddRange(SerializeMessageNode(subNode));
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
        #endregion

        #region 消息解析（字节数组 → Message对象 + 自动填充强类型值）
        public SecsGemMessage ParseSecsBytes(byte[] secsBytes)
        {

            try
            {
                // 基础验证
                if (secsBytes == null || secsBytes.Length < HeaderLength)
                {
                    throw new ArgumentException($"无效的SECS消息：  字节数组长度不足", nameof(secsBytes));
                }
                int offset = 0;
                SecsGemMessage secsMessage = new SecsGemMessage();
                // 1. 解析消息长度（前4字节，Big-Endian）
                byte[] lengthBytes = secsBytes.Skip(offset).Take(4).ToArray();
                Array.Reverse(lengthBytes);
                int totalLength = BitConverter.ToInt32(lengthBytes, 0);
                offset += 6;
                // 验证长度一致性
                if (totalLength != secsBytes.Length - 4)
                {
                    throw new ArgumentException($"消息长度不匹配：解析到长度{totalLength}，实际长度{secsBytes.Length}", nameof(secsBytes));
                }
                secsMessage.SystemBytes = secsBytes.Skip(10).Take(4).ToList();
                // 2. 解析Stream和WBit（第5字节）
                byte streamWBitByte = secsBytes[offset++];
                secsMessage.Stream = streamWBitByte & 0x7F; // 低7位=Stream
                secsMessage.WBit = (streamWBitByte & 0x80) == 0x80; // 最高位=WBit
                // 3. 解析Function（第6字节）
                secsMessage.Function = secsBytes[offset++];
                offset = 14;
                // 4. 解析消息体（剩余字节）+ 自动填充强类型值
                secsMessage.RootNode = DeserializeMessageNode(secsBytes, ref offset);
                return secsMessage;
            }
            catch (Exception ex)
            {
                _logService.Error($"解析SECS消息失败：字节信息{BitConverter.ToString(secsBytes)} ", exception: ex);
                return null;
            }

        }

        private SecsGemNodeMessage DeserializeMessageNode(byte[] bytes, ref int offset)
        {
            try
            {
                if (offset >= bytes.Length)
                    return new SecsGemNodeMessage();

                SecsGemNodeMessage node = new SecsGemNodeMessage();

                // 1. 解析数据类型
                byte b = bytes[offset++];
                byte type = (byte)(b & 0b11111100);
                node.DataType = (DataType)type;
                // 取出长度字节数

                switch (node.DataType)
                {
                    case DataType.LIST:
                        // 解析List元素个数（2字节Big-Endian）
                        byte temp_len = (byte)(b & 0b00000011);/// 后面有多个字节表示数据长度
                        // 这里表示数据字节数   可能最多3个字节
                        byte[] len_bytes = bytes.Skip(offset++).Take(temp_len).ToArray();
                        Array.Reverse(len_bytes);   // 如：01 02 03  -> 03 02 01
                        int len = 0;  // 4byte
                                      // 这里需要考虑三种不同长度字节的转换情况
                        for (int i = 0; i < temp_len; i++)
                        {
                            // 03  02 512    515   01 *256*256+515
                            // 字节数
                            len += (int)(len_bytes[i] * Math.Pow(256, i));// 当前节点有多少子项
                        }
                        //offset += temp_lenASCII;
                        node.Length = len;
                        node.TypedValue = node.SubNode; // List类型的TypedValue指向子节点列表
                        offset += (temp_len - 1);
                        // 递归解析子节点
                        for (int i = 0; i < len && offset < bytes.Length; i++)
                        {
                            node.SubNode.Add(DeserializeMessageNode(bytes, ref offset));
                        }
                        break;

                    case DataType.Boolean:
                        {
                            // Bug4 Fix: 从长度字节中读实际数据长度，支持 Boolean 数组
                            byte numLenB = (byte)(b & 0b00000011);
                            int dataLenB = ReadDataLength(bytes, ref offset, numLenB);
                            node.Length = dataLenB;
                            node.Data = bytes.Skip(offset).Take(dataLenB).ToArray();
                            node.TypedValue = dataLenB == 1
                                ? (object)(node.Data[0] == 0x01)
                                : node.Data.Select(x => x == 0x01).ToArray();
                            offset += dataLenB;
                            break;
                        }

                    case DataType.ASCII:
                        // 解析ASCII字符串
                        byte temp_lenASCII = (byte)(b & 0b00000011);/// 后面有多个字节表示数据长度
                        // 这里表示数据字节数   可能最多3个字节
                        byte[] len_bytesASCII = bytes.Skip(offset++).Take(temp_lenASCII).ToArray();
                        Array.Reverse(len_bytesASCII);   // 如：01 02 03  -> 03 02 01
                        int lenASCII = 0;  // 4byte
                                           // 这里需要考虑三种不同长度字节的转换情况
                        for (int i = 0; i < temp_lenASCII; i++)
                        {
                            // 03  02 512    515   01 *256*256+515
                            // 字节数
                            lenASCII += (int)(len_bytesASCII[i] * Math.Pow(256, i));// 当前节点有多少子项
                        }
                        //offset += temp_lenASCII;
                        node.Length = lenASCII;
                        offset += temp_lenASCII;
                        node.Data = bytes.Skip(offset -1).Take(lenASCII).ToArray();
                        node.TypedValue = Encoding.ASCII.GetString(node.Data);
                        //if (string .IsNullOrEmpty (  node .TypedValue.ToString ())  )
                        //{

                        //}
                        //// 自动转换为string
                        //if (node.Length == 0)
                        //{
                        //    lenASCII = -1;
                        //}
                        offset += lenASCII;
                        offset--;
                        break;

                    case DataType.JIS8:
                        // 解析JIS8字符串
                        byte temp_lenJIS8 = (byte)(b & 0b00000011);/// 后面有多个字节表示数据长度
                        // 这里表示数据字节数   可能最多3个字节
                        byte[] len_bytesJIS8 = bytes.Skip(offset++).Take(temp_lenJIS8).ToArray();
                        Array.Reverse(len_bytesJIS8);   // 如：01 02 03  -> 03 02 01
                        int lenJIS8 = 0;  // 4byte
                                          // 这里需要考虑三种不同长度字节的转换情况
                        for (int i = 0; i < temp_lenJIS8; i++)
                        {
                            // 03  02 512    515   01 *256*256+515
                            // 字节数
                            lenJIS8 += (int)(len_bytesJIS8[i] * Math.Pow(256, i));// 当前节点有多少子项
                        }
                        //offset += temp_lenASCII;
                        node.Length = lenJIS8;
                        offset += temp_lenJIS8;
                        node.Data = bytes.Skip(offset-1).Take(lenJIS8).ToArray();
                        node.TypedValue = _jis8Encoding.GetString(node.Data); // 自动转换为string
                        offset += lenJIS8;
                        offset--;
                        break;

                    case DataType.Binary:
                        // 解析二进制数据
                        byte temp_lenBinary = (byte)(b & 0b00000011);/// 后面有多个字节表示数据长度
                        // 这里表示数据字节数   可能最多3个字节
                        byte[] len_bytesBinary = bytes.Skip(offset++).Take(temp_lenBinary).ToArray();
                        Array.Reverse(len_bytesBinary);   // 如：01 02 03  -> 03 02 01
                        int lenBinary = 0;  // 4byte
                                            // 这里需要考虑三种不同长度字节的转换情况
                        for (int i = 0; i < temp_lenBinary; i++)
                        {
                            // 03  02 512    515   01 *256*256+515
                            // 字节数
                            lenBinary += (int)(len_bytesBinary[i] * Math.Pow(256, i));// 当前节点有多少子项
                        }
                        //offset += temp_lenASCII;
                        offset += temp_lenBinary;
                        node.Length = lenBinary;
                        node.Data = bytes.Skip(offset-1 ).Take(lenBinary).ToArray();
                       
                        offset += lenBinary;
                        offset--;
                        break;

                    case DataType.I1:
                        {
                            // Bug4 Fix: 读实际长度，支持 I1 数组
                            byte numLenI1 = (byte)(b & 0b00000011);
                            int dataLenI1 = ReadDataLength(bytes, ref offset, numLenI1);
                            node.Length = dataLenI1;
                            node.Data = bytes.Skip(offset).Take(dataLenI1).ToArray();
                            node.TypedValue = dataLenI1 == 1
                                ? (object)(sbyte)node.Data[0]
                                : node.Data.Select(x => (sbyte)x).ToArray();
                            offset += dataLenI1;
                            break;
                        }

                    case DataType.U1:
                        {
                            // Bug4 Fix: 读实际长度，支持 U1 数组
                            byte numLenU1 = (byte)(b & 0b00000011);
                            int dataLenU1 = ReadDataLength(bytes, ref offset, numLenU1);
                            node.Length = dataLenU1;
                            node.Data = bytes.Skip(offset).Take(dataLenU1).ToArray();
                            node.TypedValue = dataLenU1 == 1
                                ? (object)node.Data[0]
                                : node.Data.ToArray();
                            offset += dataLenU1;
                            break;
                        }

                    case DataType.I2:
                        {
                            // Bug4 Fix: 读实际长度，支持 I2 数组（原来只处理单值）
                            byte numLenI2 = (byte)(b & 0b00000011);
                            int dataLenI2 = ReadDataLength(bytes, ref offset, numLenI2);
                            node.Length = dataLenI2;
                            node.Data = bytes.Skip(offset).Take(dataLenI2).ToArray();
                            int countI2 = dataLenI2 / 2;
                            if (countI2 == 1)
                            {
                                node.TypedValue = BitConverter.ToInt16(new[] { node.Data[1], node.Data[0] }, 0);
                            }
                            else
                            {
                                var vals = new short[countI2];
                                for (int j = 0; j < countI2; j++)
                                    vals[j] = BitConverter.ToInt16(new[] { node.Data[j * 2 + 1], node.Data[j * 2] }, 0);
                                node.TypedValue = vals;
                            }
                            offset += dataLenI2;
                            break;
                        }

                    case DataType.U2:
                        {
                            // Bug4 Fix: 读实际长度，支持 U2 数组（原来只处理单值）
                            byte numLenU2 = (byte)(b & 0b00000011);
                            int dataLenU2 = ReadDataLength(bytes, ref offset, numLenU2);
                            node.Length = dataLenU2;
                            node.Data = bytes.Skip(offset).Take(dataLenU2).ToArray();
                            int countU2 = dataLenU2 / 2;
                            if (countU2 == 1)
                            {
                                node.TypedValue = BitConverter.ToUInt16(new[] { node.Data[1], node.Data[0] }, 0);
                            }
                            else
                            {
                                var vals = new ushort[countU2];
                                for (int j = 0; j < countU2; j++)
                                    vals[j] = BitConverter.ToUInt16(new[] { node.Data[j * 2 + 1], node.Data[j * 2] }, 0);
                                node.TypedValue = vals;
                            }
                            offset += dataLenU2;
                            break;
                        }

                    case DataType.I4:
                        {
                            // Bug4 Fix: 读实际长度，支持 I4 数组（原来只处理单值）
                            byte numLenI4 = (byte)(b & 0b00000011);
                            int dataLenI4 = ReadDataLength(bytes, ref offset, numLenI4);
                            node.Length = dataLenI4;
                            node.Data = bytes.Skip(offset).Take(dataLenI4).ToArray();
                            int countI4 = dataLenI4 / 4;
                            if (countI4 == 1)
                            {
                                node.TypedValue = BitConverter.ToInt32(new[] { node.Data[3], node.Data[2], node.Data[1], node.Data[0] }, 0);
                            }
                            else
                            {
                                var vals = new int[countI4];
                                for (int j = 0; j < countI4; j++)
                                    vals[j] = BitConverter.ToInt32(new[] { node.Data[j * 4 + 3], node.Data[j * 4 + 2], node.Data[j * 4 + 1], node.Data[j * 4] }, 0);
                                node.TypedValue = vals;
                            }
                            offset += dataLenI4;
                            break;
                        }

                    case DataType.U4:
                        {
                            // Bug4 Fix: 读实际长度，支持 U4 数组（原来只处理单值）
                            byte numLenU4 = (byte)(b & 0b00000011);
                            int dataLenU4 = ReadDataLength(bytes, ref offset, numLenU4);
                            node.Length = dataLenU4;
                            node.Data = bytes.Skip(offset).Take(dataLenU4).ToArray();
                            int countU4 = dataLenU4 / 4;
                            if (countU4 == 1)
                            {
                                node.TypedValue = BitConverter.ToUInt32(new[] { node.Data[3], node.Data[2], node.Data[1], node.Data[0] }, 0);
                            }
                            else
                            {
                                var vals = new uint[countU4];
                                for (int j = 0; j < countU4; j++)
                                    vals[j] = BitConverter.ToUInt32(new[] { node.Data[j * 4 + 3], node.Data[j * 4 + 2], node.Data[j * 4 + 1], node.Data[j * 4] }, 0);
                                node.TypedValue = vals;
                            }
                            offset += dataLenU4;
                            break;
                        }

                    case DataType.I8:
                        {
                            // Bug4 Fix: 读实际长度，支持 I8 数组（原来只处理单值）
                            byte numLenI8 = (byte)(b & 0b00000011);
                            int dataLenI8 = ReadDataLength(bytes, ref offset, numLenI8);
                            node.Length = dataLenI8;
                            node.Data = bytes.Skip(offset).Take(dataLenI8).ToArray();
                            int countI8 = dataLenI8 / 8;
                            if (countI8 == 1)
                            {
                                node.TypedValue = BitConverter.ToInt64(new[] { node.Data[7], node.Data[6], node.Data[5], node.Data[4], node.Data[3], node.Data[2], node.Data[1], node.Data[0] }, 0);
                            }
                            else
                            {
                                var vals = new long[countI8];
                                for (int j = 0; j < countI8; j++)
                                    vals[j] = BitConverter.ToInt64(new[] { node.Data[j*8+7], node.Data[j*8+6], node.Data[j*8+5], node.Data[j*8+4], node.Data[j*8+3], node.Data[j*8+2], node.Data[j*8+1], node.Data[j*8] }, 0);
                                node.TypedValue = vals;
                            }
                            offset += dataLenI8;
                            break;
                        }

                    case DataType.U8:
                        {
                            // Bug4 Fix: 读实际长度，支持 U8 数组（原来只处理单值）
                            byte numLenU8 = (byte)(b & 0b00000011);
                            int dataLenU8 = ReadDataLength(bytes, ref offset, numLenU8);
                            node.Length = dataLenU8;
                            node.Data = bytes.Skip(offset).Take(dataLenU8).ToArray();
                            int countU8 = dataLenU8 / 8;
                            if (countU8 == 1)
                            {
                                node.TypedValue = BitConverter.ToUInt64(new[] { node.Data[7], node.Data[6], node.Data[5], node.Data[4], node.Data[3], node.Data[2], node.Data[1], node.Data[0] }, 0);
                            }
                            else
                            {
                                var vals = new ulong[countU8];
                                for (int j = 0; j < countU8; j++)
                                    vals[j] = BitConverter.ToUInt64(new[] { node.Data[j*8+7], node.Data[j*8+6], node.Data[j*8+5], node.Data[j*8+4], node.Data[j*8+3], node.Data[j*8+2], node.Data[j*8+1], node.Data[j*8] }, 0);
                                node.TypedValue = vals;
                            }
                            offset += dataLenU8;
                            break;
                        }

                    case DataType.F4:
                        {
                            // Bug4 Fix: 读实际长度，支持 F4 数组（原来只处理单值）
                            byte numLenF4 = (byte)(b & 0b00000011);
                            int dataLenF4 = ReadDataLength(bytes, ref offset, numLenF4);
                            node.Length = dataLenF4;
                            node.Data = bytes.Skip(offset).Take(dataLenF4).ToArray();
                            int countF4 = dataLenF4 / 4;
                            if (countF4 == 1)
                            {
                                node.TypedValue = BitConverter.ToSingle(new[] { node.Data[3], node.Data[2], node.Data[1], node.Data[0] }, 0);
                            }
                            else
                            {
                                var vals = new float[countF4];
                                for (int j = 0; j < countF4; j++)
                                    vals[j] = BitConverter.ToSingle(new[] { node.Data[j*4+3], node.Data[j*4+2], node.Data[j*4+1], node.Data[j*4] }, 0);
                                node.TypedValue = vals;
                            }
                            offset += dataLenF4;
                            break;
                        }

                    case DataType.F8:
                        {
                            // Bug4 Fix: 读实际长度，支持 F8 数组（原来只处理单值）
                            byte numLenF8 = (byte)(b & 0b00000011);
                            int dataLenF8 = ReadDataLength(bytes, ref offset, numLenF8);
                            node.Length = dataLenF8;
                            node.Data = bytes.Skip(offset).Take(dataLenF8).ToArray();
                            int countF8 = dataLenF8 / 8;
                            if (countF8 == 1)
                            {
                                node.TypedValue = BitConverter.ToDouble(new[] { node.Data[7], node.Data[6], node.Data[5], node.Data[4], node.Data[3], node.Data[2], node.Data[1], node.Data[0] }, 0);
                            }
                            else
                            {
                                var vals = new double[countF8];
                                for (int j = 0; j < countF8; j++)
                                    vals[j] = BitConverter.ToDouble(new[] { node.Data[j*8+7], node.Data[j*8+6], node.Data[j*8+5], node.Data[j*8+4], node.Data[j*8+3], node.Data[j*8+2], node.Data[j*8+1], node.Data[j*8] }, 0);
                                node.TypedValue = vals;
                            }
                            offset += dataLenF8;
                            break;
                        }

                    default:
                        throw new NotSupportedException($"无法解析的SECS数据类型：{node.DataType}");
                }

                return node;
            }
            catch (Exception ex)
            {
                _logService.Error($"反序列化SECS消息节点失败：偏移量{offset}，字节信息{BitConverter.ToString(bytes)} ", exception: ex);
                return null;
            }
        }

        /// <summary>
        /// 读取 SECS 数据项的实际数据长度（Big-Endian，1~3 字节）。
        /// 读取后 offset 前进 numLenBytes 位，指向数据起始位置。
        /// </summary>
        private static int ReadDataLength(byte[] bytes, ref int offset, byte numLenBytes)
        {
            if (numLenBytes == 0) return 0;

            byte[] lenBytes = bytes.Skip(offset).Take(numLenBytes).ToArray();
            Array.Reverse(lenBytes); // Big-Endian → Little-Endian
            int dataLen = 0;
            for (int i = 0; i < numLenBytes; i++)
                dataLen += (int)(lenBytes[i] * Math.Pow(256, i));

            offset += numLenBytes;
            return dataLen;
        }

        #endregion



        #region 辅助方法：格式化输出（展示强类型值）
        public string FormatSecsMessage(SecsGemMessage message)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== SECS Message [ID: {message.MessageId}] ===");
            sb.AppendLine($"S{message.Stream}F{message.Function} (WBit: {message.WBit})");
            sb.AppendLine("Message Body:");
            sb.Append(FormatMessageNode(message.RootNode, 1));
            return sb.ToString();
        }

        private string FormatMessageNode(SecsGemNodeMessage node, int indentLevel)
        {
            if (node == null)
                return $"{new string(' ', indentLevel * 2)}null\n";

            string indent = new string(' ', indentLevel * 2);
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"{indent}Type: {node.DataType}, Length: {node.Length}");

            // 优先展示强类型值，更直观
            if (node.TypedValue != null)
            {
                sb.AppendLine($"{indent}TypedValue: {GetTypedValueString(node.TypedValue)}");
            }
            // 保留原始字节展示（可选）
            else if (node.Data != null && node.Data.Length > 0)
            {
                sb.AppendLine($"{indent}Data: {BitConverter.ToString(node.Data).Replace("-", " ")}");
            }

            if (node.SubNode.Count > 0)
            {
                sb.AppendLine($"{indent}SubNodes ({node.SubNode.Count}):");
                foreach (var subNode in node.SubNode)
                {
                    sb.Append(FormatMessageNode(subNode, indentLevel + 1));
                }
            }

            return sb.ToString();
        }

        // 格式化强类型值为可读字符串
        private string GetTypedValueString(object value)
        {
            if (value == null) return "null";

            if (value is List<SecsGemNodeMessage>)
            {
                return $"List[{((List<SecsGemNodeMessage>)value).Count}]";
            }

            if (value is byte[])
            {
                return $"Binary[{((byte[])value).Length}]";
            }

            return value.ToString();
        }
        #endregion

        #region 新增：安全获取强类型值（避免类型转换异常）
        /// <summary>
        /// 安全获取节点的强类型值（泛型方法，简化类型转换）
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="node">消息节点</param>
        /// <returns>强类型值，转换失败返回默认值</returns>
        public T GetTypedValue<T>(SecsGemNodeMessage node)
        {
            if (node == null || node.TypedValue == null)
                return default;

            try
            {
                return (T)Convert.ChangeType(node.TypedValue, typeof(T));
            }
            catch
            {
                return default;
            }
        }
        #endregion
    }
}
