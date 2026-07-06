using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Device.Hardware.Camera.IntelligentCamera
{
    /// <summary>
    /// 智能相机接口
    /// </summary>
    public interface IIntelligentCamera
    {

        /// <summary>
        /// 相机IP地址
        /// </summary>
        string IPAddress { get; }

        /// <summary>
        /// 相机触发端口
        /// </summary>
        int TriggerPort { get; }

        /// <summary>
        /// 相机通讯超时时间，单位毫秒
        /// </summary>
        int TimeOutMs { get; }

        /// <summary>
        /// 触发智能相机并返回识别结果。
        /// 失败抛出异常（不再返回 null）；取消抛出 OperationCanceledException。
        /// </summary>
        Task<string> Trigger(CancellationToken token = default);


        /// <summary>
        /// 修改智能相机程序号
        /// </summary>
        /// <param name="ProgramNumber">程序号</param>
        /// <param name="token">取消令牌</param>
        /// <returns>true = 切换成功</returns>
        Task<bool> ChangeProgram(object ProgramNumber, CancellationToken token = default);


        /// <summary>
        /// 相机程式列表（获取失败返回空列表，不返回 null）
        /// </summary>
        List<string> CameraProgram { get; }



        /// <summary>
        /// 判断相机程式是否存在
        /// </summary>
        /// <param name="programName">程式名称，格式 "{程序号}_{名称}"</param>
        /// <param name="token">取消令牌</param>
        /// <returns>true = 程式存在且已恢复原程式</returns>
        Task<bool> DetermineProgramExists(object programName, CancellationToken token = default);

        /// <summary>
        /// 当前程序
        /// </summary>
        string CurrentProgram { get; }


    }
}
