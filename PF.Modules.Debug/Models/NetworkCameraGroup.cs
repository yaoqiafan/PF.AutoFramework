using PF.Core.Interfaces.Device.Hardware.Camera.NetworkCamera;

namespace PF.Modules.Debug.Models
{
    /// <summary>
    /// 硬件调试树中的「网络摄像机」分组节点载体。
    ///
    /// <para>分组节点必须带 Payload：HardwareDebugView 的 TreeView_SelectedItemChanged
    /// 只对 Payload != null 的节点发起导航，纯视觉分组（如「独立设备」）点了没反应。
    /// 网络摄像机要进的是"多台摄像机的集合页"，故以本类型作分组 Payload。</para>
    ///
    /// <para>只存在于调试 UI 层，不进 PF.Core——它是调试界面的分组载体，不是硬件契约。</para>
    /// </summary>
    public sealed class NetworkCameraGroup
    {
        /// <summary>以组内全部摄像机创建分组载体。</summary>
        /// <param name="cameras">按设备名排序后的摄像机列表</param>
        public NetworkCameraGroup(IReadOnlyList<INetworkCamera> cameras) => Cameras = cameras;

        /// <summary>本组包含的摄像机（按设备名排序）。</summary>
        public IReadOnlyList<INetworkCamera> Cameras { get; }
    }
}
