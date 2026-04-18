using PF.Core.Entities.SecsGem.Message;
using PF.Modules.SecsGem.ViewModels.Models;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;

namespace PF.Modules.SecsGem.ViewModels.SubViewModels
{
    /// <summary>
    /// 负责底部实时通信日志的记录、清空和自动滚动标志。
    /// </summary>
    public class SecsLogViewModel : BindableBase
    {
        /// <summary>初始化实例</summary>
        public SecsLogViewModel()
        {
            ClearLogCommand = new DelegateCommand(() => TransactionLogs.Clear());
        }

        // ── 集合 ───────────────────────────────────────────────────────────────
        /// <summary>获取通信日志集合</summary>
        public ObservableCollection<TransactionLogEntry> TransactionLogs { get; } = new();

        // ── 自动滚动 ───────────────────────────────────────────────────────────
        private bool _autoScrollLog = false;
        /// <summary>获取或设置是否自动滚动日志</summary>
        public bool AutoScrollLog
        {
            get => _autoScrollLog;
            set => SetProperty(ref _autoScrollLog, value);
        }

        // ── 命令 ───────────────────────────────────────────────────────────────
        /// <summary>清空日志命令</summary>
        public DelegateCommand ClearLogCommand { get; }

        // ── 写入方法 ───────────────────────────────────────────────────────────

        /// <summary>追加一条普通/系统日志。</summary>
        public void Append(SecsGemMessage msg, string message = null, bool isSystem = false)
        {
            TransactionLogEntry entry;
            if (isSystem || msg == null)
            {
                entry = new TransactionLogEntry
                {
                    Timestamp  = DateTime.Now,
                    Direction  = "ℹ",
                    Header     = message ?? "SYS",
                    RawHex     = string.Empty,
                    SmlText    = msg?.ToString() ?? message,
                    IsIncoming = false
                };
            }
            else
            {
                entry = CreateLogEntry(msg, message ?? "→");
            }
            TransactionLogs.Insert(0,entry);
        }

        /// <summary>追加一条接收到的报文日志。</summary>
        public void AppendReceived(SecsGemMessage msg) =>
            TransactionLogs.Insert(0,CreateLogEntry(msg, "←"));

        // ── 私有辅助 ───────────────────────────────────────────────────────────
        private static TransactionLogEntry CreateLogEntry(SecsGemMessage msg, string direction)
        {
            string rawHex = msg.SystemBytes != null
                ? BitConverter.ToString(msg.SystemBytes.ToArray()).Replace("-", " ")
                : string.Empty;

            string header = $"S{msg.Stream}F{msg.Function}" + (msg.WBit ? " W" : string.Empty);

            return new TransactionLogEntry
            {
                Timestamp  = DateTime.Now,
                Direction  = direction,
                Header     = header,
                RawHex     = rawHex,
                SmlText    = msg.ToString(),
                IsIncoming = msg.IsIncoming
            };
        }
    }
}
