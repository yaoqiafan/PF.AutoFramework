using PF.Core.Entities.Hardware;
using PF.Modules.Parameter.ViewModels.Models.Hardware;
using PF.UI.Infrastructure.Mappers;

namespace PF.Modules.Parameter.Dialog.Mappers.Hardware
{
    /// <summary>
    /// 康视达光源控制器参数映射器（ImplementationClassName = "CTS_LightController"）
    /// </summary>
    public class CTSLightControllerParamViewMapper : ViewDataMapperBase
    {
        /// <summary>检查是否有特定映射</summary>
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is CTSLightControllerParamView view && data is HardwareConfig config)
            {
                view.DeviceId    = config.DeviceId;
                view.DeviceName  = config.DeviceName;
                view.IsEnabled   = config.IsEnabled;
                view.IsSimulated = config.IsSimulated;
                view.Remarks     = config.Remarks;

                config.ConnectionParameters.TryGetValue("COM", out var com);
                view.Com = com ?? string.Empty;

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
                    Category              = "Light",
                    // 必须与硬件工厂注册键逐字一致，否则保存后设备再也实例化不出来
                    ImplementationClassName = "CTS_LightController",
                    ConnectionParameters  = new Dictionary<string, string>
                    {
                        ["COM"] = view.Com ?? string.Empty,
                        ["ChannelCount"] = view.ChannelCount.ToString()
                    }
                };
            }

            return null;
        }
    }
}
