using PF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PF.Core.Entities.SecsGem.Message
{
    /// <summary>
    /// 自定义SECS消息节点类（扩展强类型值支持）。
    /// 约定：<see cref="Data"/> 一律存放大端序（SECS-II 线格式）字节，
    /// 序列化器直接原样输出，解析器保持原始字节不动。
    /// </summary>
    public class SecsGemNodeMessage
    {
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public SecsGemNodeMessage() { }

        /// <summary>数据类型</summary>
        public DataType DataType { get; set; }
        /// <summary>原始字节数据（大端序线格式）</summary>
        [JsonIgnore]
        public byte[] Data { get; set; }
        /// <summary>数据长度</summary>
        public int Length { get; set; }
        /// <summary>子节点列表</summary>
        public List<SecsGemNodeMessage> SubNode { get; set; } = new List<SecsGemNodeMessage>();
        /// <summary>是否为变量节点</summary>
        public bool IsVariableNode { get; set; }
        /// <summary>变量编号</summary>
        public uint VariableCode { get; set; }
        /// <summary>
        /// 解析后的强类型值（自动填充，直接使用）
        /// </summary>
        [JsonIgnore] // 不参与序列化
        public object TypedValue { get; set; }


        /// <summary>
        /// 根据数据类型和值构造节点消息。
        /// 数值类型接受任意可被 Convert 转换的输入（int/long/string 等），
        /// 统一转为目标宽度后以大端序写入 <see cref="Data"/>。
        /// </summary>
        public SecsGemNodeMessage(DataType dataType, object _value)
        {
            DataType = dataType;

            switch (dataType)
            {
                case DataType.LIST:
                    if (_value is SecsGemNodeMessage[] subNodes)
                    {
                        SubNode = subNodes.ToList();
                        Length = subNodes.Length;
                        TypedValue = SubNode;
                    }
                    else if (_value is int listLength)
                    {
                        Length = listLength;
                    }
                    else if (_value is List<SecsGemNodeMessage> list)
                    {
                        SubNode = list;
                        Length = list.Count;
                        TypedValue = list;
                    }
                    break;

                case DataType.Binary:
                    if (_value is byte[] binary)
                    {
                        Data = binary;
                        Length = binary.Length;
                        TypedValue = ByteArrayToHexStringWithSeparator(binary);
                    }
                    else if (_value is List<byte> byteList)
                    {
                        Data = byteList.ToArray();
                        Length = byteList.Count;
                        TypedValue = ByteArrayToHexStringWithSeparator(Data);
                    }
                    break;

                case DataType.Boolean:
                    {
                        bool boolVal = _value is byte b ? b != 0x00 : Convert.ToBoolean(_value);
                        Data = new[] { (byte)(boolVal ? 0x01 : 0x00) };
                        Length = 1;
                        TypedValue = boolVal;
                    }
                    break;

                case DataType.ASCII:
                    {
                        string str = _value is char[] chars ? new string(chars) : _value?.ToString() ?? string.Empty;
                        Data = Encoding.ASCII.GetBytes(str);
                        Length = Data.Length;
                        TypedValue = str;
                    }
                    break;

                case DataType.JIS8:
                    {
                        string jisStr = _value?.ToString() ?? string.Empty;
                        try
                        {
                            Encoding jisEncoding = Encoding.GetEncoding(932); // Shift-JIS 代码页
                            Data = jisEncoding.GetBytes(jisStr);
                        }
                        catch (Exception)
                        {
                            // 代码页不可用时降级为 ASCII
                            Data = Encoding.ASCII.GetBytes(jisStr);
                        }
                        Length = Data.Length;
                        TypedValue = jisStr;
                    }
                    break;

                case DataType.CHARACTER_2:
                    {
                        string str2 = (_value is char[] charArray ? new string(charArray) : _value?.ToString() ?? string.Empty)
                            .PadRight(2, ' ');
                        Data = Encoding.ASCII.GetBytes(str2.Substring(0, 2));
                        Length = 2;
                        TypedValue = str2.Substring(0, 2);
                    }
                    break;

                case DataType.I1:
                    {
                        sbyte v = _value is byte ub ? unchecked((sbyte)ub) : Convert.ToSByte(_value);
                        Data = new[] { unchecked((byte)v) };
                        Length = 1;
                        TypedValue = v;
                    }
                    break;

                case DataType.I2:
                    {
                        short v = Convert.ToInt16(_value);
                        Data = ToBigEndian(BitConverter.GetBytes(v));
                        Length = 2;
                        TypedValue = v;
                    }
                    break;

                case DataType.I4:
                    {
                        int v = Convert.ToInt32(_value);
                        Data = ToBigEndian(BitConverter.GetBytes(v));
                        Length = 4;
                        TypedValue = v;
                    }
                    break;

                case DataType.I8:
                    {
                        long v = Convert.ToInt64(_value);
                        Data = ToBigEndian(BitConverter.GetBytes(v));
                        Length = 8;
                        TypedValue = v;
                    }
                    break;

                case DataType.U1:
                    {
                        byte v = _value is sbyte sb ? unchecked((byte)sb) : Convert.ToByte(_value);
                        Data = new[] { v };
                        Length = 1;
                        TypedValue = v;
                    }
                    break;

                case DataType.U2:
                    {
                        ushort v = Convert.ToUInt16(_value);
                        Data = ToBigEndian(BitConverter.GetBytes(v));
                        Length = 2;
                        TypedValue = v;
                    }
                    break;

                case DataType.U4:
                    {
                        uint v = Convert.ToUInt32(_value);
                        Data = ToBigEndian(BitConverter.GetBytes(v));
                        Length = 4;
                        TypedValue = v;
                    }
                    break;

                case DataType.U8:
                    {
                        ulong v = Convert.ToUInt64(_value);
                        Data = ToBigEndian(BitConverter.GetBytes(v));
                        Length = 8;
                        TypedValue = v;
                    }
                    break;

                case DataType.F4:
                    {
                        float v = Convert.ToSingle(_value);
                        Data = ToBigEndian(BitConverter.GetBytes(v));
                        Length = 4;
                        TypedValue = v;
                    }
                    break;

                case DataType.F8:
                    {
                        double v = Convert.ToDouble(_value);
                        Data = ToBigEndian(BitConverter.GetBytes(v));
                        Length = 8;
                        TypedValue = v;
                    }
                    break;

                default:
                    // 未知类型只接受原始字节
                    if (_value is byte[] bytes)
                    {
                        Data = bytes;
                        Length = bytes.Length;
                        TypedValue = bytes;
                    }
                    break;
            }

            // 除 LIST 外，构造完必须有数据；否则说明传入的值类型与 dataType 不匹配
            if (DataType != DataType.LIST && Data == null)
            {
                throw new ArgumentException($"无法为类型 {dataType} 构造 SecsGemNodeMessage：值类型 {_value?.GetType().Name ?? "null"} 不匹配");
            }
        }

        /// <summary>
        /// 主机序转大端序（SECS-II 线格式）；直接在传入数组上就地转换并返回
        /// </summary>
        private static byte[] ToBigEndian(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return bytes;
        }

        private string ByteArrayToHexStringWithSeparator(byte[] bytes, string separator = " ", bool upperCase = true)
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
    }
}
