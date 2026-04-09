using PF.Core.Entities.Hardware;
using PF.Modules.Parameter.ViewModels.Models.Hardware;
using PF.UI.Infrastructure.Mappers;

namespace PF.Modules.Parameter.Dialog.Mappers.Hardware
{
    /// <summary>
    /// 海康条码扫描枪参数映射器（ImplementationClassName = "HKBarcodeScan"）
    /// </summary>
    public class HKBarcodeScanParamViewMapper : ViewDataMapperBase
    {
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is HKBarcodeScanParamView view && data is HardwareConfig config)
            {
                view.DeviceId    = config.DeviceId;
                view.DeviceName  = config.DeviceName;
                view.IsEnabled   = config.IsEnabled;
                view.IsSimulated = config.IsSimulated;
                view.Remarks     = config.Remarks;

                config.ConnectionParameters.TryGetValue("IP",         out var ip);
                config.ConnectionParameters.TryGetValue("TiggerPort", out var tiggerPort);
                config.ConnectionParameters.TryGetValue("UserPort",   out var userPort);
                config.ConnectionParameters.TryGetValue("TimeOutMs",  out var timeOutMs);

                view.IP         = ip         ?? string.Empty;
                view.TiggerPort = tiggerPort ?? string.Empty;
                view.UserPort   = userPort   ?? string.Empty;
                view.TimeOutMs  = timeOutMs  ?? string.Empty;

                return true;
            }

            return false;
        }

        protected override object ExtractSpecificData(object viewInstance)
        {
            if (viewInstance is HKBarcodeScanParamView view)
            {
                return new HardwareConfig
                {
                    DeviceId              = view.DeviceId,
                    DeviceName            = view.DeviceName,
                    IsEnabled             = view.IsEnabled,
                    IsSimulated           = view.IsSimulated,
                    ParentDeviceId        = string.Empty,
                    Remarks               = view.Remarks,
                    Category              = "ScanCode",
                    ImplementationClassName = "HKBarcodeScan",
                    ConnectionParameters  = new Dictionary<string, string>
                    {
                        ["IP"]         = view.IP         ?? string.Empty,
                        ["TiggerPort"] = view.TiggerPort ?? string.Empty,
                        ["UserPort"]   = view.UserPort   ?? string.Empty,
                        ["TimeOutMs"]  = view.TimeOutMs  ?? string.Empty
                    }
                };
            }

            return null;
        }
    }
}
