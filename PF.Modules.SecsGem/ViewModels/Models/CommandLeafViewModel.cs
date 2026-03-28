using PF.Core.Entities.SecsGem.Command;
using Prism.Commands;
using Prism.Mvvm;
using System;

namespace PF.Modules.SecsGem.ViewModels
{
    /// <summary>
    /// 命令树叶子节点：代表一条具体的 SFCommand
    /// </summary>
    public class CommandLeafViewModel : BindableBase
    {
        public CommandLeafViewModel(SFCommand command)
        {
            Command = command;
            DeleteCommand = new DelegateCommand(ExecuteDelete);
        }

        public SFCommand Command { get; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName => $"{Command.Name}";

        public uint Stream => Command.Stream;
        public uint Function => Command.Function;

        /// <summary>
        /// 奇数 Function = 主动请求；偶数 = 应答
        /// </summary>
        public bool IsRequest => Command.Function % 2 == 1;

        // ──────────────────────────────────────────────
        // 删除功能
        // ──────────────────────────────────────────────

        public DelegateCommand DeleteCommand { get; }

        /// <summary>
        /// 删除请求事件，由 SecsGemDebugViewModel 订阅并处理确认及后端删除逻辑。
        /// </summary>
        public event Action<CommandLeafViewModel> DeleteRequested;

        private void ExecuteDelete()
        {
            DeleteRequested?.Invoke(this);
        }
    }
}
