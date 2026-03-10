using PF.Core.Interfaces.Device.Hardware.BarcodeScan;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.BarcodeScan
{
    public abstract class BaseBarcodeScan : BaseDevice, IBarcodeScan
    {
        protected  BaseBarcodeScan(string deviceId, string deviceName, bool isSimulated, ILogService logger) : base(deviceId, deviceName, isSimulated, logger)
        {
            Category = Core.Enums.HardwareCategory.Scanner;
        }

        public abstract string IPAdress { get; }

        public abstract  int TiggerPort { get; }

        public abstract    int UserPort{ get; }

        public abstract  int TimeOutMs { get; }

        public abstract Task<bool> ChangeUserParam(object UserInfo,CancellationToken token = default);
        public abstract Task<string> Tigger(CancellationToken token = default);


    }
}
