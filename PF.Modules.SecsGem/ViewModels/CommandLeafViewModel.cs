using PF.Core.Entities.SecsGem.Command;
using Prism.Mvvm;

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
        }

        public SFCommand Command { get; }

        /// <summary>
        /// 显示名称格式: "S{Stream}F{Function} {Name}"
        /// </summary>
        public string DisplayName => $"S{Command.Stream}F{Command.Function}  {Command.Name}";

        public uint Stream => Command.Stream;
        public uint Function => Command.Function;

        /// <summary>
        /// 奇数 Function = 主动请求；偶数 = 应答
        /// </summary>
        public bool IsRequest => Command.Function % 2 == 1;
    }
}
