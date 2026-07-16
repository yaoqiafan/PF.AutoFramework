using PF.Core.Events;
using PF.Core.Interfaces.Communication;
using PF.Core.Interfaces.Communication.Modbus;
using PF.Modules.Debug.Dialogs;
using PF.Modules.Debug.Models;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Windows;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>Modbus RTU 主站调试 ViewModel，接收 NavigationParameter("Instance") 传入的 IModbusRtuMaster 实例</summary>
    public class ModbusRtuDebugViewModel : RegionViewModelBase
    {
        private readonly ICommunicationManagerService _commManager;
        private IModbusRtuMaster? _master;
        private string _instanceId = string.Empty;

        private string _portName = "——";
        /// <summary>串口名称</summary>
        public string PortName { get => _portName; set => SetProperty(ref _portName, value); }

        private int _baudRate;
        /// <summary>波特率</summary>
        public int BaudRate { get => _baudRate; set => SetProperty(ref _baudRate, value); }

        private string _statusText = "——";
        /// <summary>打开状态文本</summary>
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        private string _logText = string.Empty;
        /// <summary>事件日志全文（最新在前），只读多行文本框展示，支持鼠标选中+Ctrl+C 手动复制</summary>
        public string LogText { get => _logText; set => SetProperty(ref _logText, value); }

        private readonly List<string> _logLines = new();

        /// <summary>可选功能码列表</summary>
        public ObservableCollection<string> FunctionCodes { get; } = new()
        {
            "读线圈(01)", "读离散量输入(02)", "读保持寄存器(03)", "读输入寄存器(04)",
            "写单个线圈(05)", "写单个寄存器(06)", "写多个线圈(0F)", "写多个寄存器(10)"
        };

        private string _selectedFunctionCode = "读保持寄存器(03)";
        /// <summary>当前选中的测试功能码</summary>
        public string SelectedFunctionCode
        {
            get => _selectedFunctionCode;
            set
            {
                if (!SetProperty(ref _selectedFunctionCode, value)) return;
                RaisePropertyChanged(nameof(IsReadFunction));
                RaisePropertyChanged(nameof(IsSingleWriteFunction));
                RaisePropertyChanged(nameof(IsMultiWriteFunction));
                RaisePropertyChanged(nameof(QuantityLabel));
                SyncMultiWriteValues();
            }
        }

        /// <summary>当前是否为读功能码（01~04）</summary>
        public bool IsReadFunction => SelectedFunctionCode is "读线圈(01)" or "读离散量输入(02)" or "读保持寄存器(03)" or "读输入寄存器(04)";
        /// <summary>当前是否为写单个功能码（05/06）——此时 Quantity 不参与，只用 WriteValue 单值输入框</summary>
        public bool IsSingleWriteFunction => SelectedFunctionCode is "写单个线圈(05)" or "写单个寄存器(06)";
        /// <summary>当前是否为写多个功能码（0F/10）——此时改用 MultiWriteValues 逐元素输入</summary>
        public bool IsMultiWriteFunction => SelectedFunctionCode is "写多个线圈(0F)" or "写多个寄存器(10)";

        /// <summary>数量输入框的标签文本，随功能码变化，避免"数量(读)"在写场景下的误导</summary>
        public string QuantityLabel => SelectedFunctionCode switch
        {
            "写单个线圈(05)" or "写单个寄存器(06)" => "数量(单写不使用)",
            "写多个线圈(0F)" or "写多个寄存器(10)" => "数量(写入个数)",
            _ => "数量(读取个数)"
        };

        private byte _unitId = 1;
        /// <summary>从站地址</summary>
        public byte UnitId { get => _unitId; set => SetProperty(ref _unitId, value); }

        private ushort _address;
        /// <summary>起始地址</summary>
        public ushort Address { get => _address; set => SetProperty(ref _address, value); }

        private ushort _quantity = 1;
        /// <summary>读取数量，或写多个操作时的元素个数（写单个操作时不使用，见 QuantityLabel）</summary>
        public ushort Quantity
        {
            get => _quantity;
            set
            {
                if (!SetProperty(ref _quantity, value)) return;
                SyncMultiWriteValues();
            }
        }

        private ushort _writeValue;
        /// <summary>写单个线圈/寄存器时使用的值（写线圈时非 0 视为 ON）；写多个操作改用 MultiWriteValues，不再复用此值</summary>
        public ushort WriteValue { get => _writeValue; set => SetProperty(ref _writeValue, value); }

        /// <summary>
        /// 写多个线圈/寄存器时，每个位置各自的值——随 Quantity/功能码变化自动增删（见 SyncMultiWriteValues），
        /// 修正此前"全部写同一个 WriteValue"、无法逐位置自定义的问题。
        /// </summary>
        public ObservableCollection<ModbusMultiWriteValueItem> MultiWriteValues { get; } = new();

        /// <summary>打开命令</summary>
        public DelegateCommand OpenCommand { get; }
        /// <summary>关闭命令</summary>
        public DelegateCommand CloseCommand { get; }
        /// <summary>执行测试读写命令</summary>
        public DelegateCommand ExecuteTestCommand { get; }
        /// <summary>打开本实例参数修改对话框命令</summary>
        public DelegateCommand ShowParamDialogCommand { get; }

        /// <summary>初始化 Modbus RTU 调试 ViewModel</summary>
        public ModbusRtuDebugViewModel(ICommunicationManagerService commManager)
        {
            _commManager = commManager;
            OpenCommand = new DelegateCommand(async () => { if (_master != null) await _master.OpenAsync(); });
            CloseCommand = new DelegateCommand(async () => { if (_master != null) await _master.CloseAsync(); });
            ExecuteTestCommand = new DelegateCommand(async () => await ExecuteTestAsync());
            ShowParamDialogCommand = new DelegateCommand(ExecuteShowParamDialog);
        }

        /// <summary>
        /// 只有导航目标和当前已绑定的是同一个 InstanceId 才允许复用本实例，
        /// 这样同一个实例的调试页在本次程序运行期间反复进出时，LogText 等状态不会被清空重建。
        /// </summary>
        public override bool IsNavigationTarget(NavigationContext navigationContext)
        {
            if (!navigationContext.Parameters.ContainsKey("Instance")) return false;
            var target = navigationContext.Parameters.GetValue<IModbusRtuMaster>("Instance");
            return target != null && (target as ICommunication)?.InstanceId == _instanceId;
        }

        /// <summary>本实例依赖 IsNavigationTarget 复用，必须保留在 Region 中才可能被匹配到。</summary>
        public override bool KeepAlive => true;

        /// <inheritdoc/>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (!navigationContext.Parameters.ContainsKey("Instance")) return;

            var master = navigationContext.Parameters.GetValue<IModbusRtuMaster>("Instance");
            if (master == null) return;

            UnsubscribeEvents();
            _master = master;
            _instanceId = (_master as ICommunication)?.InstanceId ?? string.Empty;
            BindToMaster();
        }

        /// <inheritdoc/>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            UnsubscribeEvents();
        }

        private void BindToMaster()
        {
            if (_master == null) return;
            PortName = _master.PortName;
            BaudRate = _master.BaudRate;
            RefreshStatus();
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (_master == null) return;
            _master.Opened += OnOpened;
            _master.Closed += OnClosed;
            _master.ErrorOccurred += OnErrorOccurred;
            _master.FrameExchanged += OnFrameExchanged;
        }

        private void UnsubscribeEvents()
        {
            if (_master == null) return;
            _master.Opened -= OnOpened;
            _master.Closed -= OnClosed;
            _master.ErrorOccurred -= OnErrorOccurred;
            _master.FrameExchanged -= OnFrameExchanged;
        }

        private void RefreshStatus()
        {
            if (_master == null) return;
            StatusText = _master.Status.ToString();
        }

        // ── 参数修改：弹窗 → 保存到数据库 → 重新加载全部通讯实例 → 重新绑定刷新后的实例 ──────────

        private void ExecuteShowParamDialog()
        {
            if (string.IsNullOrEmpty(_instanceId)) return;
            var config = _commManager.GetConfig(_instanceId);
            if (config == null) return;

            var paramVm = new ModbusRtuParamViewModel
            {
                PortName = config.ConnectionParameters.GetValueOrDefault("PortName", string.Empty),
                BaudRate = int.TryParse(config.ConnectionParameters.GetValueOrDefault("BaudRate", "9600"), out var baud) ? baud : 9600
            };

            var dialogParams = new DialogParameters { { "Data", paramVm } };
            DialogService.ShowDialog(nameof(ModbusRtuParamDialog), dialogParams, OnParamDialogClosed);
        }

        private async void OnParamDialogClosed(IDialogResult result)
        {
            if (result.Result != ButtonResult.Yes) return;

            var paramItem = result.Parameters.GetValue<ModbusRtuParamViewModel>("CallBackParamItem");
            if (paramItem == null) return;

            var config = _commManager.GetConfig(_instanceId);
            if (config == null) return;

            config.ConnectionParameters["PortName"] = paramItem.PortName;
            config.ConnectionParameters["BaudRate"] = paramItem.BaudRate.ToString();
            await _commManager.SaveConfigAsync(config);

            AppendLog("[参数] 已保存，正在重新加载全部通讯实例...");
            await _commManager.ReloadAllAsync();

            UnsubscribeEvents();
            _master = _commManager.ActiveCommunications.FirstOrDefault(c => c.InstanceId == _instanceId) as IModbusRtuMaster;
            BindToMaster();
            AppendLog("[参数] 重新加载完成");
        }

        /// <summary>
        /// 按当前 Quantity/功能码同步 MultiWriteValues 的元素个数：非写多个模式下清空；
        /// 写多个模式下按需增删，已填的值原样保留（只改 Quantity 不会丢已输入的内容）。
        /// </summary>
        private void SyncMultiWriteValues()
        {
            if (!IsMultiWriteFunction)
            {
                MultiWriteValues.Clear();
                return;
            }

            var target = Math.Max((int)Quantity, 1);
            while (MultiWriteValues.Count < target)
                MultiWriteValues.Add(new ModbusMultiWriteValueItem(MultiWriteValues.Count));
            while (MultiWriteValues.Count > target)
                MultiWriteValues.RemoveAt(MultiWriteValues.Count - 1);
        }

        private static ushort ParseUshort(string text) => ushort.TryParse(text, out var v) ? v : (ushort)0;

        private async Task ExecuteTestAsync()
        {
            if (_master == null) return;
            try
            {
                switch (SelectedFunctionCode)
                {
                    case "读线圈(01)":
                        var coils = await _master.ReadCoilsAsync(UnitId, Address, Quantity);
                        AppendLog($"[读线圈] {string.Join(",", coils.Select(b => b ? 1 : 0))}");
                        break;
                    case "读离散量输入(02)":
                        var inputs = await _master.ReadDiscreteInputsAsync(UnitId, Address, Quantity);
                        AppendLog($"[读离散量输入] {string.Join(",", inputs.Select(b => b ? 1 : 0))}");
                        break;
                    case "读保持寄存器(03)":
                        var holding = await _master.ReadHoldingRegistersAsync(UnitId, Address, Quantity);
                        AppendLog($"[读保持寄存器] {string.Join(",", holding)}");
                        break;
                    case "读输入寄存器(04)":
                        var input = await _master.ReadInputRegistersAsync(UnitId, Address, Quantity);
                        AppendLog($"[读输入寄存器] {string.Join(",", input)}");
                        break;
                    case "写单个线圈(05)":
                        await _master.WriteSingleCoilAsync(UnitId, Address, WriteValue != 0);
                        AppendLog($"[写单个线圈] 地址 {Address} = {WriteValue != 0}");
                        break;
                    case "写单个寄存器(06)":
                        await _master.WriteSingleRegisterAsync(UnitId, Address, WriteValue);
                        AppendLog($"[写单个寄存器] 地址 {Address} = {WriteValue}");
                        break;
                    case "写多个线圈(0F)":
                        var coilValues = MultiWriteValues.Select(v => ParseUshort(v.Value) != 0).ToArray();
                        await _master.WriteMultipleCoilsAsync(UnitId, Address, coilValues);
                        AppendLog($"[写多个线圈] 起始地址 {Address}，{coilValues.Length} 个：{string.Join(",", coilValues.Select(b => b ? 1 : 0))}");
                        break;
                    case "写多个寄存器(10)":
                        var registerValues = MultiWriteValues.Select(v => ParseUshort(v.Value)).ToArray();
                        await _master.WriteMultipleRegistersAsync(UnitId, Address, registerValues);
                        AppendLog($"[写多个寄存器] 起始地址 {Address}，{registerValues.Length} 个：{string.Join(",", registerValues)}");
                        break;
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[错误] {ex.Message}");
            }
        }

        // IModbusRtuMaster 的事件在串口 IO 线程上触发，必须转回 UI 线程才能安全更新绑定的属性
        private void OnOpened(object? sender, EventArgs e) => RunOnUi(() =>
        {
            RefreshStatus();
            AppendLog("[打开] 串口已打开");
        });

        private void OnClosed(object? sender, string reason) => RunOnUi(() =>
        {
            RefreshStatus();
            AppendLog($"[关闭] {reason}");
        });

        private void OnErrorOccurred(object? sender, ErrorOccurredEventArgs e) => RunOnUi(() =>
            AppendLog($"[错误] {e.ErrorMessage}"));

        private void OnFrameExchanged(object? sender, ModbusFrameExchangedEventArgs e) => RunOnUi(() =>
        {
            var reqHex = ToHex(e.RequestFrame);
            var respHex = ToHex(e.ResponseFrame);
            AppendLog(e.Success
                ? $"[报文] 从站{e.UnitId} 请求: {reqHex}  响应: {respHex}"
                : $"[报文] 从站{e.UnitId} 请求: {reqHex}  响应: {respHex}  失败原因: {e.ErrorMessage}");
        });

        private static string ToHex(byte[]? data)
            => data == null || data.Length == 0 ? "(无)" : string.Join(' ', data.Select(b => b.ToString("X2")));

        private void AppendLog(string message)
        {
            _logLines.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
            if (_logLines.Count > 200) _logLines.RemoveAt(_logLines.Count - 1);
            LogText = string.Join(Environment.NewLine, _logLines);
        }

        private static void RunOnUi(Action action) => Application.Current?.Dispatcher.BeginInvoke(action);
    }
}
