using MvCameraControl;
using PF.Core.Entities.Hardware.Vision;
using PF.Core.Enums.Hardware.Vision;

namespace PF.Infrastructure.Hardware.Vision.Hikvision
{
    /// <summary>
    /// 海康 SDK 类型 ↔ 框架类型的翻译层。
    /// <para>把厂商枚举挡在设备层内部，上层（调试面板、机构、工站）只认框架自己的枚举。</para>
    /// </summary>
    internal static class MvTypeMapper
    {
        /// <summary>枚举相机时覆盖的全部传输层：网口、USB，以及经 GenTL 的 CameraLink/CXP/XoF。</summary>
        public const DeviceTLayerType AllCameraLayers =
            DeviceTLayerType.MvGigEDevice
            | DeviceTLayerType.MvUsbDevice
            | DeviceTLayerType.MvGenTLGigEDevice
            | DeviceTLayerType.MvGenTLCameraLinkDevice
            | DeviceTLayerType.MvGenTLCXPDevice
            | DeviceTLayerType.MvGenTLXoFDevice;

        /// <summary>枚举采集卡时覆盖的全部接口类型。</summary>
        public const InterfaceTLayerType AllInterfaceLayers =
            InterfaceTLayerType.MvCameraLinkInterface
            | InterfaceTLayerType.MvCXPInterface
            | InterfaceTLayerType.MvXoFInterface
            | InterfaceTLayerType.MvGigEInterface;

        /// <summary>SDK 像素类型 → 框架像素格式。未识别的返回 Unknown（原始数据仍可透传）。</summary>
        public static ImagePixelFormat ToPixelFormat(MvGvspPixelType pixelType) => pixelType switch
        {
            MvGvspPixelType.PixelType_Gvsp_Mono8 => ImagePixelFormat.Mono8,
            MvGvspPixelType.PixelType_Gvsp_Mono10 => ImagePixelFormat.Mono10,
            MvGvspPixelType.PixelType_Gvsp_Mono12 => ImagePixelFormat.Mono12,
            MvGvspPixelType.PixelType_Gvsp_Mono16 => ImagePixelFormat.Mono16,
            MvGvspPixelType.PixelType_Gvsp_BayerRG8 => ImagePixelFormat.BayerRG8,
            MvGvspPixelType.PixelType_Gvsp_BayerGB8 => ImagePixelFormat.BayerGB8,
            MvGvspPixelType.PixelType_Gvsp_BayerGR8 => ImagePixelFormat.BayerGR8,
            MvGvspPixelType.PixelType_Gvsp_BayerBG8 => ImagePixelFormat.BayerBG8,
            MvGvspPixelType.PixelType_Gvsp_RGB8_Packed => ImagePixelFormat.Rgb8Packed,
            MvGvspPixelType.PixelType_Gvsp_Jpeg => ImagePixelFormat.Jpeg,
            _ => ImagePixelFormat.Unknown,
        };

        /// <summary>SDK 传输层类型 → 框架传输层枚举。</summary>
        public static CameraTransportLayer ToTransportLayer(DeviceTLayerType layer) => layer switch
        {
            DeviceTLayerType.MvGigEDevice
                or DeviceTLayerType.MvVirGigEDevice
                or DeviceTLayerType.MvGenTLGigEDevice => CameraTransportLayer.GigE,
            DeviceTLayerType.MvUsbDevice
                or DeviceTLayerType.MvVirUsbDevice => CameraTransportLayer.Usb,
            DeviceTLayerType.MvCameraLinkDevice
                or DeviceTLayerType.MvGenTLCameraLinkDevice => CameraTransportLayer.CameraLink,
            DeviceTLayerType.MvGenTLCXPDevice => CameraTransportLayer.CoaXPress,
            DeviceTLayerType.MvGenTLXoFDevice => CameraTransportLayer.XoF,
            _ => CameraTransportLayer.Unknown,
        };

        /// <summary>框架存盘格式 → SDK 存盘参数。</summary>
        public static ImageFormatInfo ToImageFormatInfo(ImageFileFormat format)
        {
            var info = new ImageFormatInfo();
            switch (format)
            {
                case ImageFileFormat.Jpeg:
                    info.FormatType = ImageFormatType.Jpeg;
                    info.JpegQuality = 80;
                    break;
                case ImageFileFormat.Tiff:
                    info.FormatType = ImageFormatType.Tiff;
                    info.ByteOrder = 0;
                    break;
                case ImageFileFormat.Png:
                    info.FormatType = ImageFormatType.Png;
                    break;
                default:
                    info.FormatType = ImageFormatType.Bmp;
                    break;
            }

            return info;
        }

        /// <summary>SDK 设备信息 → 框架枚举结果快照。</summary>
        public static DiscoveredDeviceInfo ToDiscoveredDevice(IDeviceInfo info, string? interfaceId = null)
        {
            string? ip = null;
            if (info is IGigEDeviceInfo gigeInfo)
            {
                uint addr = gigeInfo.CurrentIp;
                ip = $"{(addr & 0xff000000) >> 24}.{(addr & 0x00ff0000) >> 16}."
                   + $"{(addr & 0x0000ff00) >> 8}.{addr & 0x000000ff}";
            }

            return new DiscoveredDeviceInfo
            {
                ManufacturerName = info.ManufacturerName ?? string.Empty,
                ModelName = info.ModelName ?? string.Empty,
                SerialNumber = info.SerialNumber ?? string.Empty,
                UserDefinedName = info.UserDefinedName ?? string.Empty,
                TransportLayer = ToTransportLayer(info.TLayerType),
                IpAddress = ip,
                InterfaceId = interfaceId,
            };
        }
    }
}
