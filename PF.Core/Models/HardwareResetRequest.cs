namespace PF.Core.Models
{
    /// <summary>
    /// 硬件复位请求载体。
    /// 由 <see cref="PF.Core.Interfaces.Alarm.IAlarmService.ClearAlarm"/> 清除报警后发布，
    /// 驱动工站或机构执行物理复位动作。
    /// </summary>
    public sealed class HardwareResetRequest
    {
        /// <summary>触发复位的来源标识（工站名或设备名）</summary>
        public string Source { get; init; } = string.Empty;

        /// <summary>本次清除的错误码列表</summary>
        public IReadOnlyList<string> ErrorCodes { get; init; } = Array.Empty<string>();
    }
}
