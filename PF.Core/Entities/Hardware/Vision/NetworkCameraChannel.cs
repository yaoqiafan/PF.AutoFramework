namespace PF.Core.Entities.Hardware.Vision
{
    /// <summary>网络摄像机的一个通道（IPC 通常只有一个模拟通道；NVR 下为各路视频输入）。</summary>
    public sealed class NetworkCameraChannel
    {
        /// <summary>通道号（传给 SDK 抓图接口用的编号）。</summary>
        public int ChannelNo { get; init; }

        /// <summary>显示名称（如 "模拟通道 1" / "IP 通道 33"）。</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>是否数字（IP）通道。NVR 下接入的网络摄像机走数字通道，编号从 33 起。</summary>
        public bool IsIpChannel { get; init; }
    }
}
