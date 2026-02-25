using PF.Core.Entities.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Device.Mechanisms
{
    public interface IMechanismUIManager
    {
        void RegisterView(MechanismUIInfo info);
        IEnumerable<MechanismUIInfo> GetAllRegisteredViews();
    }
}
