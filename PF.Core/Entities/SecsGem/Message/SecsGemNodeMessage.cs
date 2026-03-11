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
    /// �Զ���SECS��Ϣ�ڵ��ࣨ��չǿ����ֵ֧�֣�
    /// </summary>
    public class SecsGemNodeMessage
    {
        public SecsGemNodeMessage() { }

        public DataType DataType { get; set; }
        [JsonIgnore] // �����л�Data����
        public byte[] Data { get; set; }
        public int Length { get; set; }
        public List<SecsGemNodeMessage> SubNode { get; set; } = new List<SecsGemNodeMessage>();
        public bool IsVariableNode { get; set; }
        public uint VariableCode { get; set; }
        /// <summary>
        /// �������ǿ����ֵ���Զ���䣬ֱ��ʹ�ã�
        /// </summary>
        [JsonIgnore] // �����л�Data����
        public object TypedValue { get; set; }


        public SecsGemNodeMessage(DataType dataType, object _value)
        {
            switch (dataType)
            {
                case DataType.LIST:
                    if (_value is SecsGemNodeMessage[] subNodes)
                    {
                        DataType = DataType.LIST;
                        SubNode = subNodes.ToList();
                        Length = subNodes.Length;
                        TypedValue = subNodes.ToList();
                    }
                    else if (_value is int length)
                    {
                        DataType = DataType.LIST;
                        Length = length;
                    }
                    else if (_value is List<SecsGemNodeMessage> list)
                    {
                        DataType = DataType.LIST;
                        SubNode = list;
                        Length = list.Count;
                        TypedValue = list;
                    }
                    break;

                case DataType.Binary:
                    if (_value is byte[] binary)
                    {
                        DataType = DataType.Binary;
                        Data = binary;
                        Length = binary.Length;
                        TypedValue = ByteArrayToHexStringWithSeparator(binary);
                    }
                    else if (_value is List<byte> byteList)
                    {
                        DataType = DataType.Binary;
                        Data = byteList.ToArray();
                        Length = byteList.Count;
                        TypedValue = ByteArrayToHexStringWithSeparator( byteList.ToArray());
                    }
                    break;

                case DataType.Boolean:
                    if (_value is bool res)
                    {
                        byte[] boolean = new[] { (byte)(res ? 0x01 : 0x00) };
                        DataType = DataType.Boolean;
                        Data = boolean;
                        Length = 1;
                        TypedValue = res;
                    }
                    else if (_value is byte boolByte)
                    {
                        bool boolVal = boolByte != 0x00;
                        byte[] boolean = new[] { (byte)(boolVal ? 0x01 : 0x00) };
                        DataType = DataType.Boolean;
                        Data = boolean;
                        Length = 1;
                        TypedValue = boolVal;
                    }
                    break;

                case DataType.ASCII:
                    if (_value is string str)
                    {
                        byte[] ascii = Encoding.ASCII.GetBytes(str ?? string.Empty);
                        DataType = DataType.ASCII;
                        Data = ascii;
                        Length = ascii.Length;
                        TypedValue = str ?? string.Empty;
                    }
                    else if (_value is char[] chars)
                    {
                        string charStr = new string(chars);
                        byte[] ascii = Encoding.ASCII.GetBytes(charStr);
                        DataType = DataType.ASCII;
                        Data = ascii;
                        Length = ascii.Length;
                        TypedValue = charStr;
                    }
                    break;

                case DataType.JIS8:
                    // ������Shift-JIS���루�ձ���ҵ��׼��
                    if (_value is string jisStr)
                    {
                        try
                        {
                            Encoding jisEncoding = Encoding.GetEncoding(932); // Shift-JIS ����ҳ
                            byte[] jis8 = jisEncoding.GetBytes(jisStr ?? string.Empty);
                            DataType = DataType.JIS8;
                            Data = jis8;
                            Length = jis8.Length;
                            TypedValue = jisStr ?? string.Empty;
                        }
                        catch (Exception)
                        {
                            // ������벻֧�֣�ʹ��ASCII��Ϊ��
                            byte[] ascii = Encoding.ASCII.GetBytes(jisStr ?? string.Empty);
                            DataType = DataType.JIS8;
                            Data = ascii;
                            Length = ascii.Length;
                            TypedValue = jisStr ?? string.Empty;
                        }
                    }
                    break;

                case DataType.CHARACTER_2:
                    // �����������ַ����ַ���
                    if (_value is string str2)
                    {
                        byte[] char2 = new byte[2];
                        Encoding.ASCII.GetBytes(str2.PadRight(2, ' '), 0, Math.Min(2, str2.Length), char2, 0);
                        DataType = DataType.CHARACTER_2;
                        Data = char2;
                        Length = 2;
                        TypedValue = str2;
                    }
                    else if (_value is char[] charArray && charArray.Length >= 2)
                    {
                        byte[] char2 = Encoding.ASCII.GetBytes(new string(charArray, 0, 2));
                        DataType = DataType.CHARACTER_2;
                        Data = char2;
                        Length = 2;
                        TypedValue = new string(charArray, 0, 2);
                    }
                    break;

                case DataType.I8:
                    // ��������DataType������ȷ��I8��������U2
                    if (_value is long iI8)
                    {
                        byte[] i8 = BitConverter.GetBytes(iI8);
                        DataType = DataType.I8;  // ������Ӧ����I8������U2
                        Data = i8;
                        Length = 8;
                        TypedValue = iI8;
                    }
                    else if (_value is short shortVal)
                    {
                        // �����short��ת��Ϊlong
                        byte[] i8 = BitConverter.GetBytes((long)shortVal);
                        DataType = DataType.I8;
                        Data = i8;
                        Length = 8;
                        TypedValue = (long)shortVal;
                    }
                    break;

                case DataType.I1:
                    if (_value is sbyte iI1)
                    {
                        byte[] i1 = new byte[] { (byte)iI1 };
                        DataType = DataType.I1;
                        Data = i1;
                        Length = 1;
                        TypedValue = iI1;
                    }
                    else if (_value is byte byteVal)
                    {
                        // �����޷���byteתΪ�з���sbyte
                        sbyte signedByte = unchecked((sbyte)byteVal);
                        byte[] i1 = new byte[] { byteVal };
                        DataType = DataType.I1;
                        Data = i1;
                        Length = 1;
                        TypedValue = signedByte;
                    }
                    break;

                case DataType.I2:
                    if (_value is short iI2)
                    {
                        byte[] i2 = BitConverter.GetBytes(iI2);
                        DataType = DataType.I2;  // ������Ӧ����I2������U2
                        Data = i2;
                        Length = 2;
                        TypedValue = iI2;
                    }
                    break;

                case DataType.I4:
                    if (_value is int ii4)
                    {
                        byte[] i4 = BitConverter.GetBytes(ii4);
                        DataType = DataType.I4;
                        Data = i4;
                        Length = 4;
                        TypedValue = ii4;  // ������Ӧ����intֵ���������ֽ�����
                    }
                    break;

                case DataType.F8:
                    if (_value is double fF8)
                    {
                        byte[] f8 = BitConverter.GetBytes(fF8);
                        DataType = DataType.F8;
                        Data = f8;
                        Length = 8;
                        TypedValue = fF8;
                    }
                    break;

                case DataType.F4:
                    if (_value is float fF4)
                    {
                        byte[] f4 = BitConverter.GetBytes(fF4);
                        DataType = DataType.F4;
                        Data = f4;
                        Length = 4;
                        TypedValue = fF4;
                    }
                    break;

                case DataType.U8:
                    if (_value is ulong uU8)
                    {
                        byte[] u8 = BitConverter.GetBytes(uU8);
                        DataType = DataType.U8;
                        Data = u8;
                        Length = 8;
                        TypedValue = uU8;
                    }
                    break;

                case DataType.U1:
                    if (_value is byte uU1)
                    {
                        byte[] u1 = new byte[] { uU1 };
                        DataType = DataType.U1;
                        Data = u1;
                        Length = 1;
                        TypedValue = uU1;
                    }
                    else if (_value is sbyte signedByte)
                    {
                        // �����з���byteתΪ�޷���byte
                        byte unsignedByte = unchecked((byte)signedByte);
                        byte[] u1 = new byte[] { unsignedByte };
                        DataType = DataType.U1;
                        Data = u1;
                        Length = 1;
                        TypedValue = unsignedByte;
                    }
                    break;

                case DataType.U2:
                    byte[] u2 = BitConverter.GetBytes(Convert.ToUInt16(_value));
                    DataType = DataType.U2;
                    Data = u2;
                    Length = 2;
                    TypedValue = Convert.ToUInt16(_value);

                    break;

                case DataType.U4:
                    if (_value is uint uU4)
                    {
                        byte[] u4 = BitConverter.GetBytes(uU4);
                        DataType = DataType.U4;
                        Data = u4;
                        Length = 4;
                        TypedValue = uU4;
                    }
                    break;

                default:
                    // ����δ֪���͵�Ĭ�����
                    DataType = dataType;
                    if (_value is byte[] bytes)
                    {
                        Data = bytes;
                        Length = bytes.Length;
                        TypedValue = bytes;
                    }
                    break;
            }

            // ���������֤
            if (Data == null && SubNode == null && DataType != DataType.LIST)
            {
                throw new ArgumentException($"�޷�Ϊ���� {dataType} ���� SecsGemNodeMessage��ֵ���Ͳ�ƥ���δ����");
            }
        }


        private  string ByteArrayToHexStringWithSeparator(byte[] bytes, string separator = " ", bool upperCase = true)
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
