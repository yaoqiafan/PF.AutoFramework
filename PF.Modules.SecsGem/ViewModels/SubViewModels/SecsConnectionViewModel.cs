using PF.Core.Entities.SecsGem.Params;
using PF.Core.Enums;
using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.Core.Interfaces.SecsGem.Params;
using PF.SecsGem.DataBase.Entities.Variable;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PF.Modules.SecsGem.ViewModels.SubViewModels
{
    /// <summary>
    /// 负责设备连接、初始化、断开以及连接状态属性。
    /// </summary>
    public class SecsConnectionViewModel : BindableBase
    {
        private readonly ISecsGemManger _manager;
        private readonly ISecsGemDataBase _db;
        private readonly SecsLogViewModel _log;
        private readonly DispatcherTimer _timer;

        public SecsConnectionViewModel(ISecsGemManger manager, ISecsGemDataBase db, SecsLogViewModel log)
        {
            _manager = manager;
            _db      = db;
            _log     = log;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += (_, _) =>
            {
                bool actual = _manager.IsConnected;
                if (actual != IsConnected)
                    IsConnected = actual;
            };

            InitializeCommand = new DelegateCommand(
                async () => await ExecuteInitializeAsync(),
                () => !IsInitializing)
                .ObservesProperty(() => IsInitializing);

            ConnectCommand = new DelegateCommand(
                async () => await ExecuteConnectAsync(),
                () => !IsConnected && !IsConnecting)
                .ObservesProperty(() => IsConnected)
                .ObservesProperty(() => IsConnecting);

            DisconnectCommand = new DelegateCommand(
                async () => await ExecuteDisconnectAsync(),
                () => IsConnected)
                .ObservesProperty(() => IsConnected);
        }

        // ── 连接状态属性 ───────────────────────────────────────────────────────

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    RaisePropertyChanged(nameof(StatusColor));
                    RaisePropertyChanged(nameof(ConnectionStatusText));
                }
            }
        }

        private bool _isInitializing;
        public bool IsInitializing
        {
            get => _isInitializing;
            set => SetProperty(ref _isInitializing, value);
        }

        private bool _isConnecting;
        public bool IsConnecting
        {
            get => _isConnecting;
            set => SetProperty(ref _isConnecting, value);
        }

        public string StatusColor          => IsConnected ? "#4CAF50" : "#F44336";
        public string ConnectionStatusText => IsConnected ? "已连接 (Connected)" : "未连接 (Disconnected)";

        private bool _isDbEmpty;
        public bool IsDbEmpty
        {
            get => _isDbEmpty;
            set => SetProperty(ref _isDbEmpty, value);
        }

        private string _dbEmptyMessage;
        public string DbEmptyMessage
        {
            get => _dbEmptyMessage;
            set => SetProperty(ref _dbEmptyMessage, value);
        }

        // ── 命令 ───────────────────────────────────────────────────────────────

        public DelegateCommand InitializeCommand  { get; }
        public DelegateCommand ConnectCommand     { get; }
        public DelegateCommand DisconnectCommand  { get; }

        // ── 生命周期 ───────────────────────────────────────────────────────────

        /// <summary>同步初始连接状态并启动轮询定时器。</summary>
        public void StartMonitoring()
        {
            IsConnected = _manager.IsConnected;
            _timer.Start();
        }

        /// <summary>停止轮询定时器。</summary>
        public void StopMonitoring() => _timer.Stop();

        // ── 数据库空库检测 ─────────────────────────────────────────────────────

        public async Task CheckDbEmptyAsync()
        {
            try
            {
                var vidRepo  = _db.GetRepository<VIDEntity>(SecsDbSet.VIDs);
                int vidCount = await vidRepo.CountAsync();

                var sysParam = _manager.ParamsManager.GetParamOrDefault<SecsGemSystemParam>(ParamType.System, null);
                bool sysEmpty = sysParam == null || string.IsNullOrEmpty(sysParam.IPAddress);
                bool isEmpty  = vidCount == 0 || sysEmpty;

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsDbEmpty      = isEmpty;
                    DbEmptyMessage = isEmpty
                        ? "⚠  系统参数或变量库 (VID) 为空，请先从右侧面板导入配置文件"
                        : string.Empty;
                });
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsDbEmpty      = true;
                    DbEmptyMessage = $"⚠  数据库检测异常: {ex.Message}";
                });
            }
        }

        // ── 命令实现 ───────────────────────────────────────────────────────────

        private async Task ExecuteInitializeAsync()
        {
            IsInitializing = true;
            try
            {
                bool ok = await _manager.InitializeAsync();
                _log.Append(null, ok ? "初始化成功" : "初始化失败", isSystem: true);
                IsConnected = _manager.IsConnected;
            }
            catch (Exception ex)
            {
                _log.Append(null, $"初始化异常: {ex.Message}", isSystem: true);
            }
            finally
            {
                IsInitializing = false;
            }
        }

        private async Task ExecuteConnectAsync()
        {
            IsConnecting = true;
            try
            {
                bool ok = await _manager.ConnectAsync();
                IsConnected = ok;
                _log.Append(null, ok ? "连接成功" : "连接失败", isSystem: true);
            }
            catch (Exception ex)
            {
                _log.Append(null, $"连接异常: {ex.Message}", isSystem: true);
            }
            finally
            {
                IsConnecting = false;
            }
        }

        private async Task ExecuteDisconnectAsync()
        {
            try
            {
                await _manager.DisconnectAsync();
                IsConnected = false;
                _log.Append(null, "已断开连接", isSystem: true);
            }
            catch (Exception ex)
            {
                _log.Append(null, $"断开异常: {ex.Message}", isSystem: true);
            }
        }
    }
}
