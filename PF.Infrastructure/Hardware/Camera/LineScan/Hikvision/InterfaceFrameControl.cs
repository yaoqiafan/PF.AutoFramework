using PF.Core.Entities.Hardware.Vision;
using PF.Core.Interfaces.Device.Hardware.Card;

namespace PF.Infrastructure.Hardware.Camera.LineScan.Hikvision
{
    /// <summary>
    /// 经采集卡的帧控制策略（CameraLink / CoaXPress / XoF）。
    ///
    /// <para>CameraLink 相机没有自己的网络栈，流的切帧发生在采集卡上：
    /// ImageHeight（一帧多少行）、FrameTimeoutTime（攒不满时的兜底出图）、
    /// StreamTrigger*（从哪起帧）全部是采集卡节点。因此本策略只是把配置原样转交给采集卡设备，
    /// 具体节点名与下发顺序由采集卡实现自己负责——这样换采集卡厂商时相机侧无需改动。</para>
    /// </summary>
    internal sealed class InterfaceFrameControl : IFrameControl
    {
        private readonly IFrameGrabberCard _card;

        /// <summary>构造采集卡帧控制策略。</summary>
        public InterfaceFrameControl(IFrameGrabberCard card)
        {
            _card = card ?? throw new ArgumentNullException(nameof(card));
        }

        /// <inheritdoc/>
        public string Description => $"采集卡[{_card.DeviceName}]";

        /// <inheritdoc/>
        public Task ApplyAsync(FrameControlConfig config, CancellationToken token)
            => _card.ApplyFrameControlAsync(config, token);

        /// <inheritdoc/>
        public Task<bool> SoftwareTriggerFrameAsync(CancellationToken token)
            => _card.SoftwareTriggerFrameAsync(token);
    }
}
