using PF.Core.Interfaces.Device.Hardware.Camera.IntelligentCamera;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.Carame.IntelligentCamera
{
    public abstract class BaseIntelligentCamera : BaseDevice, IIntelligentCamera
    {
        public BaseIntelligentCamera(string deviceId, string deviceName, bool isSimulated, ILogService logger) : base(deviceId, deviceName, isSimulated, logger)
        {
            Category = Core.Enums.HardwareCategory.Camera;
        }

        public abstract string IPAdress { get; }

        public abstract int TiggerPort { get; }

     

        public abstract  int TimeOutMs { get; }

        public abstract Task<bool> ChangeProgram(object ProgramNumber, CancellationToken token = default);


        public abstract Task<string> Tigger(CancellationToken token = default);



    }
}
