using PF.Core.Entities.SecsGem.Command;
using PF.Core.Interfaces.SecsGem.Command;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;

namespace PF.Modules.SecsGem.ViewModels
{
    /// <summary>
    /// 命令树父节点：以 SxFx 分组（如 S1F1）
    /// </summary>
    public class CommandGroupViewModel : BindableBase
    {
        private readonly ISFCommand _commandStore;

        public CommandGroupViewModel(uint stream, uint function, ISFCommand commandStore)
        {
            Stream = stream;
            Function = function;
            _commandStore = commandStore;
            Children = new ObservableCollection<CommandLeafViewModel>();
            AddCommandCommand = new DelegateCommand(ExecuteAddCommand);
        }

        public uint Stream { get; }
        public uint Function { get; }

        /// <summary>
        /// 分组键，如 "S1F1"
        /// </summary>
        public string GroupKey => $"S{Stream}F{Function}";

        public string DisplayName => GroupKey;

        public ObservableCollection<CommandLeafViewModel> Children { get; }

        /// <summary>
        /// 右键菜单"添加命令"
        /// </summary>
        public DelegateCommand AddCommandCommand { get; }

        /// <summary>
        /// 请求由外部（SecsGemDebugViewModel）弹出命令编辑对话框并处理添加逻辑。
        /// 参数：defaultStream, defaultFunction
        /// </summary>
        public event Action<uint, uint> AddCommandFromGroupRequested;

        private void ExecuteAddCommand()
        {
            AddCommandFromGroupRequested?.Invoke(Stream, Function);
        }
    }
}
