using PF.Core.Entities.Hardware;
using PF.Modules.Parameter.ViewModels.Models.Hardware;
using PF.UI.Infrastructure.Mappers;

namespace PF.Modules.Parameter.Dialog.Mappers.Hardware
{
    /// <summary>
    /// 海康网络摄像机参数映射器（ImplementationClassName = "HikNetworkCamera"）。
    /// </summary>
    public class HikNetworkCameraParamViewMapper : ViewDataMapperBase
    {
        /// <summary>检查是否有特定映射</summary>
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is HikNetworkCameraParamView view && data is HardwareConfig config)
            {
                view.DeviceId       = config.DeviceId;
                view.DeviceName     = config.DeviceName;
                view.IsEnabled      = config.IsEnabled;
                view.IsSimulated    = config.IsSimulated;
                view.ParentDeviceId = config.ParentDeviceId;
                view.Remarks        = config.Remarks;

                config.ConnectionParameters.TryGetValue("IP",             out var ip);
                config.ConnectionParameters.TryGetValue("Port",           out var port);
                config.ConnectionParameters.TryGetValue("UserName",       out var userName);
                config.ConnectionParameters.TryGetValue("Password",       out var password);
                config.ConnectionParameters.TryGetValue("Channel",        out var channel);
                config.ConnectionParameters.TryGetValue("CaptureBufferMB", out var bufferMb);
                config.ConnectionParameters.TryGetValue("SaveFormat",     out var saveFormat);
                config.ConnectionParameters.TryGetValue("SaveDirectory",  out var saveDirectory);

                view.Ip             = ip             ?? string.Empty;
                view.Port           = port           ?? "8000";
                view.UserName       = userName       ?? "admin";
                view.Password       = password       ?? string.Empty;
                view.Channel        = channel        ?? "0";
                view.CaptureBufferMb = bufferMb      ?? "8";
                view.SaveFormat     = saveFormat     ?? "Jpeg";
                view.SaveDirectory  = saveDirectory  ?? string.Empty;

                return true;
            }

            return false;
        }

        /// <summary>提取特定数据</summary>
        protected override object ExtractSpecificData(object viewInstance)
        {
            if (viewInstance is HikNetworkCameraParamView view)
            {
                return new HardwareConfig
                {
                    DeviceId                = view.DeviceId,
                    DeviceName              = view.DeviceName,
                    IsEnabled               = view.IsEnabled,
                    IsSimulated             = view.IsSimulated,
                    ParentDeviceId          = view.ParentDeviceId ?? string.Empty,
                    Remarks                 = view.Remarks,
                    Category                = "Camera",
                    ImplementationClassName = "HikNetworkCamera",
                    ConnectionParameters    = new Dictionary<string, string>
                    {
                        ["IP"]              = view.Ip?.Trim()              ?? string.Empty,
                        ["Port"]            = view.Port?.Trim()            ?? "8000",
                        ["UserName"]        = view.UserName?.Trim()        ?? string.Empty,
                        ["Password"]        = view.Password                ?? string.Empty,
                        ["Channel"]         = view.Channel?.Trim()         ?? "0",
                        ["CaptureBufferMB"] = view.CaptureBufferMb?.Trim() ?? "8",
                        ["SaveFormat"]      = view.SaveFormat?.Trim()      ?? "Jpeg",
                        ["SaveDirectory"]   = view.SaveDirectory?.Trim()   ?? string.Empty,
                    }
                };
            }

            return null;
        }
    }
}
