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
        /// <summary>列表类型</summary>
        LIST = 0B00000000,
        /// <summary>二进制类型</summary>
        Binary = 0b00100000,
        /// <summary>布尔类型</summary>
        Boolean = 0b00100100,
        /// <summary>ASCII字符串</summary>
        ASCII = 0b01000000,
        /// <summary>JIS8字符串</summary>
        JIS8 = 0b01000100,
        /// <summary>2字节字符</summary>
        CHARACTER_2 = 0b01001000,
        /// <summary>8字节有符号整数</summary>
        I8 = 0b01100000,
        /// <summary>1字节有符号整数</summary>
        I1 = 6,
        /// <summary>2字节有符号整数</summary>
        I2 = 0b011000100,
        /// <summary>4字节有符号整数</summary>
        I4 = 0b01110000,
        /// <summary>8字节浮点数</summary>
        F8 = 0b10000000,
        /// <summary>4字节浮点数</summary>
        F4 = 0b10010000,
        /// <summary>8字节无符号整数</summary>
        U8 = 0b10100000,
        /// <summary>1字节无符号整数</summary>
        U1 = 0b10100100,
        /// <summary>2字节无符号整数</summary>
        U2 = 0b10101000,
        /// <summary>4字节无符号整数</summary>
        U4 = 0b10110000
    }



    /// <summary>
    /// 错误信息枚举
    /// </summary>
    public enum SecsErrorCode
    {
        /// <summary>无错误</summary>
        None = 0x00,
        /// <summary>数据长度错误</summary>
        数据长度错误 = 0x01,
    }

    /// <summary>
    /// SecsGem连接状态
    /// </summary>
    public enum SecsStatus
    {
        /// <summary>已连接</summary>
       Connected =0x01,
        /// <summary>已断开</summary>
       Disconnected=0x02,
    }

    /// <summary>
    /// SecsGem数据库表集合
    /// </summary>
    public enum SecsDbSet
    {
        /// <summary>系统配置</summary>
        SystemConfigs,
        /// <summary>命令ID</summary>
        CommnadIDs,
        /// <summary>事件ID</summary>
        CEIDs,
        /// <summary>报告ID</summary>
        ReportIDs,
        /// <summary>变量ID</summary>
        VIDs,
        /// <summary>主动命令</summary>
        IncentiveCommands,
        /// <summary>响应命令</summary>
        ResponseCommands,
    }


    /// <summary>
    /// SecsGem错误代码
    /// </summary>
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
