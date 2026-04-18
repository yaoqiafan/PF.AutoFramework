using PF.Core.Interfaces.Device.Mechanisms;
using Prism.Mvvm;
using System.Windows.Media;

namespace PF.Modules.Debug.Models
{
    /// <summary>
    /// 模组导航条目，封装 IMechanism 的状态信息供左侧 ListBox 绑定。
    /// </summary>
    public class MechanismNavItem : BindableBase
    {
        private static readonly Brush AlarmBrush = new SolidColorBrush(Color.FromRgb(0xDB, 0x33, 0x40));
        private static readonly Brush ReadyBrush = new SolidColorBrush(Color.FromRgb(0x02, 0xAD, 0x8B));
        private static readonly Brush IdleBrush = new SolidColorBrush(Color.FromRgb(0x32, 0x6C, 0xF3));

        /// <summary>获取导航列表中的显示标题</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>获取 Prism 区域导航的对应视图名称</summary>
        public string ViewName { get; init; } = string.Empty;

        /// <summary>内部持有的底层模组实例引用</summary>
        internal IMechanism Mechanism { get; init; } = null!;

        private bool _isInitialized;
        /// <summary>获取模组是否已初始化</summary>
        public bool IsInitialized
        {
            get => _isInitialized;
            private set => SetProperty(ref _isInitialized, value);
        }

        private bool _hasAlarm;
        /// <summary>获取模组是否存在报警</summary>
        public bool HasAlarm
        {
            get => _hasAlarm;
            private set => SetProperty(ref _hasAlarm, value);
        }

        /// <summary>获取模组状态对应的颜色画刷</summary>
        public Brush StateBrush
        {
            get
            {
                if (_hasAlarm) return AlarmBrush;
                return _isInitialized ? ReadyBrush : IdleBrush;
            }
        }

        /// <summary>获取模组状态的中文描述文本</summary>
        public string StateText
        {
            get
            {
                if (_hasAlarm) return "报警";
                return _isInitialized ? "就绪" : "未初始化";
            }
        }

        /// <summary>
        /// 由 ViewModel 的定时器统一调用，从底层 <see cref="IMechanism"/> 拉取并刷新最新状态。
        /// </summary>
        internal void Refresh()
        {
            if (Mechanism == null) return;

            var prevInitialized = _isInitialized;
            var prevAlarm = _hasAlarm;

            IsInitialized = Mechanism.IsInitialized;
            HasAlarm = Mechanism.HasAlarm;

            if (prevInitialized != _isInitialized || prevAlarm != _hasAlarm)
            {
                RaisePropertyChanged(nameof(StateBrush));
                RaisePropertyChanged(nameof(StateText));
            }
        }
    }
}
