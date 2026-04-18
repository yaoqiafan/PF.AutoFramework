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
    /// <summary>
    /// 光源控制器基类
    /// </summary>
    public abstract class BaseLightController : BaseDevice, ILightController
    {
        /// <summary>
        /// 构造光源控制器
        /// </summary>
        protected BaseLightController( string deviceId, string deviceName, bool isSimulated, ILogService logger) : base(deviceId:deviceName , deviceName:deviceName, isSimulated:isSimulated , logger:logger )
        {
            Category = Core.Enums.HardwareCategory.LightController;
        }




        /// <summary>
        /// 设置光源亮度值
        /// </summary>
        public abstract Task SetLightValue(int Channel, int LightValue);



        /// <summary>
        /// 串口名称
        /// </summary>
        public abstract string ComName { get; }



        /// <summary>
        /// IP地址
        /// </summary>
        public abstract string IPAdress { get; }


        /// <summary>
        /// 端口号
        /// </summary>
        public abstract int  Port { get; }

    }
}
