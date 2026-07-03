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
        /// <summary>检查是否有特定映射</summary>
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is HKBarcodeScanParamView view && data is HardwareConfig config)
            {
                view.DeviceId    = config.DeviceId;
                view.DeviceName  = config.DeviceName;
                view.IsEnabled   = config.IsEnabled;
                view.IsSimulated = config.IsSimulated;
                view.Remarks     = config.Remarks;

                config.ConnectionParameters.TryGetValue("TriggerCommInstanceId",   out var triggerId);
                config.ConnectionParameters.TryGetValue("UserPowerCommInstanceId", out var userPowerId);
                config.ConnectionParameters.TryGetValue("TimeOutMs",               out var timeOutMs);

                view.TriggerCommInstanceId   = triggerId   ?? string.Empty;
                view.UserPowerCommInstanceId = userPowerId ?? string.Empty;
                view.TimeOutMs               = timeOutMs   ?? string.Empty;

                return true;
            }

            return false;
        }

        /// <summary>提取特定数据</summary>
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
                        ["TriggerCommInstanceId"]   = view.TriggerCommInstanceId   ?? string.Empty,
                        ["UserPowerCommInstanceId"] = view.UserPowerCommInstanceId ?? string.Empty,
                        ["TimeOutMs"]               = view.TimeOutMs               ?? string.Empty
                    }
                };
            }

            return null;
        }
    }
}
