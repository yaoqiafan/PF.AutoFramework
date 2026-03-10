using PF.Core.Interfaces.Device.Hardware.IO;
using PF.Workstation.AutoOcr.CostParam;
using Prism.Ioc;
using Prism.Modularity;

namespace PF.Workstation.AutoOcr
{
    /// <summary>
    /// AutoOcr 工站模块
    /// 负责注册该工站特有的 IO 映射和其他业务逻辑
    /// </summary>
    public class AutoOcrModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var ioMappingService = containerProvider.Resolve<IIOMappingService>();

            // 注册输入枚举（这里的 "IO_CARD_0" 应该是该 IO 卡在 HardwareConfig 中的 DeviceId）
            ioMappingService.RegisterInputEnum<E_InPutName>("IO_CARD_0");

            // 注册输出枚举
            ioMappingService.RegisterOutputEnum<E_OutPutName>("IO_CARD_0");
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 如需注册该模块特有的服务，可在此处添加
        }
    }
}
