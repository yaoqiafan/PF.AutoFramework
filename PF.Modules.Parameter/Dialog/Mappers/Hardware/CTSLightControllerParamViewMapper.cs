using PF.Core.Entities.Hardware;
using PF.Modules.Parameter.ViewModels.Models.Hardware;
using PF.UI.Infrastructure.Mappers;

namespace PF.Modules.Parameter.Dialog.Mappers.Hardware
{
    /// <summary>
    /// 康视达光源控制器参数映射器（ImplementationClassName = "CTSLightController"）
    /// </summary>
    public class CTSLightControllerParamViewMapper : ViewDataMapperBase
    {
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is CTSLightControllerParamView view && data is HardwareConfig config)
            {
                view.DeviceId    = config.DeviceId;
                view.DeviceName  = config.DeviceName;
                view.IsEnabled   = config.IsEnabled;
                view.IsSimulated = config.IsSimulated;
                view.Remarks     = config.Remarks;

                config.ConnectionParameters.TryGetValue("Com", out var com);
                view.Com = com ?? string.Empty;

                return true;
            }

            return false;
        }

        protected override object ExtractSpecificData(object viewInstance)
        {
            if (viewInstance is CTSLightControllerParamView view)
            {
                return new HardwareConfig
                {
                    DeviceId              = view.DeviceId,
                    DeviceName            = view.DeviceName,
                    IsEnabled             = view.IsEnabled,
                    IsSimulated           = view.IsSimulated,
                    ParentDeviceId        = string.Empty,
                    Remarks               = view.Remarks,
                    Category              = "LightController",
                    ImplementationClassName = "CTSLightController",
                    ConnectionParameters  = new Dictionary<string, string>
                    {
                        ["Com"] = view.Com ?? string.Empty
                    }
                };
            }

            return null;
        }
    }
}
