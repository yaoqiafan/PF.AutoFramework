using PF.Core.Entities.Hardware;
using PF.Modules.Parameter.ViewModels.Models.Hardware;
using PF.UI.Infrastructure.Mappers;

namespace PF.Modules.Parameter.Dialog.Mappers.Hardware
{
    /// <summary>
    /// 基恩士智能相机参数映射器（ImplementationClassName = "KeyenceIntelligentCamera"）
    /// </summary>
    public class KeyenceIntelligentCameraParamViewMapper : ViewDataMapperBase
    {
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is KeyenceIntelligentCameraParamView view && data is HardwareConfig config)
            {
                view.DeviceId    = config.DeviceId;
                view.DeviceName  = config.DeviceName;
                view.IsEnabled   = config.IsEnabled;
                view.IsSimulated = config.IsSimulated;
                view.Remarks     = config.Remarks;

                config.ConnectionParameters.TryGetValue("IP",         out var ip);
                config.ConnectionParameters.TryGetValue("TiggerPort", out var tiggerPort);
                config.ConnectionParameters.TryGetValue("TimeOutms",  out var timeOutms);

                view.IP         = ip         ?? string.Empty;
                view.TiggerPort = tiggerPort ?? string.Empty;
                view.TimeOutms  = timeOutms  ?? string.Empty;

                return true;
            }

            return false;
        }

        protected override object ExtractSpecificData(object viewInstance)
        {
            if (viewInstance is KeyenceIntelligentCameraParamView view)
            {
                return new HardwareConfig
                {
                    DeviceId              = view.DeviceId,
                    DeviceName            = view.DeviceName,
                    IsEnabled             = view.IsEnabled,
                    IsSimulated           = view.IsSimulated,
                    ParentDeviceId        = string.Empty,
                    Remarks               = view.Remarks,
                    Category              = "Camera",
                    ImplementationClassName = "KeyenceIntelligentCamera",
                    ConnectionParameters  = new Dictionary<string, string>
                    {
                        ["IP"]         = view.IP         ?? string.Empty,
                        ["TiggerPort"] = view.TiggerPort ?? string.Empty,
                        ["TimeOutms"]  = view.TimeOutms  ?? string.Empty
                    }
                };
            }

            return null;
        }
    }
}
