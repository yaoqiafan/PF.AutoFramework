using PF.Core.Entities.Hardware;
using PF.Modules.Parameter.ViewModels.Models.Hardware;
using PF.UI.Infrastructure.Mappers;

namespace PF.Modules.Parameter.Dialog.Mappers.Hardware
{
    /// <summary>
    /// EtherCAT IO模块参数映射器（ImplementationClassName = "EtherCatIO"）
    /// </summary>
    public class EtherCatIOParamViewMapper : ViewDataMapperBase
    {
        /// <summary>检查是否有特定映射</summary>
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is EtherCatIOParamView view && data is HardwareConfig config)
            {
                view.DeviceId       = config.DeviceId;
                view.DeviceName     = config.DeviceName;
                view.IsEnabled      = config.IsEnabled;
                view.IsSimulated    = config.IsSimulated;
                view.ParentDeviceId = config.ParentDeviceId;
                view.Remarks        = config.Remarks;

                config.ConnectionParameters.TryGetValue("InPutCount", out var inPutCount);
                config.ConnectionParameters.TryGetValue("OutPutCount", out var outPutCount);
                view.InPutCount  = inPutCount  ?? string.Empty;
                view.OutPutCount = outPutCount ?? string.Empty;

                return true;
            }

            return false;
        }

        /// <summary>提取特定数据</summary>
        protected override object ExtractSpecificData(object viewInstance)
        {
            if (viewInstance is EtherCatIOParamView view)
            {
                return new HardwareConfig
                {
                    DeviceId              = view.DeviceId,
                    DeviceName            = view.DeviceName,
                    IsEnabled             = view.IsEnabled,
                    IsSimulated           = view.IsSimulated,
                    ParentDeviceId        = view.ParentDeviceId ?? string.Empty,
                    Remarks               = view.Remarks,
                    Category              = "IOController",
                    ImplementationClassName = "EtherCatIO",
                    ConnectionParameters  = new Dictionary<string, string>
                    {
                        ["InPutCount"]  = view.InPutCount  ?? string.Empty,
                        ["OutPutCount"] = view.OutPutCount ?? string.Empty
                    }
                };
            }

            return null;
        }
    }
}
