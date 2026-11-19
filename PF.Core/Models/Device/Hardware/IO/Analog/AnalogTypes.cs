using PF.Core.Enums.Hardware;

namespace PF.Core.Models.Device.Hardware.IO.Analog
{
    /// <summary>
    /// 模拟量通道在板卡上的地址。
    /// </summary>
    /// <param name="NodeId">从站/扩展模块节点号；null 表示板卡本体（接线盒）AD/DA</param>
    /// <param name="Channel">通道号</param>
    public readonly record struct AnalogAddress(int? NodeId, int Channel)
    {
        /// <inheritdoc/>
        public override string ToString() => NodeId is int n ? $"Node{n}/Ch{Channel}" : $"板载/Ch{Channel}";
    }

    /// <summary>
    /// 一次模拟量读数。
    /// </summary>
    /// <param name="Value">工程值；状态为 WireBreak / CommError / Stale / NotConfigured 时为 null，超量程时仍给出</param>
    /// <param name="Raw">原始值；读取失败或未配置时为 null</param>
    /// <param name="Status">读数状态</param>
    /// <param name="Timestamp">采样时间（本地时间）</param>
    public readonly record struct AnalogReading(double? Value, double? Raw, AnalogStatus Status, DateTime Timestamp);

    /// <summary>
    /// 通道表更新结果（<c>IAnalogIOController.UpdateChannels</c> 的返回值）。
    /// </summary>
    public sealed class AnalogConfigResult
    {
        private AnalogConfigResult(bool success, IReadOnlyList<string> errors)
        {
            Success = success;
            Errors = errors;
        }

        /// <summary>是否已生效</summary>
        public bool Success { get; }

        /// <summary>校验失败的原因（Success = true 时为空）</summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>成功</summary>
        public static AnalogConfigResult Ok() => new(true, Array.Empty<string>());

        /// <summary>失败</summary>
        public static AnalogConfigResult Fail(IReadOnlyList<string> errors) => new(false, errors);
    }
}
