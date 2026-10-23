using PF.Core.Enums;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.Core.Models;

namespace PF.Services.Alarm
{
    /// <summary>
    /// 报警清除/复位权限判定实现：当前登录用户等级 ≥ 报警码配置的清除等级即可操作。
    /// 只服务于人为操作入口，程序自动清除不经过这里。
    /// </summary>
    internal sealed class AlarmPermissionService : IAlarmPermissionService
    {
        private readonly IAlarmDictionaryService _dictionary;
        private readonly IAlarmService _alarmService;
        private readonly IUserService _userService;
        private readonly ILogService? _logger;

        /// <inheritdoc/>
        public event EventHandler? PermissionChanged;

        public AlarmPermissionService(
            IAlarmDictionaryService dictionary,
            IAlarmService alarmService,
            IUserService userService,
            ILogService? logger = null)
        {
            _dictionary   = dictionary;
            _alarmService = alarmService;
            _userService  = userService;
            _logger       = logger;

            _userService.CurrentUserChanged  += (_, _) => RaisePermissionChanged();
            _dictionary.RequiredLevelsChanged += (_, _) => RaisePermissionChanged();
        }

        /// <inheritdoc/>
        public UserLevel CurrentUserLevel => _userService.CurrentUser?.Root ?? UserLevel.Null;

        /// <inheritdoc/>
        public string CurrentUserName => _userService.CurrentUser?.UserName ?? string.Empty;

        /// <inheritdoc/>
        public UserLevel GetRequiredLevel(string errorCode) => _dictionary.GetRequiredLevel(errorCode);

        /// <inheritdoc/>
        public bool CanClear(AlarmRecord record)
            => record != null && CurrentUserLevel >= GetRequiredLevel(record.ErrorCode);

        /// <inheritdoc/>
        public UserLevel GetResetRequiredLevel()
        {
            var required = UserLevel.Operator;
            foreach (var record in _alarmService.ActiveAlarms)
            {
                var level = GetRequiredLevel(record.ErrorCode);
                if (level > required) required = level;
            }
            return required;
        }

        /// <inheritdoc/>
        public bool CanReset(out UserLevel requiredLevel)
        {
            requiredLevel = GetResetRequiredLevel();
            return CurrentUserLevel >= requiredLevel;
        }

        /// <inheritdoc/>
        public (int Cleared, int Skipped) ClearAllAuthorized()
        {
            int cleared = 0, skipped = 0;
            foreach (var record in _alarmService.ActiveAlarms)
            {
                if (CanClear(record))
                {
                    _alarmService.ClearAlarm(record.Source, record.ErrorCode);
                    cleared++;
                }
                else
                {
                    skipped++;
                }
            }

            _logger?.Info(
                $"[报警处理] 用户[{CurrentUserName}]（{CurrentUserLevel.ToDisplayName()}）确认全部报警：清除 {cleared} 条" +
                (skipped > 0 ? $"，权限不足跳过 {skipped} 条" : string.Empty),
                "操作日志");

            return (cleared, skipped);
        }

        private void RaisePermissionChanged()
        {
            try { PermissionChanged?.Invoke(this, EventArgs.Empty); }
            catch (Exception ex) { _logger?.Error("PermissionChanged 订阅者异常", "AlarmPermission", ex); }
        }
    }
}
