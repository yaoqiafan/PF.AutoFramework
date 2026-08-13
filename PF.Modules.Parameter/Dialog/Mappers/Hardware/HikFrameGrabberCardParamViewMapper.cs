using PF.Core.Entities.Hardware;
using PF.Modules.Parameter.ViewModels.Models.Hardware;
using PF.UI.Infrastructure.Mappers;

namespace PF.Modules.Parameter.Dialog.Mappers.Hardware
{
    /// <summary>
    /// 海康图像采集卡参数映射器（ImplementationClassName = "HikFrameGrabberCard"）。
    /// </summary>
    public class HikFrameGrabberCardParamViewMapper : ViewDataMapperBase
    {
        /// <summary>检查是否有特定映射</summary>
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is HikFrameGrabberCardParamView view && data is HardwareConfig config)
            {
                view.DeviceId    = config.DeviceId;
                view.DeviceName  = config.DeviceName;
                view.IsEnabled   = config.IsEnabled;
                view.IsSimulated = config.IsSimulated;
                view.Remarks     = config.Remarks;

                config.ConnectionParameters.TryGetValue("SerialNumber", out var serialNumber);
                config.ConnectionParameters.TryGetValue("Index",        out var index);

                view.SerialNumber = serialNumber ?? string.Empty;
                view.Index        = index        ?? "0";

                return true;
            }

            return false;
        }

        /// <summary>提取特定数据</summary>
        protected override object ExtractSpecificData(object viewInstance)
        {
            if (viewInstance is HikFrameGrabberCardParamView view)
            {
                return new HardwareConfig
                {
                    DeviceId              = view.DeviceId,
                    DeviceName            = view.DeviceName,
                    IsEnabled             = view.IsEnabled,
                    IsSimulated           = view.IsSimulated,
                    // 采集卡是顶级设备，父设备恒为空——相机挂在它下面，而不是反过来
                    ParentDeviceId        = string.Empty,
                    Remarks               = view.Remarks,
                    Category              = "FrameGrabber",
                    ImplementationClassName = "HikFrameGrabberCard",
                    ConnectionParameters  = new Dictionary<string, string>
                    {
                        ["SerialNumber"] = view.SerialNumber ?? string.Empty,
                        ["Index"]        = view.Index        ?? "0"
                    }
                };
            }

            return null;
        }
    }
}
