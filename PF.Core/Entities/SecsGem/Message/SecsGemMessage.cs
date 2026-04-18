using PF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.SecsGem.Message
{
    /// <summary>
    /// SecsGem消息
    /// </summary>
    public class SecsGemMessage
    {
        /// <summary>
        /// Stream�ţ�S��
        /// </summary>
        public int Stream { get; set; }


        /// <summary>
        /// ϵͳ�ֽڣ�System Bytes��
        /// </summary>
        public List<byte> SystemBytes { get; set; } = new List<byte>();

        /// <summary>
        /// Function�ţ�F��
        /// </summary>
        public int Function { get; set; }

        /// <summary>
        /// S0F0��ʶ��Link��
        /// </summary>
        public int LinkNumber { get; set; } = 0;

        /// <summary>
        /// WBit��ʶ���Ƿ���Ҫ�ظ���
        /// </summary>
        public bool WBit { get; set; }

        /// <summary>
        /// ��Ϣ���ڵ�
        /// </summary>
        public SecsGemNodeMessage RootNode { get; set; } = new SecsGemNodeMessage();

        /// <summary>
        /// ��ϢΨһ��ʶ
        /// </summary>
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// �Ƿ��Ǵ�����Ϣ�����豸���յ���Ϣ :true �����Ǵ�����Ϣ�����͵��豸����Ϣ:false ��
        /// </summary>
        public bool IsIncoming { get; set; } = false;


        /// <summary>
        /// 返回消息的可视化日志字符串
        /// </summary>
        public override string ToString()
        {
            return this.ToVisualLog(true);
        }


        private string ToVisualLog(bool isIncoming = true)
        {
            StringBuilder sb = new StringBuilder();
            string direction = isIncoming ? "<--" : "-->";
            string wBit = this.WBit ? " W" : "";

            // 1. ͷ����Ϣ������ [2026-03-26 10:00:00] [ID:xxx] <-- S6F11 W
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ID:{this.MessageId[..8]}] {direction} S{this.Stream}F{this.Function}{wBit}");
            // 2. ϵͳ�ֽ�չʾ
            string sysBytes = string.Join(" ", this.SystemBytes.Select(b => b.ToString("X2")));
            sb.AppendLine($"SystemBytes: [{sysBytes}]");

            // 3. �ݹ�������Ϣ�� (SML ���)
            if (this.RootNode != null)
            {
                FormatNode(this.RootNode, sb, 1);
            }

            sb.AppendLine("."); // ������
            return sb.ToString();
        }

        private static void FormatNode(SecsGemNodeMessage node, StringBuilder sb, int indent)
        {
            string indentStr = new string(' ', indent * 4);

            // ���� SecsGemNodeMessage �� ItemFormat, Value, �� Children ����
            // ��ʽʾ��: <L [3]
            //             <U4 [1] 100>
            //           >
            if (node == null )
            {
                return;
            }
            string type = node.DataType.ToString(); // ����: L, U4, ASCII, BI
            string count = node.SubNode?.Count > 0 ? $" [{node.SubNode.Count}]" : "";
            string value = node.TypedValue != null ? $" {node.TypedValue}" : "";

            if (node.DataType ==DataType.LIST) // �б�����
            {
                sb.AppendLine($"{indentStr}<{type}{count}");
                foreach (var child in node?.SubNode)
                {
                    FormatNode(child, sb, indent + 1);
                }
                sb.AppendLine($"{indentStr}>");
            }
            else // ������������
            {
                sb.AppendLine($"{indentStr}<{type}{count}{value}>");
            }
        }
    }
}
