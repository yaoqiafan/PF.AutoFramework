namespace PF.Core.Interfaces.Device.Hardware
{
    /// <summary>
    /// GenICam 节点通用读写通道 —— 相机与采集卡共用。
    ///
    /// <para>存在意义：相机/采集卡的可配节点因型号、固件而异（同为采集卡，
    /// CameraLink 卡有 ImageHeight/FrameTimeoutTime，CXP 卡却是 IspGamma/BayerCFAEnable）。
    /// 把强类型配置覆盖不到的部分交给本通道，换型号时改配置即可，不必改代码重新发版。</para>
    ///
    /// <para>值一律以字符串传递（枚举用 symbolic 名，数值用不变文化格式的字面量），
    /// 由实现方按节点实际类型转换后下发。</para>
    /// </summary>
    public interface IGenICamNodeAccess
    {
        /// <summary>
        /// 探测节点是否存在且当前可访问（RO/RW/WO 均视为可用）。
        /// <para>用于新旧固件双分支：节点不存在时应跳过并记日志，而不是让整个初始化失败。</para>
        /// </summary>
        Task<bool> IsNodeAvailableAsync(string nodeName, CancellationToken token = default);

        /// <summary>读取节点当前值。节点不可读或不存在时返回 null。</summary>
        Task<string?> GetNodeAsync(string nodeName, CancellationToken token = default);

        /// <summary>写入节点值。节点不存在、不可写或值非法时返回 false（不抛异常）。</summary>
        Task<bool> SetNodeAsync(string nodeName, string value, CancellationToken token = default);

        /// <summary>
        /// 读取枚举节点的全部可选项（symbolic 名列表），供调试面板填充下拉框。
        /// 非枚举节点或读取失败返回空列表。
        /// </summary>
        Task<IReadOnlyList<string>> GetEnumEntriesAsync(string nodeName, CancellationToken token = default);

        /// <summary>执行命令节点（如 TriggerSoftware / StreamSoftwareTrigger）。</summary>
        Task<bool> ExecuteCommandAsync(string nodeName, CancellationToken token = default);
    }
}
