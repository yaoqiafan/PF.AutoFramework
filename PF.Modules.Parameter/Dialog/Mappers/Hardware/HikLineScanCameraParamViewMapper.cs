using PF.Core.Entities.Hardware;
using PF.Modules.Parameter.ViewModels.Models.Hardware;
using PF.UI.Infrastructure.Mappers;

namespace PF.Modules.Parameter.Dialog.Mappers.Hardware
{
    /// <summary>
    /// 海康线阵相机参数映射器（ImplementationClassName = "HikLineScanCamera"）。
    /// <para>ParentDeviceId 非空即挂在采集卡下（CameraLink 链路），留空即 GigE/USB 直连。</para>
    /// </summary>
    public class HikLineScanCameraParamViewMapper : ViewDataMapperBase
    {
        /// <summary>检查是否有特定映射</summary>
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is HikLineScanCameraParamView view && data is HardwareConfig config)
            {
                view.DeviceId       = config.DeviceId;
                view.DeviceName     = config.DeviceName;
                view.IsEnabled      = config.IsEnabled;
                view.IsSimulated    = config.IsSimulated;
                view.ParentDeviceId = config.ParentDeviceId;
                view.Remarks        = config.Remarks;

                config.ConnectionParameters.TryGetValue("SerialNumber", out var serialNumber);
                config.ConnectionParameters.TryGetValue("Index",        out var index);
                config.ConnectionParameters.TryGetValue("ImageNodeNum", out var imageNodeNum);

                view.SerialNumber = serialNumber ?? string.Empty;
                view.Index        = index        ?? "0";
                view.ImageNodeNum = imageNodeNum ?? "3";

                return true;
            }

            return false;
        }

        /// <summary>提取特定数据</summary>
        protected override object ExtractSpecificData(object viewInstance)
        {
            if (viewInstance is HikLineScanCameraParamView view)
            {
                return new HardwareConfig
                {
                    DeviceId              = view.DeviceId,
                    DeviceName            = view.DeviceName,
                    IsEnabled             = view.IsEnabled,
                    IsSimulated           = view.IsSimulated,
                    ParentDeviceId        = view.ParentDeviceId ?? string.Empty,
                    Remarks               = view.Remarks,
                    Category              = "Camera",
                    ImplementationClassName = "HikLineScanCamera",
                    ConnectionParameters  = new Dictionary<string, string>
                    {
                        ["SerialNumber"] = view.SerialNumber ?? string.Empty,
                        ["Index"]        = view.Index        ?? "0",
                        ["ImageNodeNum"] = view.ImageNodeNum ?? "3"
                    }
                };
            }

            return null;
        }
    }
}
