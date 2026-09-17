namespace PF.Core.Entities.Hardware.Vision
{
    /// <summary>
    /// 线阵相机完整配置（相机本体参数 + 行触发 + 帧控制）。
    ///
    /// <para>所有可空字段的语义统一为：null = 不下发、沿用设备当前值。
    /// 这样同一个配置对象可以用于"只改曝光"这类局部下发，不会把未填字段清成默认值。</para>
    /// </summary>
    public class LineScanCameraConfig
    {
        /// <summary>
        /// 扫描模式，如 "LineScan"、部分型号支持的 "FrameScan"。null（默认）= 不下发、沿用设备
        /// 当前值——这是个模式切换类参数、不是幂等赋值，不该由本类替调用方猜一个默认值："LineScan"
        /// 对某些型号/接线（比如编码器接在采集卡上、经外部信号线转发的场景）就是错的，猜错等于
        /// 拿相机已经调好的真实配置去覆盖。
        /// </summary>
        public string? ScanMode { get; set; }

        /// <summary>像素格式 symbolic 值，如 "Mono8"。留空则不下发。</summary>
        public string? PixelFormat { get; set; }

        /// <summary>曝光时间（微秒）。下发前会先关闭自动曝光。</summary>
        public double? ExposureTimeUs { get; set; }

        /// <summary>模拟增益档位 symbolic 值（PreampGain 是枚举节点，不是数值）。</summary>
        public string? PreampGain { get; set; }

        /// <summary>数字增益。下发时会一并置 DigitalShiftEnable=true。</summary>
        public double? DigitalShift { get; set; }

        /// <summary>无损压缩模式 symbolic 值（ImageCompressionMode）。留空则不下发。</summary>
        public string? ImageCompressionMode { get; set; }

        /// <summary>行触发配置（每扫一行由谁驱动）。</summary>
        public LineTriggerConfig LineTrigger { get; set; } = new();

        /// <summary>帧控制配置（一帧多少行、从哪起帧）。</summary>
        public FrameControlConfig FrameControl { get; set; } = new();

        /// <summary>
        /// 相机侧附加节点透传表（节点名 → 值）。
        /// 用于本模型未覆盖、且因型号而异的节点，按配置逐条下发。
        /// </summary>
        public Dictionary<string, string> ExtraNodes { get; set; } = new();
    }
}
