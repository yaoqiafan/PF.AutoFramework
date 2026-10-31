using PF.Core.Enums.Hardware.Vision;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Camera.NetworkCamera;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Windows;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 网络摄像机「分组节点」参数页 ViewModel：列出组内全部摄像机的参数卡片。
    ///
    /// <para><b>本页只做参数编辑，不做调试</b>。连接/断开/预览/抓图都在单台实例页
    /// （<see cref="NetworkCameraDebugViewModel"/>）——两台机器干两件事，不混在一起。</para>
    ///
    /// <para><b>保存不自动重载</b>：<c>ReloadAllAsync</c> 是全量断开所有设备再重新实例化，
    /// 产线运行中触发会打断运动轴/工站/其他相机。本页点「保存全部」只写库，
    /// 顶部提示"需重新加载硬件才生效"，重载由显式按钮（带确认）完成。</para>
    /// </summary>
    public class NetworkCameraGroupViewModel : RegionViewModelBase
    {
        private readonly IHardwareManagerService _hardwareManager;

        /// <summary>以硬件管理服务初始化。</summary>
        public NetworkCameraGroupViewModel(IHardwareManagerService hardwareManager)
        {
            _hardwareManager = hardwareManager;

            SaveAllCommand     = new DelegateCommand(() => _ = SaveAllAsync(), () => HasChanges).ObservesProperty(() => HasChanges);
            RevertAllCommand   = new DelegateCommand(RevertAll, () => HasChanges).ObservesProperty(() => HasChanges);
            RefreshCommand     = new DelegateCommand(LoadCameras);
            ReloadAllHardwareCommand = new DelegateCommand(() => _ = ReloadAllAsync());
        }

        // ── 绑定属性 ─────────────────────────────────────────────────────────────

        /// <summary>组内摄像机的参数卡片集合。</summary>
        public ObservableCollection<NetworkCameraParamCardViewModel> Cards { get; } = new();

        /// <summary>头部汇总信息（文案格式与其余硬件调试页一致）。</summary>
        public string HeaderInfo
            => Cards.Count == 0
                ? "组内无摄像机"
                : $"设备类别: 网络摄像机 | 共 {Cards.Count} 台 | 模拟状态: {(Cards.Any(c => c.IsSimulated) ? "部分" : "否")}";

        private bool _hasChanges;
        /// <summary>是否存在未保存的改动（任意卡片脏即真）。</summary>
        public bool HasChanges
        {
            get => _hasChanges;
            private set
            {
                if (SetProperty(ref _hasChanges, value))
                    RaisePropertyChanged(nameof(ChangeSummary));
            }
        }

        /// <summary>顶部提示文案：几台有改动。</summary>
        public string ChangeSummary
        {
            get
            {
                int dirty = Cards.Count(c => c.IsDirty);
                return dirty == 0 ? string.Empty : $"{dirty} 台摄像机有未保存的改动";
            }
        }

        private bool _isBusy;
        /// <summary>正在执行阻塞操作（保存/重载，防重复点击）。</summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        private string _statusText = string.Empty;
        /// <summary>底部状态文本（保存结果/重载提示）。</summary>
        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        // ── 命令 ─────────────────────────────────────────────────────────────────

        /// <summary>保存全部改动（只写库，不重载）。</summary>
        public DelegateCommand SaveAllCommand { get; }

        /// <summary>撤销全部改动（回到最近一次加载/保存的值）。</summary>
        public DelegateCommand RevertAllCommand { get; }

        /// <summary>重新从设备管理器读取摄像机列表（丢弃未保存改动）。</summary>
        public DelegateCommand RefreshCommand { get; }

        /// <summary>重新加载全部硬件（全量断开重建，带确认）。</summary>
        public DelegateCommand ReloadAllHardwareCommand { get; }

        // ── Prism 导航生命周期 ───────────────────────────────────────────────────

        /// <inheritdoc/>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (navigationContext.Parameters.ContainsKey("Cameras"))
            {
                // 从「网络摄像机」分组节点进入
                var all = navigationContext.Parameters
                    .GetValue<IReadOnlyList<INetworkCamera>>("Cameras");
                LoadCameras(all);
            }
            else
            {
                // 兜底：没带参数时按当前活跃设备里所有网络摄像机加载
                LoadCameras();
            }
        }

        // ── 加载 ─────────────────────────────────────────────────────────────────

        private void LoadCameras()
            => LoadCameras(_hardwareManager.ActiveDevices.OfType<INetworkCamera>());

        private void LoadCameras(IEnumerable<INetworkCamera> cameras)
        {
            DetachCards();

            foreach (var cam in cameras.OrderBy(c => c.DeviceName))
            {
                var card = new NetworkCameraParamCardViewModel(cam, _hardwareManager.GetConfig(cam.DeviceId));
                card.PropertyChanged += OnCardPropertyChanged;
                Cards.Add(card);
            }

            HasChanges = false;
            RaisePropertyChanged(nameof(HeaderInfo));
        }

        /// <summary>卡片脏标记变化 → 汇总到页面级 HasChanges。</summary>
        private void OnCardPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(NetworkCameraParamCardViewModel.IsDirty))
            {
                HasChanges = Cards.Any(c => c.IsDirty);
                RaisePropertyChanged(nameof(ChangeSummary));
            }
        }

        /// <summary>解除所有卡片的订阅（离开页面/重载列表时调用，避免卡片被事件长期持有）。</summary>
        private void DetachCards()
        {
            foreach (var card in Cards)
                card.PropertyChanged -= OnCardPropertyChanged;
            Cards.Clear();
        }

        /// <inheritdoc/>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            // 卡片持有对设备实例的引用且挂了 PropertyChanged 订阅；
            // 本页 KeepAlive 恒为 false，离开即丢实例——不解绑会随设备活到进程结束。
            foreach (var card in Cards)
                card.PropertyChanged -= OnCardPropertyChanged;

            base.OnNavigatedFrom(navigationContext);
        }

        // ── 保存 / 撤销 / 重载 ───────────────────────────────────────────────────

        private async Task SaveAllAsync()
        {
            if (IsBusy) return;

            // 先整体校验：数字字段格式不对就整批不保存，避免"存了一半"的中间态
            foreach (var card in Cards.Where(c => c.IsDirty))
            {
                if (!int.TryParse(card.Port.Trim(), out _))
                {
                    ShowValidation($"【{card.DeviceName}】端口必须是数字");
                    return;
                }
                if (!int.TryParse(card.Channel.Trim(), out _))
                {
                    ShowValidation($"【{card.DeviceName}】通道号必须是数字");
                    return;
                }
                if (!int.TryParse(card.BufferMb.Trim(), out _))
                {
                    ShowValidation($"【{card.DeviceName}】抓图缓冲区(MB)必须是数字");
                    return;
                }
                if (!string.IsNullOrWhiteSpace(card.SaveFormat) &&
                    !Enum.TryParse<ImageFileFormat>(card.SaveFormat.Trim(), true, out _))
                {
                    ShowValidation($"【{card.DeviceName}】存图格式只能是 Bmp / Jpeg / Tiff / Png");
                    return;
                }
            }

            IsBusy = true;
            try
            {
                var saved = new List<string>();
                var failed = new List<string>();

                foreach (var card in Cards.Where(c => c.IsDirty))
                {
                    var config = _hardwareManager.GetConfig(card.DeviceId);
                    if (config == null)
                    {
                        failed.Add($"{card.DeviceName}: 未找到配置记录");
                        continue;
                    }

                    if (!card.ApplyTo(config))
                    {
                        failed.Add($"{card.DeviceName}: 配置写入失败");
                        continue;
                    }

                    try
                    {
                        await _hardwareManager.SaveConfigAsync(config);
                        card.MarkSaved();
                        saved.Add(card.DeviceName);
                    }
                    catch (Exception ex)
                    {
                        // 单台写库失败不影响其余台，逐台记录
                        failed.Add($"{card.DeviceName}: {ex.Message}");
                    }
                }

                HasChanges = Cards.Any(c => c.IsDirty);

                StatusText = failed.Count == 0
                    ? $"已保存 {saved.Count} 台。新配置需重新加载硬件后生效（当前连接仍使用旧参数）。"
                    : $"保存完成：成功 {saved.Count} 台，失败 {failed.Count} 台。";

                if (failed.Count > 0)
                {
                    MessageService.ShowSystemMessage(
                        $"以下摄像机保存失败：\n{string.Join("\n", failed)}",
                        "网络摄像机参数",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RevertAll()
        {
            foreach (var card in Cards)
                card.Revert();

            HasChanges = false;
            StatusText = "已撤销全部未保存的改动。";
        }

        private void ShowValidation(string message)
        {
            StatusText = $"保存失败：{message}";
            MessageService.ShowSystemMessage(message, "网络摄像机参数",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private async Task ReloadAllAsync()
        {
            if (IsBusy) return;

            var result = MessageService.ShowSystemMessage(
                "重新加载将断开【全部】硬件设备并重新实例化（运动轴、工站、其他相机都会被打断）。\n确认继续？",
                "重新加载全部硬件",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                await _hardwareManager.ReloadAllAsync();
                LoadCameras();
                StatusText = "硬件已重新加载，新配置已生效。";
            }
            catch (Exception ex)
            {
                StatusText = $"重新加载失败：{ex.Message}";
                MessageService.ShowSystemMessage(
                    $"重新加载硬件失败：{ex.Message}\n部分设备可能已断开，请检查后在硬件调试页重新连接。",
                    "重新加载全部硬件",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
