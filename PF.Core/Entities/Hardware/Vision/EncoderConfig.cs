namespace PF.Core.Entities.Hardware.Vision
{
    /// <summary>
    /// 线阵相机外部编码器配置（外置磁栅/光栅读数头接入相机 IO）。
    ///
    /// <para>所有节点值一律使用 GenICam 的 symbolic 字符串（如 "Line1"、"Encoder0"），
    /// 不使用数字枚举值——数字在不同型号/固件间不保证一致，字符串才是稳定契约。</para>
    ///
    /// <para>分频、方向反转、单/双向触发等节点因型号而异，其确切节点名不写死在代码里，
    /// 现场用 MVS 客户端属性树确认后填入 <see cref="ExtraNodes"/> 即可，无需改代码重新发版。</para>
    /// </summary>
    public class EncoderConfig
    {
        /// <summary>编码器选择器节点值，如 "Encoder0"。</summary>
        public string Selector { get; set; } = "Encoder0";

        /// <summary>编码器 A 相信号源，如 "Line1"。</summary>
        public string SourceA { get; set; } = "Line1";

        /// <summary>编码器 B 相信号源，如 "Line3"。</summary>
        public string SourceB { get; set; } = "Line3";

        /// <summary>
        /// 读数头当量：每个编码器脉冲对应的实际位移（微米/脉冲）。
        /// 仅用于换算并展示行间距，不下发到相机。
        /// </summary>
        public double PulseEquivalentUm { get; set; }

        /// <summary>
        /// 相机侧分频/倍频系数（实际行间距 = <see cref="PulseEquivalentUm"/> × 本系数）。
        /// 仅用于换算展示；真正下发的分频节点请写入 <see cref="ExtraNodes"/>。
        /// </summary>
        public double DividerRatio { get; set; } = 1.0;

        /// <summary>
        /// 附加节点透传表（节点名 → symbolic 值）。
        /// 用于分频、方向反转、单/双向触发等因型号而异的节点，按配置逐条下发。
        /// </summary>
        public Dictionary<string, string> ExtraNodes { get; set; } = new();

        /// <summary>换算后的行间距（微米/行）。</summary>
        public double LineSpacingUm => PulseEquivalentUm * DividerRatio;
    }
}
