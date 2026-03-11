using PF.Core.Interfaces.SecsGem;
using Prism.Mvvm;

namespace PF.Modules.SecsGem.ViewModels
{
    public class ViewAViewModel : BindableBase
    {
        private readonly ISecsGemManger _secsGemManger;

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        public ViewAViewModel(ISecsGemManger secsGemManger)
        {
            _secsGemManger = secsGemManger;
            _secsGemManger.MessageReceived += OnMessageReceived;
            IsConnected = _secsGemManger.IsConnected;
        }

        private void OnMessageReceived(object sender, SecsMessageReceivedEventArgs e)
        {
            // 处理接收到的 SECS/GEM 消息
        }
    }
}
