using PF.Core.Enums;

namespace PF.Core.Models
{
    /// <summary>
    /// 硬件加载进度消息载体，由底层服务通过 IProgress&lt;T&gt; 向 UI 层汇报。
    /// </summary>
    public record SplashProgressPayload
    {
        /// <summary>进度描述文本，显示在 Splash 界面上</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>日志分类，默认 "Hardware"</summary>
        public string Category { get; init; } = "Hardware";

        /// <summary>消息级别，决定 Splash 显示颜色及日志写入方式</summary>
        public MsgType MsgType { get; init; } = MsgType.Info;
    }
}
