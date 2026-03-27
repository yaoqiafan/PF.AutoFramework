using PF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.SecsGem.Message
{
    public class SecsGemMessage
    {
        /// <summary>
        /// Stream号（S）
        /// </summary>
        public int Stream { get; set; }


        /// <summary>
        /// 系统字节（System Bytes）
        /// </summary>
        public List<byte> SystemBytes { get; set; } = new List<byte>();

        /// <summary>
        /// Function号（F）
        /// </summary>
        public int Function { get; set; }

        /// <summary>
        /// S0F0标识的Link号
        /// </summary>
        public int LinkNumber { get; set; } = 0;

        /// <summary>
        /// WBit标识（是否需要回复）
        /// </summary>
        public bool WBit { get; set; }

        /// <summary>
        /// 消息根节点
        /// </summary>
        public SecsGemNodeMessage RootNode { get; set; } = new SecsGemNodeMessage();

        /// <summary>
        /// 消息唯一标识
        /// </summary>
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 是否是传入消息（从设备接收的消息 :true ）还是传出消息（发送到设备的消息:false ）
        /// </summary>
        public bool IsIncoming { get; set; } = false;


        public override string ToString()
        {
            return this.ToVisualLog(true);
        }


        private string ToVisualLog(bool isIncoming = true)
        {
            StringBuilder sb = new StringBuilder();
            string direction = isIncoming ? "<--" : "-->";
            string wBit = this.WBit ? " W" : "";

            // 1. 头部信息：例如 [2026-03-26 10:00:00] [ID:xxx] <-- S6F11 W
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ID:{this.MessageId[..8]}] {direction} S{this.Stream}F{this.Function}{wBit}");
            // 2. 系统字节展示
            string sysBytes = string.Join(" ", this.SystemBytes.Select(b => b.ToString("X2")));
            sb.AppendLine($"SystemBytes: [{sysBytes}]");

            // 3. 递归生成消息体 (SML 风格)
            if (this.RootNode != null)
            {
                FormatNode(this.RootNode, sb, 1);
            }

            sb.AppendLine("."); // 结束符
            return sb.ToString();
        }

        private static void FormatNode(SecsGemNodeMessage node, StringBuilder sb, int indent)
        {
            string indentStr = new string(' ', indent * 2);

            // 假设 SecsGemNodeMessage 有 ItemFormat, Value, 和 Children 属性
            // 格式示例: <L [3]
            //             <U4 [1] 100>
            //           >
            if (node == null )
            {
                return;
            }
            string type = node.DataType.ToString(); // 例如: L, U4, ASCII, BI
            string count = node.SubNode?.Count > 0 ? $" [{node.SubNode.Count}]" : "";
            string value = node.TypedValue != null ? $" {node.TypedValue}" : "";

            if (node.DataType ==DataType.LIST) // 列表类型
            {
                sb.AppendLine($"{indentStr}<{type}{count}");
                foreach (var child in node?.SubNode)
                {
                    FormatNode(child, sb, indent + 1);
                }
                sb.AppendLine($"{indentStr}>");
            }
            else // 基本数据类型
            {
                sb.AppendLine($"{indentStr}<{type}{count}{value}>");
            }
        }
    }
}
