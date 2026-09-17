namespace PF.Core.Entities.Hardware.Vision
{
    /// <summary>
    /// 帧控制配置 —— 控制"一帧多少行"与"从哪里开始收这一帧"。
    ///
    /// <para>本模型在两种链路下内容一致，但**下发到的节点树不同**，由设备层的帧控制策略决定：</para>
    /// <list type="table">
    ///   <item>
    ///     <term>经采集卡（CameraLink/CXP/XoF）</term>
    ///     <description>写入采集卡 IInterface：ImageHeight / FrameTimeoutTime /
    ///     StreamTriggerEnable / StreamTriggerSource / StreamTriggerActivation /
    ///     StreamPartialImageControl / StreamSelector / CameraType</description>
    ///   </item>
    ///   <item>
    ///     <term>相机直连（GigE/USB）</term>
    ///     <description>写入相机 IDevice：Height / FrameTriggerMode / FrameTriggerSource
    ///     （老固件回退到 TriggerSelector=FrameBurstStart + TriggerMode + TriggerSource）</description>
    ///   </item>
    /// </list>
    /// </summary>
    public class FrameControlConfig
    {
        /// <summary>
        /// 帧长：一帧累计多少行。
        /// <para>经采集卡时写 ImageHeight；相机直连时写 Height。小于等于 0 表示不下发、沿用设备当前值。</para>
        /// </summary>
        public int ImageHeight { get; set; }

        /// <summary>
        /// 帧超时（毫秒）：行数攒不满一帧时的兜底出图时间。
        /// <para>仅采集卡链路有对应节点（FrameTimeoutTime）；相机直连时本值不下发，
        /// 仅由 WaitFrameAsync 的等待超时兜底。小于等于 0 表示不下发。</para>
        /// </summary>
        public int FrameTimeoutMs { get; set; }

        /// <summary>是否启用帧触发。false = 连续模式，攒满 <see cref="ImageHeight"/> 行即出图。</summary>
        public bool TriggerEnable { get; set; }

        /// <summary>
        /// 挂了采集卡时，帧触发（含软触发命令）是否仍然落在<b>相机自身</b>节点树上，
        /// 而不是采集卡节点树。
        ///
        /// <para>null（默认）：调用方不关心这件事，设备层退回"连接时探测到的相机实际状态"决定
        /// （相机自身 FrameTriggerMode 当时已经是 On，就落相机侧，否则落卡侧）——不能直接当
        /// false 处理：那等于把"调用方没说"和"调用方明确要卡控帧"划了等号，一次只想测曝光的
        /// 调用就会把现场已经验证好的相机侧接线强行路由回卡，卡侧没有对应触发节点时报
        /// MV_E_GC_ACCESS，这不是假设，是真出过的故障。</para>
        ///
        /// <para>false：保持"挂卡即卡控帧"（<c>ImageHeight</c>/<c>FrameTimeoutTime</c>/
        /// <c>StreamTriggerEnable</c> 等写入采集卡，对应海康官方 ParameterInterface_SoftwareTrigger
        /// 样例的用法，相机侧配合 <c>ScanMode=LineScan</c> + <c>TriggerMode=Off</c>）。</para>
        ///
        /// <para>true：即使挂了采集卡，帧触发（<c>FrameTriggerMode</c>/<c>FrameTriggerSource</c>/
        /// 软触发命令）也下发到相机自身，对应海康官方 LineScanSoftwareTrigger 样例的用法——
        /// 相机侧需配合 <c>ScanMode=FrameScan</c>，此时采集卡只承担物理连接，不参与帧边界判定。
        /// "挂了采集卡"与"由采集卡控帧"是两件独立的事，不能划等号，具体用哪种由现场验证过的
        /// 相机参数决定，false/true 都是调用方的明确选择，只有 null 才交给探测兜底。</para>
        /// </summary>
        public bool? TriggerOnCameraSide { get; set; }

        /// <summary>
        /// 帧触发源 symbolic 值。
        /// <para>采集卡链路：如 "SoftwareSignal0"（软触发）、"QuickSoftwareTrigger0"（快速软触发，
        /// 需固件支持）或硬件信号；相机直连：如 "Software"、"Line0"。</para>
        /// </summary>
        public string TriggerSource { get; set; } = "SoftwareSignal0";

        /// <summary>帧触发有效边沿，如 "RisingEdge"。留空则不下发。</summary>
        public string? TriggerActivation { get; set; } = "RisingEdge";

        /// <summary>残帧处理策略（采集卡 StreamPartialImageControl）。留空则不下发。</summary>
        public string? PartialImageControl { get; set; }

        /// <summary>流选择器（采集卡 StreamSelector）。一卡一相机时通常固定；留空则不下发。</summary>
        public string? StreamSelector { get; set; }

        /// <summary>
        /// 相机类型匹配（采集卡 CameraType）。CameraLink 特有：卡侧与相机侧位宽配置必须一致，
        /// 不匹配的典型表现是出不了图或图像横向错位。留空则不下发。
        /// </summary>
        public string? CameraType { get; set; }

        /// <summary>附加节点透传表（节点名 → 值），按配置逐条下发到当前链路对应的节点树。</summary>
        public Dictionary<string, string> ExtraNodes { get; set; } = new();
    }
}
