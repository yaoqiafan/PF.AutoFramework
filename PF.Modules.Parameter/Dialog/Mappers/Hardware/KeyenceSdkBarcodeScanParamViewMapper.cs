using PF.Core.Entities.Hardware;
using PF.Modules.Parameter.ViewModels.Models.Hardware;
using PF.UI.Infrastructure.Mappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Modules.Parameter.Dialog.Mappers.Hardware
{

    /// <summary>
    /// 基恩士 SDK 条码扫描枪参数映射器（ImplementationClassName = "KeyenceBarcodeScan"）。
    /// </summary>
    public class KeyenceSdkBarcodeScanParamViewMapper : ViewDataMapperBase
    {

        /// <summary>检查是否有特定映射</summary>
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is KeyenceSdkBarcodeScanParamView view && data is HardwareConfig config)
            {
                view.DeviceId = config.DeviceId;
                view.DeviceName = config.DeviceName;
                view.IsEnabled = config.IsEnabled;
                view.IsSimulated = config.IsSimulated;
                view.Remarks = config.Remarks;

                config.ConnectionParameters.TryGetValue("IP", out var ip);
                config.ConnectionParameters.TryGetValue("TimeOutMs", out var timeOutMs);

                view.IP = ip ?? string.Empty;
                view.TimeOutMs = timeOutMs ?? string.Empty;

                return true;
            }

            return false;
        }

        /// <summary>提取特定数据</summary>
        protected override object ExtractSpecificData(object viewInstance)
        {
            if (viewInstance is KeyenceSdkBarcodeScanParamView view)
            {
                return new HardwareConfig
                {
                    DeviceId = view.DeviceId,
                    DeviceName = view.DeviceName,
                    IsEnabled = view.IsEnabled,
                    IsSimulated = view.IsSimulated,
                    ParentDeviceId = string.Empty,
                    Remarks = view.Remarks,
                    Category = "ScanCode",
                    ImplementationClassName = "KeyenceBarcodeScan",
                    ConnectionParameters = new Dictionary<string, string>
                    {
                        ["IP"] = view.IP ?? string.Empty,
                        ["TimeOutMs"] = view.TimeOutMs ?? string.Empty
                    }
                };
            }

            return null;
        }
    }
}
