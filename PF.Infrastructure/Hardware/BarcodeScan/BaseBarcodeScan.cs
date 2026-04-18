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
    /// <summary>
    /// 扫码器基类
    /// </summary>
    public abstract class BaseBarcodeScan : BaseDevice, IBarcodeScan
    {
        /// <summary>
        /// 构造扫码器
        /// </summary>
        protected  BaseBarcodeScan(string deviceId, string deviceName, bool isSimulated, ILogService logger) : base(deviceId, deviceName, isSimulated, logger)
        {
            Category = Core.Enums.HardwareCategory.Scanner;
        }

        /// <summary>
        /// IP地址
        /// </summary>
        public abstract string IPAdress { get; }

        /// <summary>
        /// 触发端口
        /// </summary>
        public abstract  int TiggerPort { get; }

        /// <summary>
        /// 用户端口
        /// </summary>
        public abstract    int UserPort{ get; }

        /// <summary>
        /// 超时时间（毫秒）
        /// </summary>
        public abstract  int TimeOutMs { get; }

        /// <summary>
        /// 切换用户参数
        /// </summary>
        public abstract Task<bool> ChangeUserParam(object UserInfo,CancellationToken token = default);
        /// <summary>
        /// 触发扫码
        /// </summary>
        public abstract Task<string> Tigger(CancellationToken token = default);


    }
}
