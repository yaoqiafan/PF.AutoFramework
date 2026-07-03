using PF.Core.Enums.FileTransfer;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// FileTransfer 通道参数对话框 ViewModel。
    /// 通道级参数（角色/接收目录/内存阈值）与 Lane 级参数（每条物理网口的连接端点）分层管理，
    /// 支持任意数量的 Lane 新增/删除，保存时校验通过才关闭对话框，避免误删其余 Lane 配置。
    /// </summary>
    public class FileTransferParamDialogViewModel : PFDialogViewModelBase
    {
        /// <summary>初始化 FileTransfer 通道参数对话框 ViewModel</summary>
        public FileTransferParamDialogViewModel()
        {
            Title = "FileTransfer通道参数修改";

            ConfirmCommand = new DelegateCommand(OnConfirmed);

            CancelCommand = new DelegateCommand(() =>
            {
                LogService.Info($"[FileTransfer参数] 用户[{CurrentUserName}] 取消修改", "操作日志");
                RequestClose.Invoke(new DialogResult { Result = ButtonResult.Cancel });
            });

            AddLaneCommand = new DelegateCommand(ExecuteAddLane);
            RemoveLaneCommand = new DelegateCommand(ExecuteRemoveLane, CanRemoveLane)
                .ObservesProperty(() => SelectedLane);
            Lanes.CollectionChanged += (_, _) => RemoveLaneCommand.RaiseCanExecuteChanged();
        }

        private FileTransferChannelParamViewModel _channelParams = new();
        /// <summary>正在编辑的通道级参数（角色/接收目录/内存阈值，整个通道只有一份）</summary>
        public FileTransferChannelParamViewModel ChannelParams
        {
            get => _channelParams;
            set => SetProperty(ref _channelParams, value);
        }

        /// <summary>正在编辑的 Lane 列表（每条对应一个物理网口）</summary>
        public ObservableCollection<FileTransferLaneParamViewModel> Lanes { get; } = new();

        private FileTransferLaneParamViewModel? _selectedLane;
        /// <summary>当前选中的 Lane，PropertyGrid 展示其字段</summary>
        public FileTransferLaneParamViewModel? SelectedLane
        {
            get => _selectedLane;
            set => SetProperty(ref _selectedLane, value);
        }

        /// <summary>新增一条 Lane 命令</summary>
        public DelegateCommand AddLaneCommand { get; }
        /// <summary>删除选中 Lane 命令（至少保留一条）</summary>
        public DelegateCommand RemoveLaneCommand { get; }

        /// <inheritdoc/>
        public override void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);

            if (parameters.ContainsKey("ChannelData"))
                ChannelParams = parameters.GetValue<FileTransferChannelParamViewModel>("ChannelData");

            Lanes.Clear();
            if (parameters.ContainsKey("LanesData"))
            {
                foreach (var lane in parameters.GetValue<List<FileTransferLaneParamViewModel>>("LanesData"))
                    Lanes.Add(lane);
            }

            SelectedLane = Lanes.FirstOrDefault();
        }

        private void ExecuteAddLane()
        {
            var nextLaneId = Lanes.Count == 0 ? 0 : Lanes.Max(l => l.LaneId) + 1;
            var newLane = new FileTransferLaneParamViewModel { LaneId = nextLaneId, LocalIp = "0.0.0.0" };
            Lanes.Add(newLane);
            SelectedLane = newLane;
        }

        private bool CanRemoveLane() => SelectedLane != null && Lanes.Count > 1;

        private void ExecuteRemoveLane()
        {
            if (SelectedLane == null) return;
            var index = Lanes.IndexOf(SelectedLane);
            Lanes.Remove(SelectedLane);
            SelectedLane = Lanes.Count == 0 ? null : Lanes[Math.Min(index, Lanes.Count - 1)];
        }

        private void OnConfirmed()
        {
            if (!TryValidate(out var role, out var errorMessage))
            {
                MessageService.ShowMessage(errorMessage, "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LogService.Info(
                $"[FileTransfer参数] 用户[{CurrentUserName}] 确认修改 | Role:{ChannelParams.RoleText} " +
                $"LaneCount:{Lanes.Count} ReceiveDir:{ChannelParams.ReceiveDirectory} ThresholdMb:{ChannelParams.InMemoryReceiveThresholdMb}",
                "操作日志");

            var paras = new DialogParameters
            {
                { "CallBackChannelParam", ChannelParams },
                { "CallBackLanes", Lanes.ToList() }
            };

            RequestClose.Invoke(new DialogResult
            {
                Result = ButtonResult.Yes,
                Parameters = paras
            });
        }

        private bool TryValidate(out FileTransferRole role, out string errorMessage)
        {
            role = default;
            errorMessage = string.Empty;

            if (!Enum.TryParse(ChannelParams.RoleText, ignoreCase: true, out role))
            {
                errorMessage = $"角色填写不合法（需为 Server 或 Client）：{ChannelParams.RoleText}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(ChannelParams.ReceiveDirectory))
            {
                errorMessage = "接收落盘目录不能为空";
                return false;
            }

            if (ChannelParams.InMemoryReceiveThresholdMb <= 0)
            {
                errorMessage = $"内存重组阈值需为正整数（MB）：{ChannelParams.InMemoryReceiveThresholdMb}";
                return false;
            }

            if (Lanes.Count == 0)
            {
                errorMessage = "至少需要配置一条 Lane";
                return false;
            }

            var duplicateGroup = Lanes.GroupBy(l => l.LaneId).FirstOrDefault(g => g.Count() > 1);
            if (duplicateGroup != null)
            {
                errorMessage = $"LaneId 重复：{duplicateGroup.Key}";
                return false;
            }

            foreach (var lane in Lanes)
            {
                if (lane.Port <= 0 || lane.Port > 65535)
                {
                    errorMessage = $"Lane {lane.LaneId} 端口不合法（需为 1~65535）：{lane.Port}";
                    return false;
                }

                if (role == FileTransferRole.Client && string.IsNullOrWhiteSpace(lane.RemoteIp))
                {
                    errorMessage = $"Lane {lane.LaneId} 是 Client 角色，对端IP不能为空";
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>FileTransfer 通道级可编辑参数（角色/接收目录/内存阈值），整个通道只有一份</summary>
    public class FileTransferChannelParamViewModel : BindableBase
    {
        private string _roleText = "Server";
        /// <summary>角色，填 Server 或 Client</summary>
        [Category("FileTransfer通道参数")]
        [DisplayName("角色(Server/Client)")]
        [Browsable(true)]
        public string RoleText { get => _roleText; set => SetProperty(ref _roleText, value); }

        private string _receiveDirectory = string.Empty;
        /// <summary>接收落盘根目录：超过内存重组阈值的传输落盘于此目录下以通道名命名的子目录（建议指向数据盘，如 D:\）</summary>
        [Category("FileTransfer通道参数")]
        [DisplayName("接收落盘目录")]
        [Browsable(true)]
        public string ReceiveDirectory { get => _receiveDirectory; set => SetProperty(ref _receiveDirectory, value); }

        private int _inMemoryReceiveThresholdMb = 16;
        /// <summary>接收内存重组阈值（MB）：不超过该值的传输在内存重组经 Data 交付，超过则落盘经 FilePath 交付</summary>
        [Category("FileTransfer通道参数")]
        [DisplayName("内存重组阈值(MB)")]
        [Browsable(true)]
        public int InMemoryReceiveThresholdMb { get => _inMemoryReceiveThresholdMb; set => SetProperty(ref _inMemoryReceiveThresholdMb, value); }
    }

    /// <summary>FileTransfer 单条 Lane 的可编辑参数，一条对应一个物理网口</summary>
    public class FileTransferLaneParamViewModel : BindableBase
    {
        private int _laneId;
        /// <summary>Lane编号，两端必须一致，用于配对而非按列表顺序隐式匹配</summary>
        [Category("Lane参数")]
        [DisplayName("LaneId")]
        [Browsable(true)]
        public int LaneId { get => _laneId; set => SetProperty(ref _laneId, value); }

        private string _localIp = "0.0.0.0";
        /// <summary>本地绑定IP（Server=监听网口IP；Client=出口网口IP）</summary>
        [Category("Lane参数")]
        [DisplayName("本地IP")]
        [Browsable(true)]
        public string LocalIp { get => _localIp; set => SetProperty(ref _localIp, value); }

        private int _port;
        /// <summary>端口</summary>
        [Category("Lane参数")]
        [DisplayName("端口")]
        [Browsable(true)]
        public int Port { get => _port; set => SetProperty(ref _port, value); }

        private string _remoteIp = string.Empty;
        /// <summary>对端IP，仅 Client 角色需要</summary>
        [Category("Lane参数")]
        [DisplayName("对端IP(仅Client需要)")]
        [Browsable(true)]
        public string RemoteIp { get => _remoteIp; set => SetProperty(ref _remoteIp, value); }
    }
}
