using PF.Core.Entities.Hardware.Vision;

namespace PF.Infrastructure.Hardware.Camera.LineScan.Hikvision
{
    /// <summary>
    /// 帧控制策略 —— 封装"一帧多少行"与"从哪起帧"到底写进哪一棵节点树。
    ///
    /// <para>这是线扫相机在两种链路下**唯一**的实现分叉：</para>
    /// <list type="bullet">
    ///   <item>经采集卡（CameraLink/CXP/XoF）：帧长与帧触发是采集卡的节点，走
    ///         <see cref="InterfaceFrameControl"/>；</item>
    ///   <item>相机直连（GigE/USB）：回到相机自身的节点，走 <see cref="DeviceFrameControl"/>。</item>
    /// </list>
    ///
    /// <para>行触发（编码器）两种链路下都配在相机上，不属于本策略的职责。</para>
    /// </summary>
    internal interface IFrameControl
    {
        /// <summary>策略描述，用于日志说明当前帧控制落在哪一侧。</summary>
        string Description { get; }

        /// <summary>下发帧控制配置。</summary>
        Task ApplyAsync(FrameControlConfig config, CancellationToken token);

        /// <summary>发送一次帧软触发。</summary>
        Task<bool> SoftwareTriggerFrameAsync(CancellationToken token);
    }
}
