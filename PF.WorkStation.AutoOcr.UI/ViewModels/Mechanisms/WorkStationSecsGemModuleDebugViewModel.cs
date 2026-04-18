using PF.Core.Interfaces.Device.Mechanisms;
using PF.UI.Infrastructure.PrismBase;
using PF.WorkStation.AutoOcr.Mechanisms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PF.WorkStation.AutoOcr.UI.ViewModels.Mechanisms
{
    /// <summary>
    /// WorkStationSecsGemModuleDebugViewModel
    /// </summary>
    public  class WorkStationSecsGemModuleDebugViewModel : RegionViewModelBase
    {
        private readonly WorkStationSecsGemModule? _secsgemModule;
        /// <summary>
        /// 获取或设置 SecsGemModule
        /// </summary>

        public WorkStationSecsGemModule? SecsGemModule => _secsgemModule;

        private string _debugMessage = "就绪";
        /// <summary>
        /// 成员
        /// </summary>
        public string DebugMessage
        {
            get => _debugMessage;
            set => SetProperty(ref _debugMessage, value);
        }

      

        #region Commands 定义
        // 1. 顶部全局生命周期控制
        /// <summary>
        /// InitializeModule 命令
        /// </summary>
        public DelegateCommand InitializeModuleCommand { get; }
        /// <summary>
        /// ResetModule 命令
        /// </summary>
        public DelegateCommand ResetModuleCommand { get; }
        /// <summary>
        /// Stop 命令
        /// </summary>
        public DelegateCommand StopCommand { get; }
        #endregion Commands 定义
        /// <summary>
        /// WorkStationSecsGemModuleDebugViewModel 构造函数
        /// </summary>

        public WorkStationSecsGemModuleDebugViewModel(IContainerProvider containerProvider)
        {
            _secsgemModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationSecsGemModule)) as WorkStationSecsGemModule;

            // --- 绑定全局生命周期指令 ---
            InitializeModuleCommand = new DelegateCommand(async () => await ExecuteAsync(() => _secsgemModule?.InitializeAsync()));
            ResetModuleCommand = new DelegateCommand(async () => await ExecuteAsync(() => _secsgemModule?.ResetAsync()));
            StopCommand = new DelegateCommand(async () => await ExecuteAsync(() => _secsgemModule?.StopAsync()));
        }



        #region 内部执行逻辑与状态更新

        private async Task ExecuteAsync(Func<Task>? action)
        {
            if (action == null) return;
            try
            {
                DebugMessage = "执行中...";
                await action.Invoke();
                DebugMessage = "执行成功";
            }
            catch (Exception ex)
            {
                DebugMessage = $"执行异常: {ex.Message}";
                MessageService.ShowMessage(ex.Message, "调试面板报错", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion 内部执行逻辑与状态更新


    }
}
