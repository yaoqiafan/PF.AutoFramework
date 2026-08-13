using PF.Core.Enums.Hardware.Vision;

namespace PF.Core.Entities.Hardware.Vision
{
    /// <summary>
    /// 行触发配置 —— 控制"每扫一行由谁驱动"。
    /// <para>始终配置在相机本体上，与是否经采集卡无关。</para>
    /// </summary>
    public class LineTriggerConfig
    {
        /// <summary>行触发方式。</summary>
        public LineTriggerMode Mode { get; set; } = LineTriggerMode.InternalRate;

        /// <summary>
        /// 行触发源 symbolic 值。
        /// <para><see cref="LineTriggerMode.Encoder"/> 时通常为 "EncoderModuleOut"；
        /// <see cref="LineTriggerMode.ExternalLine"/> 时为 "Line0" 等。
        /// 留空则由设备层按 <see cref="Mode"/> 取默认值。</para>
        /// </summary>
        public string? TriggerSource { get; set; }

        /// <summary>
        /// 编码器配置。<see cref="Mode"/> 为 <see cref="LineTriggerMode.Encoder"/> 时必填，
        /// 其余方式下可为 null（整块选配，便于后续更换编码器类型）。
        /// </summary>
        public EncoderConfig? Encoder { get; set; }

        /// <summary>
        /// 内部行频使能。仅 <see cref="LineTriggerMode.InternalRate"/> 下有意义。
        /// </summary>
        public bool AcquisitionLineRateEnable { get; set; }

        /// <summary>
        /// 内部行频设定值（行/秒）。仅 <see cref="LineTriggerMode.InternalRate"/> 下有意义。
        /// </summary>
        public int AcquisitionLineRate { get; set; }

        /// <summary>附加节点透传表（节点名 → symbolic 值），按配置逐条下发。</summary>
        public Dictionary<string, string> ExtraNodes { get; set; } = new();
    }
}
