using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Device.Hardware.LightController
{
    public interface ILightController : IHardwareDevice
    {

        /// <summary>
        /// 设置光源亮度
        /// </summary>
        /// <param name="Channel">通道号</param>
        /// <param name="LightValue">光源亮度值</param>
        /// <returns></returns>

        Task SetLightValue(int Channel, int LightValue);


    }
}
