using PF.Core.Events;
using PF.Core.Interfaces.Communication;
using PF.Core.Interfaces.Communication.Serial;
using PF.Modules.Debug.Dialogs;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 串口调试 ViewModel，接收 NavigationParameter("Instance") 传入的 <see cref="ISerialCommunication"/> 实例。
    /// 结构照抄 <see cref="TcpClientDebugViewModel"/>，差别在于串口是点对点物理连接，
    /// 语义是"打开/关闭"而不是"连接/断开"。
    ///
    /// <para><b>为什么要有裸串口调试页</b>：串口设备（如海康串口光源）出问题时，故障可能在链路本身
    /// （串口没开、线接错、波特率/校验位不匹配），也可能在设备协议层。设备调试页只能告诉你"指令失败"，
    /// 分不清是哪一层。这里直接对着链路收发原始字节，先把物理链路择干净。</para>
    ///
    /// <para><b>为什么收发都要有十六进制模式</b>：串口设备的协议既有 ASCII 文本型的
    /// （如海康光源的 "SA0500#"），也有纯二进制型的。二进制报文按文本显示会变成一堆乱码和不可见字符，
    /// 根本没法核对；反过来文本协议按十六进制看又很费劲。所以收、发各给一个独立开关。</para>
    /// </summary>
    public class SerialPortDebugViewModel : RegionViewModelBase
    {
        /// <summary>日志最大保留条数，与其它通讯调试页一致。</summary>
        private const int MaxLogEntries = 200;

        private readonly ICommunicationManagerService _commManager;
        private ISerialCommunication? _serial;
        private string _instanceId = string.Empty;

        private string _portName = "未选中";
        /// <summary>串口名称</summary>
        public string PortName { get => _portName; set => SetProperty(ref _portName, value); }

        private string _statusText = "——";
        /// <summary>打开状态文本</summary>
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        private string _frameFormat = "——";
        /// <summary>串口帧格式描述（波特率 数据位-校验位-停止位）</summary>
        public string FrameFormat { get => _frameFormat; set => SetProperty(ref _frameFormat, value); }

        /// <summary>事件日志（最新在前）</summary>
        public ObservableCollection<string> LogEntries { get; } = new();

        private string _sendText = string.Empty;
        /// <summary>待发送的测试数据</summary>
        public string SendText { get => _sendText; set => SetProperty(ref _sendText, value); }

        private bool _sendAsHex;
        /// <summary>发送框内容是否按十六进制解析（如 "53 41 30 35 30 30 23"）</summary>
        public bool SendAsHex { get => _sendAsHex; set => SetProperty(ref _sendAsHex, value); }

        private bool _displayAsHex;
        /// <summary>收到的数据是否按十六进制显示</summary>
        public bool DisplayAsHex { get => _displayAsHex; set => SetProperty(ref _displayAsHex, value); }

        /// <summary>打开串口命令</summary>
        public DelegateCommand OpenCommand { get; }
        /// <summary>关闭串口命令</summary>
        public DelegateCommand CloseCommand { get; }
        /// <summary>发送测试数据命令</summary>
        public DelegateCommand SendCommand { get; }
        /// <summary>清空日志命令</summary>
        public DelegateCommand ClearLogCommand { get; }
        /// <summary>打开本实例参数修改对话框命令</summary>
        public DelegateCommand ShowParamDialogCommand { get; }

        /// <summary>初始化串口调试 ViewModel</summary>
        public SerialPortDebugViewModel(ICommunicationManagerService commManager)
        {
            _commManager = commManager;
            OpenCommand = new DelegateCommand(async () => await ExecuteOpenAsync());
            CloseCommand = new DelegateCommand(async () => await ExecuteCloseAsync());
            SendCommand = new DelegateCommand(async () => await ExecuteSendAsync());
            ClearLogCommand = new DelegateCommand(() => LogEntries.Clear());
            ShowParamDialogCommand = new DelegateCommand(ExecuteShowParamDialog);
        }

        /// <summary>
        /// 只有导航目标和当前已绑定的是同一个 InstanceId 才允许复用本实例，
        /// 这样同一个实例的调试页反复进出时，LogEntries 等状态不会被清空重建。
        /// </summary>
        public override bool IsNavigationTarget(NavigationContext navigationContext)
        {
            if (!navigationContext.Parameters.ContainsKey("Instance")) return false;
            var target = navigationContext.Parameters.GetValue<ISerialCommunication>("Instance");
            return target != null && (target as ICommunication)?.InstanceId == _instanceId;
        }

        /// <summary>本实例依赖 IsNavigationTarget 复用，必须保留在 Region 中才可能被匹配到。</summary>
        public override bool KeepAlive => true;

        /// <inheritdoc/>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (!navigationContext.Parameters.ContainsKey("Instance")) return;

            var serial = navigationContext.Parameters.GetValue<ISerialCommunication>("Instance");
            if (serial == null) return;

            // 无论首次绑定还是复用旧实例重新导航进来，都先退订旧引用的事件，理由同 TcpClientDebugViewModel
            UnsubscribeEvents();
            _serial = serial;
            _instanceId = (_serial as ICommunication)?.InstanceId ?? string.Empty;
            BindToSerial();
        }

        /// <inheritdoc/>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            UnsubscribeEvents();
        }

        private void BindToSerial()
        {
            if (_serial == null) return;
            PortName = _serial.PortName;
            RefreshStatus();
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (_serial == null) return;
            _serial.Opened += OnOpened;
            _serial.Closed += OnClosed;
            _serial.DataReceived += OnDataReceived;
            _serial.ErrorOccurred += OnErrorOccurred;
        }

        private void UnsubscribeEvents()
        {
            if (_serial == null) return;
            _serial.Opened -= OnOpened;
            _serial.Closed -= OnClosed;
            _serial.DataReceived -= OnDataReceived;
            _serial.ErrorOccurred -= OnErrorOccurred;
        }

        private void RefreshStatus()
        {
            if (_serial == null) return;
            StatusText = _serial.Status.ToString();

            // 校验位/数据位/停止位没有走 ISerialCommunication 暴露出来（接口只有 PortName/BaudRate），
            // 这里从持久化配置里取，与"参数设置"对话框读的是同一份数据，不会两处对不上。
            var config = string.IsNullOrEmpty(_instanceId) ? null : _commManager.GetConfig(_instanceId);
            if (config == null)
            {
                FrameFormat = $"{_serial.BaudRate}bps";
                return;
            }

            string parity = config.ConnectionParameters.GetValueOrDefault("Parity", "None");
            string dataBits = config.ConnectionParameters.GetValueOrDefault("DataBits", "8");
            string stopBits = config.ConnectionParameters.GetValueOrDefault("StopBits", "One");
            FrameFormat = $"{_serial.BaudRate}bps  数据位 {dataBits}  校验 {parity}  停止位 {stopBits}";
        }

        private async Task ExecuteOpenAsync()
        {
            if (_serial == null) return;
            var ok = await _serial.OpenAsync();
            RefreshStatus();
            AppendLog(ok ? $"[打开] 串口 {_serial.PortName} 打开成功" : $"[打开] 串口 {_serial.PortName} 打开失败");
        }

        private async Task ExecuteCloseAsync()
        {
            if (_serial == null) return;
            await _serial.CloseAsync();
            RefreshStatus();
        }

        private async Task ExecuteSendAsync()
        {
            if (_serial == null || string.IsNullOrEmpty(SendText)) return;

            byte[] data;
            if (SendAsHex)
            {
                if (!TryParseHex(SendText, out data, out var error))
                {
                    // 解析失败只提示、不发送：把半截报文丢给设备比不发更糟
                    AppendLog($"[发送] 十六进制解析失败：{error}");
                    return;
                }
            }
            else
            {
                data = _serial.Encoding.GetBytes(SendText);
            }

            var ok = await _serial.SendAsync(data);
            AppendLog(ok
                ? $"[发送] {data.Length} 字节: {Describe(data)}"
                : $"[发送] 失败: {Describe(data)}");
        }

        // ── 参数修改：弹窗 → 保存到数据库 → 重新加载全部通讯实例 → 重新绑定刷新后的实例 ──────────

        private void ExecuteShowParamDialog()
        {
            if (string.IsNullOrEmpty(_instanceId)) return;
            var config = _commManager.GetConfig(_instanceId);
            if (config == null) return;

            var paramVm = new SerialPortParamViewModel
            {
                PortName = config.ConnectionParameters.GetValueOrDefault("PortName", string.Empty),
                BaudRate = int.TryParse(config.ConnectionParameters.GetValueOrDefault("BaudRate", "9600"), out var br) ? br : 9600,
                Parity = config.ConnectionParameters.GetValueOrDefault("Parity", "None"),
                DataBits = int.TryParse(config.ConnectionParameters.GetValueOrDefault("DataBits", "8"), out var db) ? db : 8,
                StopBits = config.ConnectionParameters.GetValueOrDefault("StopBits", "One")
            };

            var dialogParams = new DialogParameters { { "Data", paramVm } };
            DialogService.ShowDialog(nameof(SerialPortParamDialog), dialogParams, OnParamDialogClosed);
        }

        private async void OnParamDialogClosed(IDialogResult result)
        {
            if (result.Result != ButtonResult.Yes) return;

            var paramItem = result.Parameters.GetValue<SerialPortParamViewModel>("CallBackParamItem");
            if (paramItem == null) return;

            var config = _commManager.GetConfig(_instanceId);
            if (config == null) return;

            config.ConnectionParameters["PortName"] = paramItem.PortName;
            config.ConnectionParameters["BaudRate"] = paramItem.BaudRate.ToString();
            config.ConnectionParameters["Parity"] = paramItem.Parity;
            config.ConnectionParameters["DataBits"] = paramItem.DataBits.ToString();
            config.ConnectionParameters["StopBits"] = paramItem.StopBits;
            await _commManager.SaveConfigAsync(config);

            AppendLog("[参数] 已保存，正在重新加载全部通讯实例...");
            await _commManager.ReloadAllAsync();

            UnsubscribeEvents();
            _serial = _commManager.ActiveCommunications.FirstOrDefault(c => c.InstanceId == _instanceId) as ISerialCommunication;
            BindToSerial();
            AppendLog("[参数] 重新加载完成");
        }

        // 串口事件在 SerialPort 的接收线程上触发，必须转回 UI 线程才能安全更新绑定的属性
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

        private void OnDataReceived(object? sender, DataReceivedEventArgs e) => RunOnUi(() =>
            AppendLog($"[收到] {e.Data?.Length ?? 0} 字节: {Describe(e.Data)}"));

        private void OnErrorOccurred(object? sender, ErrorOccurredEventArgs e) => RunOnUi(() =>
            AppendLog($"[错误] {e.ErrorMessage}"));

        // ── 收发数据的文本化 ──────────────────────────────────────────────────

        /// <summary>按当前显示模式把字节数组转成可读文本。</summary>
        private string Describe(byte[]? data)
        {
            if (data == null || data.Length == 0) return string.Empty;
            if (DisplayAsHex) return ToHex(data);

            // 文本模式下把不可打印字符替换成 '.'，否则控制字符会把日志行搅乱
            var encoding = _serial?.Encoding ?? Encoding.ASCII;
            var text = encoding.GetString(data);
            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                sb.Append(char.IsControl(ch) ? '.' : ch);
            }
            return sb.ToString();
        }

        /// <summary>转成空格分隔的大写十六进制，如 "53 41 30 35"。</summary>
        private static string ToHex(byte[] data) => BitConverter.ToString(data).Replace('-', ' ');

        /// <summary>
        /// 解析十六进制输入。容忍空格、逗号、连字符换行等分隔符与 "0x" 前缀，
        /// 十六进制位数必须为偶数（半个字节没法发）。
        /// </summary>
        private static bool TryParseHex(string text, out byte[] data, out string error)
        {
            data = Array.Empty<byte>();
            error = string.Empty;

            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch is ' ' or ',' or '-' or '\r' or '\n' or '\t') continue;

                // 跳过 0x / 0X 前缀
                if ((ch is '0') && i + 1 < text.Length && (text[i + 1] is 'x' or 'X'))
                {
                    i++;
                    continue;
                }

                if (!Uri.IsHexDigit(ch))
                {
                    error = $"含非十六进制字符 '{ch}'";
                    return false;
                }
                sb.Append(ch);
            }

            if (sb.Length == 0)
            {
                error = "没有有效的十六进制字符";
                return false;
            }
            if (sb.Length % 2 != 0)
            {
                error = $"十六进制位数为奇数（{sb.Length} 位），无法凑成整字节";
                return false;
            }

            var bytes = new byte[sb.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(sb.ToString(i * 2, 2), 16);
            }
            data = bytes;
            return true;
        }

        private void AppendLog(string message)
        {
            LogEntries.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} {message}");
            while (LogEntries.Count > MaxLogEntries) LogEntries.RemoveAt(LogEntries.Count - 1);
        }

        private static void RunOnUi(Action action) => Application.Current?.Dispatcher.BeginInvoke(action);
    }
}
