using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Device.Hardware.BarcodeScan
{
    public interface IBarcodeScan : IHardwareDevice
    {


        /// <summary>
        /// 扫码枪IP地址
        /// </summary>
        string IPAdress { get; }

        /// <summary>
        /// 触发端口
        /// </summary>
        int TiggerPort { get; }


        /// <summary>
        /// 管理端口
        /// </summary>
        int UserPort { get; }



        /// <summary>
        /// 扫码枪通讯超时时间，单位毫秒
        /// </summary>
        int TimeOutMs { get ; }


        /// <summary>
        /// 出发扫码枪
        /// </summary>
        /// <returns></returns>
        Task<string> Tigger(CancellationToken token =default );


        /// <summary>
        /// 修改扫码枪用户参数集
        /// </summary>
        /// <param name="UserIndex">用户集</param>
        /// <returns></returns>
        Task<bool> ChangeUserParam(object UserInfo, CancellationToken token = default);

    }
}
