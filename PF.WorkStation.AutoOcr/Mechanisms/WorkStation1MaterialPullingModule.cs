using PF.Core.Attributes;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Mechanisms;
using PF.Workstation.AutoOcr.CostParam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Mechanisms
{
    [MechanismUI("工位1推拉晶圆模组", "WorkStation1MaterialPullingModuleDebugView", 1)]
    public class WorkStation1MaterialPullingModule : BaseMechanism
    {



        #region 轴点枚举定义


        public enum YAxisPoint
        {
            待机位置,
            晶圆取料位置,
            晶圆拉出位置,
        }

        #endregion 轴点枚举定义
        private IAxis _yAxis;


        public IAxis YAxis => _yAxis;


        private IIOController _io;

        public IIOController IO => _io;


        /// <summary>
        /// 当前生产晶圆尺寸
        /// </summary>
        private E_WafeSize _currentWafeSize = E_WafeSize._12寸;

        public WorkStation1MaterialPullingModule(IHardwareManagerService hardwareManagerService, IParamService paramService, ILogService logger) : base(E_Mechanisms.工位1推拉晶圆模组.ToString(), hardwareManagerService, paramService, logger)
        {


        }


        // ── BaseMechanism 框架钩子实现 ─────────────────────────────────────

        /// <summary>
        /// 模组初始化核心逻辑：延迟解析轴和IO → 注册报警聚合 → 连接/使能/回零
        /// </summary>
        protected override async Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            // ① 延迟解析硬件实例：通过硬件管理器及配置名称查找具体硬件对象
            _yAxis = HardwareManagerService?.GetDevice(E_AxisName.工位1拉料Y轴.ToString()) as IAxis;
            if (_yAxis == null)
            {
                _logger.Error($"[{MechanismName}] 未找到Y轴 '{E_AxisName.工位1拉料Y轴}'，请确认硬件配置。");
                return false;
            }
            _io = HardwareManagerService?.GetDevice("IO_Collectorll") as IIOController;
            if (_io == null)
            {
                _logger.Error($"[{MechanismName}] 未找到IO模块 'IO_Collectorll'，请确认硬件配置。");
                return false;
            }

            // ② 将硬件注册到当前模组：这样当单一轴/IO发生报警时，整个模组的状态会自动同步为 Alarm
            RegisterHardwareDevice(_yAxis as IHardwareDevice);
            RegisterHardwareDevice(_io as IHardwareDevice);

            // 确认点位枚举在轴配置中都已示教/创建
            await ConfirmEunmPoints();

            // ③ 并行/串行连接所有硬件通信
            if (!await _yAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] Z轴连接失败"); return false; }
            if (!await _io.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] IO模块连接失败"); return false; }

            // ④ 使能伺服电机（Power On）
            if (!await _yAxis.EnableAsync()) { _logger.Error($"[{MechanismName}] Z轴使能失败"); return false; }

            return true;
        }

        protected override async Task InternalStopAsync()
        {
            if (_yAxis != null)
            {
                await _yAxis.StopAsync();
            }
        }




        #region 内部辅助数学/验证方法


        /// <summary>
        /// 验证当前轴的枚举点位是否在底层配置中全部存在
        /// 防止因为漏建点位导致程序在运行中引发空引用异常（NullReferenceException）
        /// </summary>
        public async Task ConfirmEunmPoints()
        {
            if (_yAxis != null) EnsurePointsExist<YAxisPoint>(_yAxis);
            await Task.CompletedTask; // 满足 async 签名要求
        }


        #endregion 内部辅助数学/验证方法
    }
}
