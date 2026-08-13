using PF.Core.Enums.Hardware.Vision;

namespace PF.Core.Entities.Hardware.Vision
{
    /// <summary>
    /// 在线枚举到的相机/采集卡信息（只读快照）。
    /// <para>用途：配置阶段挑选设备（填 IP / 序列号），以及调试面板的"扫描在线设备"按钮。
    /// 与已实例化的设备无关，纯粹是一次 SDK 枚举的结果。</para>
    /// </summary>
    public sealed class DiscoveredDeviceInfo
    {
        /// <summary>厂商名称。</summary>
        public string ManufacturerName { get; init; } = string.Empty;

        /// <summary>型号名称，如 "MV-CL162-91F2M"。</summary>
        public string ModelName { get; init; } = string.Empty;

        /// <summary>序列号（跨链路唯一，推荐作为设备选定依据）。</summary>
        public string SerialNumber { get; init; } = string.Empty;

        /// <summary>用户自定义名称（相机中写入的别名，可能为空）。</summary>
        public string UserDefinedName { get; init; } = string.Empty;

        /// <summary>传输层类型。</summary>
        public CameraTransportLayer TransportLayer { get; init; } = CameraTransportLayer.Unknown;

        /// <summary>当前 IP 地址（仅 GigE 有效，其余链路为空）。</summary>
        public string? IpAddress { get; init; }

        /// <summary>所属采集卡标识（经采集卡枚举时有值）。</summary>
        public string? InterfaceId { get; init; }

        /// <summary>供 UI 直接展示的一行摘要。</summary>
        public string DisplayName =>
            string.IsNullOrEmpty(UserDefinedName)
                ? $"{TransportLayer}: {ManufacturerName} {ModelName} ({SerialNumber})"
                : $"{TransportLayer}: {UserDefinedName} ({SerialNumber})";
    }
}
