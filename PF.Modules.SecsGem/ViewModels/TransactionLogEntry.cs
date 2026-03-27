using System;
using System.Windows.Media;

namespace PF.Modules.SecsGem.ViewModels
{
    /// <summary>
    /// 实时通信日志条目（双向报文展示：Raw Hex + SML 结构化文本）
    /// </summary>
    public class TransactionLogEntry
    {
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 方向箭头: "→" 发送 / "←" 接收
        /// </summary>
        public string Direction { get; set; }

        /// <summary>
        /// S{Stream}F{Function} [W]
        /// </summary>
        public string Header { get; set; }

        /// <summary>
        /// SystemBytes + 原始数据的十六进制字符串
        /// </summary>
        public string RawHex { get; set; }

        /// <summary>
        /// 格式化的 SML 结构文本
        /// </summary>
        public string SmlText { get; set; }

        public bool IsIncoming { get; set; }

        /// <summary>
        /// 时间戳 + 方向 + 报文头的单行摘要（用于日志列表标题列）
        /// </summary>
        public string TimestampHeader => $"[{Timestamp:HH:mm:ss.fff}] {Direction} {Header}";

        /// <summary>
        /// 发送用蓝色，接收用绿色
        /// </summary>
        public string DirectionColor => IsIncoming ? "#388E3C" : "#1565C0";
    }
}
