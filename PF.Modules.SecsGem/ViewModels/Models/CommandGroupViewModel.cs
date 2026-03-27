using PF.Core.Entities.SecsGem.Command;
using PF.Core.Interfaces.SecsGem.Command;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Windows;

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

        private async void ExecuteAddCommand()
        {
            try
            {
                // 传入当前组的 S 和 F 作为默认值
                var dialog = new CommandEditDialog(Stream, Function);
                if (dialog.ShowDialog() == true)
                {
                    var newCommand = new SFCommand
                    {
                        Stream = dialog.Stream,
                        Function = dialog.Function,
                        Name = dialog.CommandName,
                        ID = Guid.NewGuid().ToString("N")[..8],
                        Message = new PF.Core.Entities.SecsGem.Message.SecsGemMessage
                        {
                            Stream = (int)dialog.Stream,
                            Function = (int)dialog.Function,
                            WBit = dialog.Function % 2 == 1,
                            SystemBytes = new System.Collections.Generic.List<byte> { 0, 0, 0, 0 },
                            MessageId = Guid.NewGuid().ToString(),
                            RootNode = new PF.Core.Entities.SecsGem.Message.SecsGemNodeMessage
                            {
                                DataType = PF.Core.Enums.DataType.LIST,
                                Length = 0,
                                SubNode = new System.Collections.Generic.List<PF.Core.Entities.SecsGem.Message.SecsGemNodeMessage>()
                            }
                        }
                    };

                    bool added = await _commandStore.AddCommand(newCommand);
                    if (added)
                    {
                        Application.Current?.Dispatcher.Invoke(() =>
                            Children.Add(new CommandLeafViewModel(newCommand)));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加命令失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
