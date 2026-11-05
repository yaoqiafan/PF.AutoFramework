using PF.Core.Attributes;
using PF.Core.Constants;
using System.Windows.Controls;

namespace PF.Modules.Debug.Views
{
    /// <summary>语音播报调试视图（仅超级用户可见，权限在 DebugModule 中注册）</summary>
    [ModuleNavigation(NavigationConstants.Views.SpeechDebugView, "语音播报调试", GroupName = "系统调试", Icon = "VolumeHigh", Order = 5)]
    public partial class SpeechDebugView : UserControl
    {
        /// <summary>初始化语音播报调试视图</summary>
        public SpeechDebugView()
        {
            InitializeComponent();
        }
    }
}
