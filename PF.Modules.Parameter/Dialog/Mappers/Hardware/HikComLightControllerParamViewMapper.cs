using PF.Core.Entities.Hardware;
using PF.Modules.Parameter.ViewModels.Models.Hardware;
using PF.UI.Infrastructure.Mappers;

namespace PF.Modules.Parameter.Dialog.Mappers.Hardware
{
    /// <summary>
    /// 海康串口光源控制器参数映射器（ImplementationClassName = "HikComLightController"）。
    /// </summary>
    public class HikComLightControllerParamViewMapper : ViewDataMapperBase
    {
        /// <summary>检查是否有特定映射</summary>
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is HikComLightControllerParamView view && data is HardwareConfig config)
            {
                view.DeviceId    = config.DeviceId;
                view.DeviceName  = config.DeviceName;
                view.IsEnabled   = config.IsEnabled;
                view.IsSimulated = config.IsSimulated;
                view.Remarks     = config.Remarks;

                config.ConnectionParameters.TryGetValue("CommInstanceId", out var commInstanceId);
                view.CommInstanceId = commInstanceId ?? string.Empty;

                view.ChannelCount = config.ConnectionParameters.TryGetValue("ChannelCount", out var cc) && int.TryParse(cc, out var channelCount)
                    ? channelCount
                    : 4;

                return true;
            }

            return false;
        }

        /// <summary>提取特定数据</summary>
        protected override object ExtractSpecificData(object viewInstance)
        {
            if (viewInstance is HikComLightControllerParamView view)
            {
                return new HardwareConfig
                {
                    DeviceId              = view.DeviceId,
                    DeviceName            = view.DeviceName,
                    IsEnabled             = view.IsEnabled,
                    IsSimulated           = view.IsSimulated,
                    // 光源直接挂在通讯实例上，不依附任何父设备
                    ParentDeviceId        = string.Empty,
                    Remarks               = view.Remarks,
                    Category              = "Light",
                    // 必须与硬件工厂注册键一致，否则保存后设备再也实例化不出来
                    ImplementationClassName = "HikComLightController",
                    ConnectionParameters  = new Dictionary<string, string>
                    {
                        ["CommInstanceId"] = view.CommInstanceId ?? string.Empty,
                        ["ChannelCount"] = view.ChannelCount.ToString()
                    }
                };
            }

            return null;
        }
    }
}
