using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Enums
{
    /// <summary>
    /// 数据类型枚举
    /// </summary>
    public enum DataType
    {
        LIST = 0B00000000,      // L - 列表类型
        Binary = 0b00100000,    // B - 二进制类型
        Boolean = 0b00100100,   // BOOLEAN - 布尔类型
        ASCII = 0b01000000,     // A - ASCII字符串
        JIS8 = 0b01000100,// J - JIS8字符串
        CHARACTER_2 = 0b01001000,
        I8 = 0b01100000,        // I8 - 8字节有符号整数
        I1 = 6,        // I1 - 1字节有符号整数
        I2 = 0b011000100,        // I2 - 2字节有符号整数
        I4 = 0b01110000,        // I4 - 4字节有符号整数
        F8 = 0b10000000,        // F8 - 8字节浮点数
        F4 = 0b10010000,       // F4 - 4字节浮点数
        U8 = 0b10100000,       // U8 - 8字节无符号整数
        U1 = 0b10100100,       // U1 - 1字节无符号整数
        U2 = 0b10101000,       // U2 - 2字节无符号整数
        U4 = 0b10110000        // U4 - 4字节无符号整数
    }



    /// <summary>
    /// 错误信息枚举
    /// </summary>
    public enum SecsErrorCode
    {
        None = 0x00,
        数据长度错误 = 0x01,
    }

    /// <summary>
    /// SecsGem连接状态
    /// </summary>
    public enum SecsStatus
    {
       Connected =0x01,
       Disconnected=0x02,
    }

    public enum SecsDbSet
    {
        SystemConfigs,
        CommnadIDs,
        CEIDs,
        ReportIDs,
        VIDs,
        IncentiveCommands,
        ResponseCommands,
    }


    public static class SecsGemErrorCode
    {
        // 错误代码映射
        private static readonly Dictionary<byte, string> Errors = new Dictionary<byte, string>()
        {
            {0x00, "Abort Transaction"},
            {0x01, "Unrecognized Device ID"},
            {0x03, "Unrecognized Stream Type"},
            {0x05, "Unrecognized Function Type"},
            {0x07, "Illegal Data"},
            {0x09, "Transaction Timer Timeout"},
        };

    }


}
