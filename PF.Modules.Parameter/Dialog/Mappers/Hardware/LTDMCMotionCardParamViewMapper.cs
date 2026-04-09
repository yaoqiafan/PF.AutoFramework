using PF.Core.Entities.Hardware;
using PF.Modules.Parameter.ViewModels.Models.Hardware;
using PF.UI.Infrastructure.Mappers;

namespace PF.Modules.Parameter.Dialog.Mappers.Hardware
{
    /// <summary>
    /// 雷赛运动控制卡参数映射器（ImplementationClassName = "LTDMCMotionCard"）
    /// </summary>
    public class LTDMCMotionCardParamViewMapper : ViewDataMapperBase
    {
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is LTDMCMotionCardParamView view && data is HardwareConfig config)
            {
                view.DeviceId   = config.DeviceId;
                view.DeviceName = config.DeviceName;
                view.IsEnabled  = config.IsEnabled;
                view.IsSimulated = config.IsSimulated;
                view.Remarks    = config.Remarks;

                config.ConnectionParameters.TryGetValue("CardIndex", out var cardIndex);
                view.CardIndex = cardIndex ?? string.Empty;

                return true;
            }

            return false;
        }

        protected override object ExtractSpecificData(object viewInstance)
        {
            if (viewInstance is LTDMCMotionCardParamView view)
            {
                return new HardwareConfig
                {
                    DeviceId              = view.DeviceId,
                    DeviceName            = view.DeviceName,
                    IsEnabled             = view.IsEnabled,
                    IsSimulated           = view.IsSimulated,
                    Remarks               = view.Remarks,
                    Category              = "MotionCard",
                    ImplementationClassName = "LTDMCMotionCard",
                    ParentDeviceId        = string.Empty,
                    ConnectionParameters  = new Dictionary<string, string>
                    {
                        ["CardIndex"] = view.CardIndex ?? string.Empty
                    }
                };
            }

            return null;
        }
    }
}
