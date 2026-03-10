using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Device.Hardware.Camera.IntelligentCamera
{
    public interface IIntelligentCamera
    {

        /// <summary>
        /// 相机IP地址
        /// </summary>
        string IPAdress { get; }

        /// <summary>
        /// 相机端口
        /// </summary>
        int TiggerPort { get; }

        /// <summary>
        /// 相机通讯超时时间，单位毫秒
        /// </summary>
        int TimeOutMs { get; }

        /// <summary>
        /// 触发智能相机
        /// </summary>
        /// <returns></returns>
        Task<string> Tigger(CancellationToken token = default);


        /// <summary>
        /// 修改智能相机程序号
        /// </summary>
        /// <param name="ProgramNumber">程序号</param>
        /// <returns></returns>
        Task<bool> ChangeProgram(object ProgramNumber, CancellationToken token = default);

    }
}
