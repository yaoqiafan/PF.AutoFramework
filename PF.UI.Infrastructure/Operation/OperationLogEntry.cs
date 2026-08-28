namespace PF.UI.Infrastructure.Operation
{
    /// <summary>操作日志项目级配置的一行：某个页面下某个 Key 是否记录，以及展示用的描述文本。</summary>
    public class OperationLogEntry
    {
        /// <summary>展示用描述文本</summary>
        public string Description { get; set; }

        /// <summary>是否记录</summary>
        public bool Enabled { get; set; }
    }
}
