using PF.Core.Interfaces.Device.Hardware.Camera.IntelligentCamera;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.Carame.IntelligentCamera
{
    /// <summary>
    /// 智能相机基类
    /// </summary>
    public abstract class BaseIntelligentCamera : BaseDevice, IIntelligentCamera
    {
        /// <summary>
        /// 构造智能相机
        /// </summary>
        public BaseIntelligentCamera(string deviceId, string deviceName, bool isSimulated, ILogService logger) : base(deviceId, deviceName, isSimulated, logger)
        {
            Category = Core.Enums.HardwareCategory.Camera;
        }

        /// <summary>
        /// IP地址
        /// </summary>
        public abstract string IPAdress { get; }

        /// <summary>
        /// 触发端口
        /// </summary>
        public abstract int TiggerPort { get; }

        /// <summary>
        /// 相机程序列表
        /// </summary>
        public abstract List <string > CameraProgram { get; }




        /// <summary>
        /// 超时时间（毫秒）
        /// </summary>
        public abstract  int TimeOutMs { get; }

        /// <summary>
        /// 切换程序
        /// </summary>
        public abstract Task<bool> ChangeProgram(object ProgramNumber, CancellationToken token = default);


        /// <summary>
        /// 触发拍照
        /// </summary>
        public abstract Task<string> Tigger(CancellationToken token = default);

        /// <summary>
        /// 判断程序是否存在
        /// </summary>
        public abstract Task<bool> DetermineProgramExits(object programName, CancellationToken token = default);

    }
}
