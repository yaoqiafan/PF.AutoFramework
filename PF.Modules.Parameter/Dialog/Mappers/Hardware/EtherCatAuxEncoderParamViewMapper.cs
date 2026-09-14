using PF.Core.Entities.Hardware;
using PF.Modules.Parameter.ViewModels.Models.Hardware;
using PF.UI.Infrastructure.Mappers;

namespace PF.Modules.Parameter.Dialog.Mappers.Hardware
{
    /// <summary>
    /// 辅助编码器参数映射器（ImplementationClassName = "EtherCatAuxEncoder"）
    /// </summary>
    public class EtherCatAuxEncoderParamViewMapper : ViewDataMapperBase
    {
        /// <summary>检查是否有特定映射</summary>
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is EtherCatAuxEncoderParamView view && data is HardwareConfig config)
            {
                view.DeviceId       = config.DeviceId;
                view.DeviceName     = config.DeviceName;
                view.IsEnabled      = config.IsEnabled;
                view.IsSimulated    = config.IsSimulated;
                view.ParentDeviceId = config.ParentDeviceId;
                view.Remarks        = config.Remarks;

                config.ConnectionParameters.TryGetValue("Channel", out var channel);
                config.ConnectionParameters.TryGetValue("Multiplier", out var multiplier);
                view.Channel    = channel ?? string.Empty;
                view.Multiplier = string.IsNullOrWhiteSpace(multiplier) ? "2" : multiplier;

                return true;
            }

            return false;
        }

        /// <summary>提取特定数据</summary>
        protected override object ExtractSpecificData(object viewInstance)
        {
            if (viewInstance is EtherCatAuxEncoderParamView view)
            {
                return new HardwareConfig
                {
                    DeviceId              = view.DeviceId,
                    DeviceName            = view.DeviceName,
                    IsEnabled             = view.IsEnabled,
                    IsSimulated           = view.IsSimulated,
                    ParentDeviceId        = view.ParentDeviceId ?? string.Empty,
                    Remarks               = view.Remarks,
                    Category              = "AuxEncoder",
                    ImplementationClassName = "EtherCatAuxEncoder",
                    ConnectionParameters  = new Dictionary<string, string>
                    {
                        ["Channel"]    = view.Channel    ?? string.Empty,
                        ["Multiplier"] = string.IsNullOrWhiteSpace(view.Multiplier) ? "2" : view.Multiplier
                    }
                };
            }

            return null;
        }
    }
}
