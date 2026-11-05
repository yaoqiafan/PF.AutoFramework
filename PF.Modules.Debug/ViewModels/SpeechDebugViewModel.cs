using Microsoft.Win32;
using PF.Core.Constants;
using PF.Core.Entities.Speech;
using PF.Core.Enums;
using PF.Core.Interfaces.Speech;
using PF.Modules.Debug.Models;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 语音播报调试 ViewModel：<see cref="ISpeechService"/> 的设置面板 + 试播 + 常用语 + 生成 WAV + 播报记录。
    /// <para>
    /// 设置直接改全局唯一的服务实例，会立即影响运行中的工站播报，服务负责防抖保存到 speech_config.json。
    /// 页面不另存一份副本，离开页面也不回滚——现场需求就是"调到合适就走"。
    /// </para>
    /// <para>
    /// 播报记录由服务在内存里保留最近 200 条（含页面关闭期间工站发出的播报），进页面时取一次快照，
    /// 之后订阅 RecordUpdated 增量刷新；正在播报 / 排队数没有事件，用 300ms 定时器刷新。
    /// </para>
    /// </summary>
    public class SpeechDebugViewModel : RegionViewModelBase
    {
        /// <summary>调试页发出的播报在记录里的来源名</summary>
        private const string PageSource = "调试页";
        private const int MaxRecords = 200;

        private readonly ISpeechService _speech;
        private readonly DispatcherTimer _statusTimer;
        private readonly Dispatcher _dispatcher;
        private readonly Dictionary<long, SpeechRecordItem> _recordIndex = new();
        private bool _subscribed;

        /// <summary>创建语音播报调试 ViewModel。</summary>
        public SpeechDebugViewModel(ISpeechService speech)
        {
            _speech = speech;
            _dispatcher = Application.Current.Dispatcher;

            RecordsView = CollectionViewSource.GetDefaultView(Records);
            RecordsView.Filter = o => !OnlyAbnormal || (o is SpeechRecordItem r && r.IsAbnormal);

            SpeakCommand = new DelegateCommand(() => SpeakText(TestText, SpeechPriority.Normal), CanSpeakTestText)
                .ObservesProperty(() => TestText).ObservesProperty(() => IsMuted);
            SpeakHighCommand = new DelegateCommand(() => SpeakText(TestText, SpeechPriority.High), CanSpeakTestText)
                .ObservesProperty(() => TestText).ObservesProperty(() => IsMuted);
            StopAllCommand = new DelegateCommand(() => _speech.CancelAll(), () => IsAvailable);
            PreviewCommand = new DelegateCommand(
                () => SpeakText($"语音播报测试，当前音量 {Volume}，语速 {Rate}", SpeechPriority.Normal),
                () => IsAvailable && !IsMuted).ObservesProperty(() => IsMuted);
            ResetSettingsCommand = new DelegateCommand(async () => await ResetSettingsAsync(), () => IsAvailable);

            PlayPhraseCommand = new DelegateCommand<SpeechPhraseItem>(
                p => SpeakText(p?.Text, p?.IsHighPriority == true ? SpeechPriority.High : SpeechPriority.Normal),
                _ => IsAvailable && !IsMuted).ObservesProperty(() => IsMuted);
            TogglePhrasePriorityCommand = new DelegateCommand<SpeechPhraseItem>(TogglePhrasePriority);
            RemovePhraseCommand = new DelegateCommand<SpeechPhraseItem>(RemovePhrase);
            AddPhraseCommand = new DelegateCommand(AddPhrase, () => !string.IsNullOrWhiteSpace(NewPhraseText))
                .ObservesProperty(() => NewPhraseText);

            BrowseWavCommand = new DelegateCommand(BrowseWav);
            GenerateWavCommand = new DelegateCommand(async () => await GenerateWavAsync(),
                    () => IsAvailable && !IsGenerating && !string.IsNullOrWhiteSpace(TestText) && !string.IsNullOrWhiteSpace(WavPath))
                .ObservesProperty(() => IsGenerating).ObservesProperty(() => TestText).ObservesProperty(() => WavPath);
            OpenWavFolderCommand = new DelegateCommand(OpenWavFolder);
            ClearRecordsCommand = new DelegateCommand(ClearRecords);

            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _statusTimer.Tick += (_, _) => RefreshQueueStatus();

            WavPath = BuildDefaultWavPath();
        }

        #region 导航生命周期

        /// <summary>进入页面：加载服务当前设置、常用语和最近记录，开始订阅。</summary>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);
            LoadFromService();

            if (!_subscribed)
            {
                _speech.RecordUpdated += OnRecordUpdated;
                _speech.SettingsSaved += OnSettingsSaved;
                _subscribed = true;
            }
            _statusTimer.Start();
        }

        /// <summary>离开页面：退订并停止刷新。</summary>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            Unsubscribe();
        }

        /// <summary>销毁时兜底退订（服务是单例，不退订会让本页实例一直被引用）。</summary>
        public override void Destroy()
        {
            Unsubscribe();
            base.Destroy();
        }

        private void Unsubscribe()
        {
            _statusTimer.Stop();
            if (!_subscribed) return;
            _speech.RecordUpdated -= OnRecordUpdated;
            _speech.SettingsSaved -= OnSettingsSaved;
            _subscribed = false;
        }

        private void LoadFromService()
        {
            IsAvailable = _speech.IsAvailable;
            Voices = _speech.GetInstalledVoices();

            _loading = true;
            try
            {
                SelectedVoice = _speech.CurrentVoice;
                Volume = _speech.Volume;
                Rate = _speech.Rate;
                IsMuted = _speech.IsMuted;
            }
            finally
            {
                _loading = false;
            }

            Phrases.Clear();
            foreach (var p in _speech.GetPhrases())
                Phrases.Add(new SpeechPhraseItem(p.Text, p.IsHighPriority));

            Records.Clear();
            _recordIndex.Clear();
            foreach (var r in _speech.GetRecentRecords())   // 新的在前，与表格顺序一致
            {
                var item = new SpeechRecordItem(r);
                Records.Add(item);
                _recordIndex[r.Id] = item;
            }

            RaisePropertyChanged(nameof(ConfigFilePathText));
            RefreshSavedText();
            RefreshQueueStatus();
            StopAllCommand.RaiseCanExecuteChanged();
            ResetSettingsCommand.RaiseCanExecuteChanged();
            PreviewCommand.RaiseCanExecuteChanged();
            PlayPhraseCommand.RaiseCanExecuteChanged();
            GenerateWavCommand.RaiseCanExecuteChanged();
            SpeakCommand.RaiseCanExecuteChanged();
            SpeakHighCommand.RaiseCanExecuteChanged();
        }

        #endregion

        #region 标题栏状态

        private bool _isAvailable;
        /// <summary>引擎是否可用；不可用时整页输入置灰</summary>
        public bool IsAvailable
        {
            get => _isAvailable;
            private set
            {
                if (SetProperty(ref _isAvailable, value)) RaisePropertyChanged(nameof(IsUnavailable));
            }
        }

        /// <summary>引擎不可用（控制提示条显示）</summary>
        public bool IsUnavailable => !IsAvailable;

        private string _currentText = "空闲";
        /// <summary>正在播报的文字</summary>
        public string CurrentText { get => _currentText; private set => SetProperty(ref _currentText, value); }

        private bool _isPlaying;
        /// <summary>是否正在播报</summary>
        public bool IsPlaying { get => _isPlaying; private set => SetProperty(ref _isPlaying, value); }

        private string _pendingText = "高 0 / 普通 0";
        /// <summary>排队数</summary>
        public string PendingText { get => _pendingText; private set => SetProperty(ref _pendingText, value); }

        /// <summary>设置文件路径（未持久化时给出说明）</summary>
        public string ConfigFilePathText => _speech.ConfigFilePath ?? "配置路径未初始化，设置不会保存";

        private void RefreshQueueStatus()
        {
            var text = _speech.CurrentText;
            IsPlaying = text != null;
            CurrentText = text ?? "空闲";
            PendingText = $"高 {_speech.PendingHighCount} / 普通 {_speech.PendingNormalCount}";
        }

        #endregion

        #region 播报设置

        /// <summary>加载服务当前值期间为 true，避免回写服务、触发无意义的保存</summary>
        private bool _loading;

        private IReadOnlyList<string> _voices = Array.Empty<string>();
        /// <summary>已安装语音</summary>
        public IReadOnlyList<string> Voices { get => _voices; private set => SetProperty(ref _voices, value); }

        private string? _selectedVoice;
        /// <summary>当前语音</summary>
        public string? SelectedVoice
        {
            get => _selectedVoice;
            set
            {
                if (!SetProperty(ref _selectedVoice, value) || _loading || value == null) return;
                if (!_speech.SelectVoice(value))
                {
                    _loading = true;
                    try { SelectedVoice = _speech.CurrentVoice; } finally { _loading = false; }
                }
            }
        }

        private double _volume;
        /// <summary>音量 0~100（滑块与数值框共用）</summary>
        public double Volume
        {
            get => _volume;
            set
            {
                var v = Math.Round(Math.Clamp(value, 0, 100));
                if (SetProperty(ref _volume, v) && !_loading) _speech.Volume = (int)v;
            }
        }

        private double _rate;
        /// <summary>语速 -10~10（滑块与数值框共用）</summary>
        public double Rate
        {
            get => _rate;
            set
            {
                var v = Math.Round(Math.Clamp(value, -10, 10));
                if (SetProperty(ref _rate, v) && !_loading) _speech.Rate = (int)v;
            }
        }

        private bool _isMuted;
        /// <summary>静音</summary>
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (SetProperty(ref _isMuted, value) && !_loading) _speech.IsMuted = value;
            }
        }

        private string _savedText = string.Empty;
        /// <summary>保存状态提示</summary>
        public string SavedText { get => _savedText; private set => SetProperty(ref _savedText, value); }

        private void OnSettingsSaved(object? sender, EventArgs e)
            => _dispatcher.BeginInvoke(RefreshSavedText);

        private void RefreshSavedText()
        {
            SavedText = _speech.ConfigFilePath == null ? "设置不会保存（配置路径未初始化）"
                      : _speech.LastSavedTime is { } t ? $"✔ 已保存 {t:HH:mm:ss}"
                      : "修改即生效并自动保存";
        }

        /// <summary>试听当前设置</summary>
        public DelegateCommand PreviewCommand { get; }

        /// <summary>恢复默认设置</summary>
        public DelegateCommand ResetSettingsCommand { get; }

        private async Task ResetSettingsAsync()
        {
            var result = await MessageService.ShowMessageAsync(
                "语音、音量、语速、静音将恢复默认（中文语音、音量 100、语速 0、不静音），常用语不变。\n确定恢复？",
                "恢复默认", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (result != ButtonResult.OK) return;

            _speech.ResetSettings();
            LoadFromService();
        }

        #endregion

        #region 播报测试

        private string _testText = string.Empty;
        /// <summary>测试文字（生成 WAV 也用它）</summary>
        public string TestText { get => _testText; set => SetProperty(ref _testText, value); }

        /// <summary>播报</summary>
        public DelegateCommand SpeakCommand { get; }

        /// <summary>插队播报</summary>
        public DelegateCommand SpeakHighCommand { get; }

        /// <summary>停止全部</summary>
        public DelegateCommand StopAllCommand { get; }

        private bool CanSpeakTestText() => IsAvailable && !IsMuted && !string.IsNullOrWhiteSpace(TestText);

        private void SpeakText(string? text, SpeechPriority priority)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _speech.Speak(text, priority, PageSource);
        }

        #endregion

        #region 常用语

        /// <summary>常用语</summary>
        public ObservableCollection<SpeechPhraseItem> Phrases { get; } = new();

        private string _newPhraseText = string.Empty;
        /// <summary>待添加的常用语文字</summary>
        public string NewPhraseText { get => _newPhraseText; set => SetProperty(ref _newPhraseText, value); }

        private bool _newPhraseIsHigh;
        /// <summary>待添加的常用语是否插队</summary>
        public bool NewPhraseIsHigh { get => _newPhraseIsHigh; set => SetProperty(ref _newPhraseIsHigh, value); }

        /// <summary>播报常用语</summary>
        public DelegateCommand<SpeechPhraseItem> PlayPhraseCommand { get; }

        /// <summary>切换常用语优先级</summary>
        public DelegateCommand<SpeechPhraseItem> TogglePhrasePriorityCommand { get; }

        /// <summary>删除常用语</summary>
        public DelegateCommand<SpeechPhraseItem> RemovePhraseCommand { get; }

        /// <summary>添加常用语</summary>
        public DelegateCommand AddPhraseCommand { get; }

        private void AddPhrase()
        {
            var text = NewPhraseText.Trim();
            if (text.Length == 0) return;
            if (Phrases.Any(p => p.Text == text))
            {
                MessageService.ShowMessage($"常用语「{text}」已存在。", "添加常用语", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Phrases.Add(new SpeechPhraseItem(text, NewPhraseIsHigh));
            NewPhraseText = string.Empty;
            NewPhraseIsHigh = false;
            SavePhrases();
        }

        private void RemovePhrase(SpeechPhraseItem? item)
        {
            if (item == null || !Phrases.Remove(item)) return;
            SavePhrases();
        }

        private void TogglePhrasePriority(SpeechPhraseItem? item)
        {
            if (item == null) return;
            item.IsHighPriority = !item.IsHighPriority;
            SavePhrases();
        }

        private void SavePhrases()
            => _speech.SetPhrases(Phrases.Select(p => new SpeechPhrase { Text = p.Text, IsHighPriority = p.IsHighPriority }));

        #endregion

        #region 生成 WAV

        private string _wavPath = string.Empty;
        /// <summary>WAV 输出路径</summary>
        public string WavPath { get => _wavPath; set => SetProperty(ref _wavPath, value); }

        private bool _isGenerating;
        /// <summary>正在生成</summary>
        public bool IsGenerating { get => _isGenerating; private set => SetProperty(ref _isGenerating, value); }

        private string _wavResultText = string.Empty;
        /// <summary>生成结果提示</summary>
        public string WavResultText { get => _wavResultText; private set => SetProperty(ref _wavResultText, value); }

        private bool _wavResultOk = true;
        /// <summary>生成结果是否成功（控制提示颜色）</summary>
        public bool WavResultOk { get => _wavResultOk; private set => SetProperty(ref _wavResultOk, value); }

        /// <summary>浏览输出路径</summary>
        public DelegateCommand BrowseWavCommand { get; }

        /// <summary>生成 WAV 文件</summary>
        public DelegateCommand GenerateWavCommand { get; }

        /// <summary>打开 WAV 所在文件夹</summary>
        public DelegateCommand OpenWavFolderCommand { get; }

        private static string BuildDefaultWavPath()
        {
            string dir;
            try { dir = Path.Combine(ConstGlobalParam.ConfigPath, "Speech"); }
            catch (InvalidOperationException) { dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); }
            return Path.Combine(dir, $"speech_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
        }

        private void BrowseWav()
        {
            var dialog = new SaveFileDialog
            {
                Title = "选择 WAV 输出路径",
                Filter = "WAV 音频 (*.wav)|*.wav",
                DefaultExt = ".wav",
                AddExtension = true,
                OverwritePrompt = true
            };
            try
            {
                var dir = Path.GetDirectoryName(WavPath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) dialog.InitialDirectory = dir;
                dialog.FileName = Path.GetFileName(WavPath);
            }
            catch (ArgumentException) { /* 手输的路径不合法，按默认目录打开 */ }

            if (dialog.ShowDialog() == true) WavPath = dialog.FileName;
        }

        private async Task GenerateWavAsync()
        {
            IsGenerating = true;
            WavResultText = "生成中...";
            WavResultOk = true;
            try
            {
                var path = WavPath.Trim();
                var ok = await _speech.SaveToWaveFileAsync(TestText, path);
                WavResultOk = ok;
                WavResultText = ok
                    ? $"✔ 已生成 {DateTime.Now:HH:mm:ss}（{new FileInfo(path).Length / 1024.0:0} KB）"
                    : "✘ 生成失败，详见日志";
            }
            catch (Exception ex)
            {
                WavResultOk = false;
                WavResultText = $"✘ 生成失败：{ex.Message}";
            }
            finally
            {
                IsGenerating = false;
            }
        }

        private void OpenWavFolder()
        {
            try
            {
                var path = Path.GetFullPath(WavPath.Trim());
                if (File.Exists(path))
                {
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                    return;
                }
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    Process.Start("explorer.exe", $"\"{dir}\"");
                else
                    MessageService.ShowMessage("文件夹尚不存在，生成一次文件后会自动创建。", "打开文件夹", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"打开文件夹失败：{ex.Message}", "打开文件夹", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region 播报记录

        /// <summary>播报记录（新的在前）</summary>
        public ObservableCollection<SpeechRecordItem> Records { get; } = new();

        /// <summary>带筛选的记录视图（表格绑定它）</summary>
        public ICollectionView RecordsView { get; }

        private bool _onlyAbnormal;
        /// <summary>只看异常记录</summary>
        public bool OnlyAbnormal
        {
            get => _onlyAbnormal;
            set
            {
                if (SetProperty(ref _onlyAbnormal, value)) RecordsView.Refresh();
            }
        }

        /// <summary>清空记录</summary>
        public DelegateCommand ClearRecordsCommand { get; }

        private void ClearRecords()
        {
            _speech.ClearRecords();
            Records.Clear();
            _recordIndex.Clear();
        }

        private void OnRecordUpdated(object? sender, SpeechRecord record)
            => _dispatcher.BeginInvoke(() => ApplyRecord(record));

        private void ApplyRecord(SpeechRecord record)
        {
            if (!_subscribed) return;

            if (_recordIndex.TryGetValue(record.Id, out var item))
            {
                item.Update(record);
                // 状态变化可能改变"仅异常"筛选结果
                if (OnlyAbnormal) RecordsView.Refresh();
                return;
            }

            // 清空后才结束的那几句不补回来，与服务端一致
            if (record.IsFinished && record.Status != SpeechStatus.Muted) return;

            item = new SpeechRecordItem(record);
            Records.Insert(0, item);
            _recordIndex[record.Id] = item;
            while (Records.Count > MaxRecords)
            {
                var last = Records[^1];
                Records.RemoveAt(Records.Count - 1);
                _recordIndex.Remove(last.Id);
            }
        }

        #endregion
    }
}
