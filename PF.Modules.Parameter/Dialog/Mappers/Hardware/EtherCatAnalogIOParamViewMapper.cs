using PF.Core.Entities.Hardware;
using PF.Modules.Parameter.ViewModels.Models.Hardware;
using PF.UI.Infrastructure.Mappers;

namespace PF.Modules.Parameter.Dialog.Mappers.Hardware
{
    /// <summary>
    /// 总线卡模拟量模块参数映射器（ImplementationClassName = "EtherCatAnalogIO"）。
    /// 只编辑 NodeId；Channels 通道表等本视图不认识的键由 CommonChangeParamDialogViewModel 统一原样补回。
    /// </summary>
    public class EtherCatAnalogIOParamViewMapper : ViewDataMapperBase
    {
        private const string NodeIdKey = "NodeId";

        /// <summary>检查是否有特定映射</summary>
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is EtherCatAnalogIOParamView view && data is HardwareConfig config)
            {
                view.DeviceId       = config.DeviceId;
                view.DeviceName     = config.DeviceName;
                view.IsEnabled      = config.IsEnabled;
                view.IsSimulated    = config.IsSimulated;
                view.ParentDeviceId = config.ParentDeviceId;
                view.Remarks        = config.Remarks;

                config.ConnectionParameters.TryGetValue(NodeIdKey, out var nodeId);
                view.NodeId = nodeId ?? string.Empty;

                return true;
            }

            return false;
        }

        /// <summary>提取特定数据</summary>
        protected override object ExtractSpecificData(object viewInstance)
        {
            if (viewInstance is EtherCatAnalogIOParamView view)
            {
                return new HardwareConfig
                {
                    DeviceId              = view.DeviceId,
                    DeviceName            = view.DeviceName,
                    IsEnabled             = view.IsEnabled,
                    IsSimulated           = view.IsSimulated,
                    ParentDeviceId        = view.ParentDeviceId ?? string.Empty,
                    Remarks               = view.Remarks,
                    Category              = "AnalogIO",
                    ImplementationClassName = "EtherCatAnalogIO",
                    ConnectionParameters  = new Dictionary<string, string>
                    {
                        [NodeIdKey] = view.NodeId?.Trim() ?? string.Empty
                    }
                };
            }

            return null;
        }
    }
}
