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
            return this.ToVisualLog(this.IsIncoming);
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
            // 列表一律写项数（空列表写 [0]，此前不写，看不出是空列表）；非列表项不写
            string count = node.DataType == DataType.LIST ? $" [{node.SubNode?.Count ?? 0}]" : "";
            string value = FormatValue(node);
            if (value.Length > 0) value = " " + value;

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

        /// <summary>
        /// 数据项的值文本。此前直接拼 TypedValue：解析器给 Binary 存的是 byte[]、多元素数值项存的是 T[]，
        /// 打印出来是 "System.Byte[]" / "System.UInt32[]"，日志和托盘实时报文里看不到实际内容。
        /// Binary 一律按 <see cref="SecsGemNodeMessage.Data"/> 输出 SML 风格十六进制（0x01 0xFF），
        /// 不看 TypedValue——构造函数给它存的是 "01 FF" 字符串、解析器存的是 byte[]，两者不一致；
        /// 数组按空格拼接元素。只改显示，TypedValue 本身不变（业务代码按类型取值）。
        /// </summary>
        private static string FormatValue(SecsGemNodeMessage node)
        {
            if (node.DataType == DataType.Binary)
            {
                var data = node.Data ?? (node.TypedValue as byte[]);
                return data == null ? "" : string.Join(" ", data.Select(b => $"0x{b:X2}"));
            }

            return node.TypedValue switch
            {
                null => "",
                string s => s,
                System.Collections.IEnumerable items => string.Join(" ", items.Cast<object>()),
                var v => v.ToString() ?? "",
            };
        }
    }
}
