using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Device.Hardware.LightController;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.LightController
{
    public abstract class BaseLightController : BaseDevice, ILightController
    {
        protected BaseLightController(string deviceId, string deviceName, bool isSimulated, ILogService logger) : base(deviceId, deviceName, isSimulated, logger)
        {
            Category = Core.Enums.HardwareCategory.LightController ;
        }

       

    
        public abstract Task SetLightValue(int Channel, int LightValue);
    }
}
