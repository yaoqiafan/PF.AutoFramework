using PF.Core.Events;
using PF.Core.Interfaces.Communication;
using PF.Core.Interfaces.Communication.Modbus;
using PF.Infrastructure.Communication.Modbus.Internal;
using PF.Modules.Debug.Dialogs;
using PF.Modules.Debug.Models;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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
                UpdateFramePreview();
            }
        }

        /// <summary>当前是否为读功能码（01~04）</summary>
        public bool IsReadFunction => SelectedFunctionCode is "读线圈(01)" or "读离散量输入(02)" or "读保持寄存器(03)" or "读输入寄存器(04)";
        /// <summary>当前是否为写单个功能码（05/06）——此时 Quantity 不参与，只用 WriteValueText 单值输入框</summary>
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
        /// <summary>从站地址（始终十进制填写）</summary>
        public byte UnitId
        {
            get => _unitId;
            set
            {
                if (!SetProperty(ref _unitId, value)) return;
                UpdateFramePreview();
            }
        }

        private string _addressText = "0";
        /// <summary>起始地址文本，按 UseHexInput 决定的进制解析</summary>
        public string AddressText
        {
            get => _addressText;
            set
            {
                if (!SetProperty(ref _addressText, value)) return;
                UpdateFramePreview();
            }
        }

        private ushort _quantity = 1;
        /// <summary>读取数量，或写多个操作时的元素个数（写单个操作时不使用，见 QuantityLabel；始终十进制填写）</summary>
        public ushort Quantity
        {
            get => _quantity;
            set
            {
                if (!SetProperty(ref _quantity, value)) return;
                SyncMultiWriteValues();
                UpdateFramePreview();
            }
        }

        private string _writeValueText = "0";
        /// <summary>写单个线圈/寄存器时使用的值文本（写线圈时非 0 视为 ON），按 UseHexInput 决定的进制解析</summary>
        public string WriteValueText
        {
            get => _writeValueText;
            set
            {
                if (!SetProperty(ref _writeValueText, value)) return;
                UpdateFramePreview();
            }
        }

        private bool _useHexInput = true;
        /// <summary>
        /// 地址/写入值是否按十六进制填写（默认 true）。切换时已填的值在两种进制的字符串表示之间
        /// 自动换算（解析失败的文本原样保留）；从站地址与数量始终按十进制填写，不受此开关影响。
        /// </summary>
        public bool UseHexInput
        {
            get => _useHexInput;
            set
            {
                if (_useHexInput == value) return;
                var fromHex = _useHexInput;
                _useHexInput = value;
                RaisePropertyChanged();
                ConvertInputBase(fromHex, value);
                UpdateFramePreview();
            }
        }

        /// <summary>
        /// 写多个线圈/寄存器时，每个位置各自的值——随 Quantity/功能码变化自动增删（见 SyncMultiWriteValues），
        /// 修正此前"全部写同一个 WriteValue"、无法逐位置自定义的问题。
        /// </summary>
        public ObservableCollection<ModbusMultiWriteValueItem> MultiWriteValues { get; } = new();

        private string _framePreview = "——";
        /// <summary>按当前参数实时生成的完整请求帧预览（与实际发送帧一致），参数无效时显示原因</summary>
        public string FramePreview { get => _framePreview; private set => SetProperty(ref _framePreview, value); }

        private string _rawPduText = string.Empty;
        /// <summary>原始透传的 PDU 十六进制字符串（功能码+数据，可含空格/短横线分隔，从站地址/CRC 自动补全）</summary>
        public string RawPduText
        {
            get => _rawPduText;
            set
            {
                if (!SetProperty(ref _rawPduText, value)) return;
                UpdateFramePreview();
            }
        }

        private string _rawFramePreview = "——";
        /// <summary>原始透传的完整帧预览</summary>
        public string RawFramePreview { get => _rawFramePreview; private set => SetProperty(ref _rawFramePreview, value); }

        /// <summary>打开命令</summary>
        public DelegateCommand OpenCommand { get; }
        /// <summary>关闭命令</summary>
        public DelegateCommand CloseCommand { get; }
        /// <summary>执行测试读写命令</summary>
        public DelegateCommand ExecuteTestCommand { get; }
        /// <summary>原始报文直发命令</summary>
        public DelegateCommand SendRawCommand { get; }
        /// <summary>打开本实例参数修改对话框命令</summary>
        public DelegateCommand ShowParamDialogCommand { get; }

        /// <summary>初始化 Modbus RTU 调试 ViewModel</summary>
        public ModbusRtuDebugViewModel(ICommunicationManagerService commManager)
        {
            _commManager = commManager;
            OpenCommand = new DelegateCommand(async () => { if (_master != null) await _master.OpenAsync(); });
            CloseCommand = new DelegateCommand(async () => { if (_master != null) await _master.CloseAsync(); });
            ExecuteTestCommand = new DelegateCommand(async () => await ExecuteTestAsync());
            SendRawCommand = new DelegateCommand(async () => await ExecuteSendRawAsync());
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
            UpdateFramePreview();
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
                BaudRate = int.TryParse(config.ConnectionParameters.GetValueOrDefault("BaudRate", "9600"), out var baud) ? baud : 9600,
                Parity = config.ConnectionParameters.GetValueOrDefault("Parity", "None"),
                DataBits = int.TryParse(config.ConnectionParameters.GetValueOrDefault("DataBits", "8"), out var bits) ? bits : 8,
                StopBits = config.ConnectionParameters.GetValueOrDefault("StopBits", "One"),
                TimeoutMs = int.TryParse(config.ConnectionParameters.GetValueOrDefault("TimeoutMs", "1000"), out var to) ? to : 1000,
                AutoReconnect = bool.TryParse(config.ConnectionParameters.GetValueOrDefault("AutoReconnect", "true"), out var ar) ? ar : true,
                ReconnectIntervalMs = int.TryParse(config.ConnectionParameters.GetValueOrDefault("ReconnectIntervalMs", "5000"), out var ri) ? ri : 5000
            };

            var dialogParams = new DialogParameters { { "Data", paramVm } };
            DialogService.ShowDialog(nameof(ModbusRtuParamDialog), dialogParams, OnParamDialogClosed);
        }

        // async void 事件回调：异常必须就地兜住，否则会直接崩掉进程
        private async void OnParamDialogClosed(IDialogResult result)
        {
            try
            {
                if (result.Result != ButtonResult.Yes) return;

                var paramItem = result.Parameters.GetValue<ModbusRtuParamViewModel>("CallBackParamItem");
                if (paramItem == null) return;

                var config = _commManager.GetConfig(_instanceId);
                if (config == null) return;

                config.ConnectionParameters["PortName"] = paramItem.PortName;
                config.ConnectionParameters["BaudRate"] = paramItem.BaudRate.ToString();
                config.ConnectionParameters["Parity"] = paramItem.Parity;
                config.ConnectionParameters["DataBits"] = paramItem.DataBits.ToString();
                config.ConnectionParameters["StopBits"] = paramItem.StopBits;
                config.ConnectionParameters["TimeoutMs"] = paramItem.TimeoutMs.ToString();
                config.ConnectionParameters["AutoReconnect"] = paramItem.AutoReconnect.ToString();
                config.ConnectionParameters["ReconnectIntervalMs"] = paramItem.ReconnectIntervalMs.ToString();
                await _commManager.SaveConfigAsync(config);

                AppendLog("[参数] 已保存，正在重新加载全部通讯实例...");
                await _commManager.ReloadAllAsync();

                UnsubscribeEvents();
                _master = _commManager.ActiveCommunications.FirstOrDefault(c => c.InstanceId == _instanceId) as IModbusRtuMaster;
                BindToMaster();
                AppendLog("[参数] 重新加载完成");
            }
            catch (Exception ex)
            {
                AppendLog($"[参数] 保存/重载失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 按当前 Quantity/功能码同步 MultiWriteValues 的元素个数：非写多个模式下清空；
        /// 写多个模式下按需增删，已填的值原样保留（只改 Quantity 不会丢已输入的内容）。
        /// 每个元素挂 PropertyChanged 以驱动报文预览实时刷新。
        /// </summary>
        private void SyncMultiWriteValues()
        {
            if (!IsMultiWriteFunction)
            {
                foreach (var item in MultiWriteValues) item.PropertyChanged -= OnMultiWriteItemChanged;
                MultiWriteValues.Clear();
                return;
            }

            // 上限取功能码允许的最大数量（线圈 1968 / 寄存器 123），同时避免误输大数在 UI 线程生成上万行卡死界面
            var max = SelectedFunctionCode == "写多个线圈(0F)" ? 1968 : 123;
            var target = Math.Clamp((int)Quantity, 1, max);
            while (MultiWriteValues.Count < target)
            {
                var item = new ModbusMultiWriteValueItem(MultiWriteValues.Count);
                item.PropertyChanged += OnMultiWriteItemChanged;
                MultiWriteValues.Add(item);
            }
            while (MultiWriteValues.Count > target)
            {
                MultiWriteValues[^1].PropertyChanged -= OnMultiWriteItemChanged;
                MultiWriteValues.RemoveAt(MultiWriteValues.Count - 1);
            }
        }

        private void OnMultiWriteItemChanged(object? sender, PropertyChangedEventArgs e) => UpdateFramePreview();

        // ── 进制解析与报文预览 ─────────────────────────────────────────────────

        private static bool TryParseUShort(string? text, bool hex, out ushort value)
        {
            value = 0;
            text = text?.Trim();
            if (string.IsNullOrEmpty(text)) return false;
            if (hex)
            {
                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
                return ushort.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            }
            return ushort.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static string FormatUShort(ushort value, bool hex)
            => hex ? value.ToString("X") : value.ToString(CultureInfo.InvariantCulture);

        private static string ConvertTextBase(string text, bool fromHex, bool toHex)
            => TryParseUShort(text, fromHex, out var v) ? FormatUShort(v, toHex) : text;

        private void ConvertInputBase(bool fromHex, bool toHex)
        {
            AddressText = ConvertTextBase(AddressText, fromHex, toHex);
            WriteValueText = ConvertTextBase(WriteValueText, fromHex, toHex);
            foreach (var item in MultiWriteValues)
                item.Value = ConvertTextBase(item.Value, fromHex, toHex);
        }

        /// <summary>解析十六进制字符串为字节数组，允许空格/短横线/逗号分隔与 0x 前缀</summary>
        private static bool TryParseHexBytes(string? text, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(text)) return false;
            var cleaned = text.Replace("0x", "", StringComparison.OrdinalIgnoreCase)
                .Replace(" ", "").Replace("-", "").Replace(",", "");
            if (cleaned.Length == 0 || cleaned.Length % 2 != 0) return false;
            var result = new byte[cleaned.Length / 2];
            for (var i = 0; i < result.Length; i++)
            {
                if (!byte.TryParse(cleaned.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result[i]))
                    return false;
            }
            bytes = result;
            return true;
        }

        /// <summary>按当前参数构建 PDU（预览与实际发送共用 ModbusPduCodec，两者不会跑偏）</summary>
        private bool TryBuildCurrentPdu(out byte[] pdu, out string error)
        {
            pdu = Array.Empty<byte>();
            error = string.Empty;
            if (!TryParseUShort(AddressText, UseHexInput, out var address)) { error = "起始地址格式错误"; return false; }

            try
            {
                switch (SelectedFunctionCode)
                {
                    case "读线圈(01)": pdu = ModbusPduCodec.BuildReadRequest(0x01, address, Quantity); return true;
                    case "读离散量输入(02)": pdu = ModbusPduCodec.BuildReadRequest(0x02, address, Quantity); return true;
                    case "读保持寄存器(03)": pdu = ModbusPduCodec.BuildReadRequest(0x03, address, Quantity); return true;
                    case "读输入寄存器(04)": pdu = ModbusPduCodec.BuildReadRequest(0x04, address, Quantity); return true;
                    case "写单个线圈(05)":
                        if (!TryParseUShort(WriteValueText, UseHexInput, out var coil)) { error = "写入值格式错误"; return false; }
                        pdu = ModbusPduCodec.BuildWriteSingleCoil(address, coil != 0); return true;
                    case "写单个寄存器(06)":
                        if (!TryParseUShort(WriteValueText, UseHexInput, out var reg)) { error = "写入值格式错误"; return false; }
                        pdu = ModbusPduCodec.BuildWriteSingleRegister(address, reg); return true;
                    case "写多个线圈(0F)":
                        if (!TryParseMultiValues(out var coils, out error)) return false;
                        pdu = ModbusPduCodec.BuildWriteMultipleCoils(address, coils.Select(v => v != 0).ToArray()); return true;
                    case "写多个寄存器(10)":
                        if (!TryParseMultiValues(out var regs, out error)) return false;
                        pdu = ModbusPduCodec.BuildWriteMultipleRegisters(address, regs); return true;
                    default: error = "未选择功能码"; return false;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool TryParseMultiValues(out ushort[] values, out string error)
        {
            values = new ushort[MultiWriteValues.Count];
            error = string.Empty;
            if (values.Length == 0) { error = "写入个数为 0"; return false; }
            for (var i = 0; i < MultiWriteValues.Count; i++)
            {
                if (!TryParseUShort(MultiWriteValues[i].Value, UseHexInput, out values[i]))
                {
                    error = $"第 [{i}] 个写入值格式错误";
                    return false;
                }
            }
            return true;
        }

        private void UpdateFramePreview()
        {
            if (_master == null)
            {
                FramePreview = "——";
                RawFramePreview = "——";
                return;
            }

            FramePreview = TryBuildCurrentPdu(out var pdu, out var error)
                ? ToHex(_master.BuildFrame(UnitId, pdu))
                : $"参数无效：{error}";

            if (string.IsNullOrWhiteSpace(RawPduText))
                RawFramePreview = "——";
            else if (!TryParseHexBytes(RawPduText, out var rawPdu))
                RawFramePreview = "PDU 十六进制字符串无效（应为偶数个十六进制字符，可含空格分隔）";
            else if (rawPdu.Length > 253)
                RawFramePreview = "PDU 长度须为 1~253 字节";
            else
                RawFramePreview = ToHex(_master.BuildFrame(UnitId, rawPdu));
        }

        // ── 测试执行 ─────────────────────────────────────────────────────────

        private async Task ExecuteTestAsync()
        {
            if (_master == null) return;
            if (!TryParseUShort(AddressText, UseHexInput, out var address)) { AppendLog("[错误] 起始地址格式错误"); return; }
            try
            {
                switch (SelectedFunctionCode)
                {
                    case "读线圈(01)":
                        var coils = await _master.ReadCoilsAsync(UnitId, address, Quantity);
                        AppendLog($"[读线圈] {string.Join(",", coils.Select(b => b ? 1 : 0))}");
                        break;
                    case "读离散量输入(02)":
                        var inputs = await _master.ReadDiscreteInputsAsync(UnitId, address, Quantity);
                        AppendLog($"[读离散量输入] {string.Join(",", inputs.Select(b => b ? 1 : 0))}");
                        break;
                    case "读保持寄存器(03)":
                        var holding = await _master.ReadHoldingRegistersAsync(UnitId, address, Quantity);
                        AppendLog($"[读保持寄存器] {string.Join(",", holding)}");
                        break;
                    case "读输入寄存器(04)":
                        var input = await _master.ReadInputRegistersAsync(UnitId, address, Quantity);
                        AppendLog($"[读输入寄存器] {string.Join(",", input)}");
                        break;
                    case "写单个线圈(05)":
                        if (!TryParseUShort(WriteValueText, UseHexInput, out var coilValue)) { AppendLog("[错误] 写入值格式错误"); return; }
                        await _master.WriteSingleCoilAsync(UnitId, address, coilValue != 0);
                        AppendLog($"[写单个线圈] 地址 {address} = {coilValue != 0}");
                        break;
                    case "写单个寄存器(06)":
                        if (!TryParseUShort(WriteValueText, UseHexInput, out var regValue)) { AppendLog("[错误] 写入值格式错误"); return; }
                        await _master.WriteSingleRegisterAsync(UnitId, address, regValue);
                        AppendLog($"[写单个寄存器] 地址 {address} = {regValue}");
                        break;
                    case "写多个线圈(0F)":
                        if (!TryParseMultiValues(out var coilValues, out var coilErr)) { AppendLog($"[错误] {coilErr}"); return; }
                        var coilBools = coilValues.Select(v => v != 0).ToArray();
                        await _master.WriteMultipleCoilsAsync(UnitId, address, coilBools);
                        AppendLog($"[写多个线圈] 起始地址 {address}，{coilBools.Length} 个：{string.Join(",", coilBools.Select(b => b ? 1 : 0))}");
                        break;
                    case "写多个寄存器(10)":
                        if (!TryParseMultiValues(out var registerValues, out var regErr)) { AppendLog($"[错误] {regErr}"); return; }
                        await _master.WriteMultipleRegistersAsync(UnitId, address, registerValues);
                        AppendLog($"[写多个寄存器] 起始地址 {address}，{registerValues.Length} 个：{string.Join(",", registerValues)}");
                        break;
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[错误] {ex.Message}");
            }
        }

        /// <summary>原始报文直发：PDU 透传，不校验功能码回显，从站异常响应原样返回并在日志中标注</summary>
        private async Task ExecuteSendRawAsync()
        {
            if (_master == null) return;
            if (!TryParseHexBytes(RawPduText, out var pdu)) { AppendLog("[原始报文] PDU 十六进制字符串无效（应为偶数个十六进制字符，可含空格分隔）"); return; }
            try
            {
                var response = await _master.SendRawAsync(UnitId, pdu);
                var isException = response.Length > 0 && (response[0] & 0x80) != 0;
                AppendLog(isException
                    ? $"[原始报文] 响应 PDU（从站异常响应）: {ToHex(response)}"
                    : $"[原始报文] 响应 PDU: {ToHex(response)}");
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
