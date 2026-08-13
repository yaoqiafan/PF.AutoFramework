namespace PF.Core.Enums.Hardware.Vision
{
    /// <summary>
    /// 线阵相机的行触发方式 —— 决定"每扫一行"由谁驱动。
    /// <para>行触发始终配置在相机本体上，与是否使用采集卡无关。</para>
    /// </summary>
    public enum LineTriggerMode
    {
        /// <summary>
        /// 相机内部行频驱动（AcquisitionLineRateEnable + AcquisitionLineRate）。
        /// <para>零接线，但行间距 = 扫描速度 ÷ 行频，运动速度波动会直接变成图像 Y 向拉伸/压缩。
        /// 适用于仿真、无编码器的验证场合。</para>
        /// </summary>
        InternalRate = 0,

        /// <summary>
        /// 外部编码器驱动（LineTriggerSource = EncoderModuleOut）。
        /// <para>编码器 A/B 相接入相机 IO，走多远扫多少行，与运动速度无关，是线扫的标准做法。</para>
        /// </summary>
        Encoder = 1,

        /// <summary>
        /// 外部信号线直接驱动（LineTriggerSource = Line0/Line1/...）。
        /// <para>由外部脉冲源逐行触发，不经相机内部编码器模块。</para>
        /// </summary>
        ExternalLine = 2,
    }
}
