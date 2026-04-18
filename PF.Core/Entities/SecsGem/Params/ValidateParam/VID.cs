using PF.Core.Entities.SecsGem.Params.ValidateParam.Base;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataType = PF.Core.Enums.DataType;

namespace PF.Core.Entities.SecsGem.Params.ValidateParam
{
    /// <summary>
    /// 变量标识（Variable ID）
    /// </summary>
    public class VID : IDBase
    {
        /// <summary>
        /// 初始化VID
        /// </summary>
        public VID(uint _ID, string _Description, DataType dataType)
            : base(_ID, _Description)
        {
            DataType = dataType;
        }

        /// <summary>数据类型</summary>
        public DataType DataType { get; set; }
        /// <summary>当前值</summary>
        public object? Value { get; set; }

        /// <summary>
        /// 获取强类型值
        /// </summary>
        public T? GetValue<T>()
        {
            if (Value == null) return default;
            try
            {
                return (T)Convert.ChangeType(Value, typeof(T));
            }
            catch
            {
                return default;
            }
        }


        /// <summary>
        /// 设置值，根据数据类型自动转换
        /// </summary>
        public bool SetValue(object value)
        {
            Type type = default;
            try
            {
                switch (DataType)
                {
                    case DataType.LIST:
                        type = typeof(Array);
                        break;
                    case DataType.Binary:
                        if (value is string a)
                        {
                           Value = Encoding.UTF8.GetBytes(value?.ToString ());
                            return true;
                        }


                        type = typeof(byte[]);
                        break;
                    case DataType.Boolean:
                        type = typeof(bool);
                        break;
                    case DataType.ASCII:
                        type = typeof(string);
                        break;
                    case DataType.JIS8:
                        type = typeof(string);
                        break;
                    case DataType.CHARACTER_2:
                        type = typeof(string);
                        break;
                    case DataType.I8:
                        type = typeof(Int128);
                        break;
                    case DataType.I1:
                        type = typeof(short);
                        break;
                    case DataType.I2:
                        type = typeof(int);
                        break;
                    case DataType.I4:
                        type = typeof(long);
                        break;
                    case DataType.F8:
                        type = typeof(double);
                        break;
                    case DataType.F4:
                        type = typeof(float);
                        break;
                    case DataType.U8:
                        type = typeof(UInt128);
                        break;
                    case DataType.U1:
                        type = typeof(ushort);
                        break;
                    case DataType.U2:
                        type = typeof(uint);
                        break;
                    case DataType.U4:
                        type = typeof(ulong);
                        break;
                    default:
                        break;
                }
                Value = Convert.ChangeType(value, type);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
