using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Hardware.IO.Basic
{
    /// <summary>
    /// 数字量 IO 控制卡接口，继承自基础硬件设备接口
    /// </summary>
    public interface IIOController : IHardwareDevice
    {
        /// <summary>输入端口总数</summary>
        int InputCount { get; }

        /// <summary>输出端口总数</summary>
        int OutputCount { get; }

        /// <summary>
        /// 读取指定的输入信号
        /// </summary>
        /// <param name="portIndex">端口号</param>
        /// <returns>True 为有信号(高电平)，False 为无信号(低电平)</returns>
        bool ReadInput(int portIndex);

        /// <summary>
        /// 设置指定的输出信号
        /// </summary>
        /// <param name="portIndex">端口号</param>
        /// <param name="value">True 为开启输出，False 为关闭</param>
        void WriteOutput(int portIndex, bool value);

        /// <summary>
        /// 读取指定的输出信号当前状态（用于UI回显）
        /// </summary>
        bool ReadOutput(int portIndex);

        /// <summary>
        /// 异步等待某个输入端口达到指定状态（自带防卡死超时机制）
        /// </summary>
        /// <param name="portIndex">端口号</param>
        /// <param name="targetState">期待达到的状态</param>
        /// <param name="timeoutMs">超时时间(毫秒)</param>
        /// <param name="token">取消令牌</param>
        Task<bool> WaitInputAsync(int portIndex, bool targetState, int timeoutMs = 5000, CancellationToken token = default);
    }
}
