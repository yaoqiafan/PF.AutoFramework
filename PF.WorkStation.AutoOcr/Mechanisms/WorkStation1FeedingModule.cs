using PF.Core.Attributes;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Mechanisms;
using PF.Workstation.AutoOcr.CostParam;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Mechanisms
{
    [MechanismUI("工位1上晶圆模组", "Workstation1FeedingModelDebugView", 1)]
    public class WorkStation1FeedingModule : BaseMechanism
    {
        public WorkStation1FeedingModule(IHardwareManagerService hardwareManagerService, IParamService paramService, ILogService logger)
            : base(E_Mechanisms.工位1上晶圆模组.ToString(), hardwareManagerService, paramService, logger)
        {
        }

        protected override Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        protected override Task InternalStopAsync()
        {
            throw new NotImplementedException();
        }

        #region 晶圆上料模组业务流程方法 

        /// <summary>
        /// 0.初始化上料状态
        /// </summary>
        /// <param name="token">取消令牌</param>
        public async Task InitializeFeedingStateAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 1.判断晶圆盒子是哪种类型：12或8寸
        /// </summary>
        /// <param name="token">取消令牌</param>
        /// <returns>返回晶圆盒尺寸（例如返回 8 或 12）</returns>
        public async Task<E_WafeSize> GetWaferBoxSizeAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 2.切换当前设备准备生产物料状态：8寸状态，12寸状态
        /// </summary>
        /// <param name="waferSize">晶圆尺寸参数（8 或 12）</param>
        /// <param name="token">取消令牌</param>
        public async Task<bool> SwitchProductionStateAsync(E_WafeSize waferSize, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 3.判断当前是否具备动轴的条件（Z轴和X轴）
        /// </summary>
        /// <param name="token">取消令牌</param>
        /// <returns>具备条件返回 true，否则返回 false</returns>
        public async Task<bool> CanMoveZAxesAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 3.判断当前是否具备动轴的条件（Z轴和X轴）
        /// </summary>
        /// <param name="token">取消令牌</param>
        /// <returns>具备条件返回 true，否则返回 false</returns>
        public async Task<bool> CanMoveXAxesAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 4.寻层的方法（晶圆扫描）
        /// </summary>
        /// <param name="token">取消令牌</param>
        public async Task<int> SearchLayerAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 5.切换层数的方法
        /// </summary>
        /// <param name="targetLayer">目标层数序号</param>
        /// <param name="token">取消令牌</param>
        public async Task<bool> SwitchToLayerAsync(int targetLayer, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 6.判断当前是否具备拉出物料条件
        /// </summary>
        /// <param name="token">取消令牌</param>
        /// <returns>具备拉出条件返回 true，否则返回 false</returns>
        public async Task<bool> CanPullOutMaterialAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 7.阻塞等待拉出物料完成（标志位）
        /// </summary>
        /// <param name="timeoutMilliseconds">超时时间，防止死等</param>
        /// <param name="token">取消令牌</param>
        /// <returns>在规定时间内完成返回 true，否则返回 false</returns>
        public async Task<bool> WaitUntilMaterialPulledOutAsync(int timeoutMilliseconds = 5000, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 8.阻塞等待回退物料完成（标志位）
        /// </summary>
        /// <param name="timeoutMilliseconds">超时时间，防止死等</param>
        /// <param name="token">取消令牌</param>
        /// <returns>在规定时间内完成返回 true，否则返回 false</returns>
        public async Task<bool> WaitUntilMaterialReturnedAsync(int timeoutMilliseconds = 5000, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}