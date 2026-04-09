using PF.Core.Entities.Hardware;
using PF.Modules.Parameter.ViewModels.Models.Hardware;
using PF.UI.Infrastructure.Mappers;

namespace PF.Modules.Parameter.Dialog.Mappers.Hardware
{
    /// <summary>
    /// EtherCAT轴参数映射器（ImplementationClassName = "EtherCatAxis"）
    /// </summary>
    public class EtherCatAxisParamViewMapper : ViewDataMapperBase
    {
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is EtherCatAxisParamView view && data is HardwareConfig config)
            {
                view.DeviceId      = config.DeviceId;
                view.DeviceName    = config.DeviceName;
                view.IsEnabled     = config.IsEnabled;
                view.IsSimulated   = config.IsSimulated;
                view.ParentDeviceId = config.ParentDeviceId;
                view.Remarks       = config.Remarks;

                config.ConnectionParameters.TryGetValue("AxisIndex", out var axisIndex);
                view.AxisIndex = axisIndex ?? string.Empty;

                return true;
            }

            return false;
        }

        protected override object ExtractSpecificData(object viewInstance)
        {
            if (viewInstance is EtherCatAxisParamView view)
            {
                return new HardwareConfig
                {
                    DeviceId              = view.DeviceId,
                    DeviceName            = view.DeviceName,
                    IsEnabled             = view.IsEnabled,
                    IsSimulated           = view.IsSimulated,
                    ParentDeviceId        = view.ParentDeviceId ?? string.Empty,
                    Remarks               = view.Remarks,
                    Category              = "Axis",
                    ImplementationClassName = "EtherCatAxis",
                    ConnectionParameters  = new Dictionary<string, string>
                    {
                        ["AxisIndex"] = view.AxisIndex ?? string.Empty
                    }
                };
            }

            return null;
        }
    }
}
