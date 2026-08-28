namespace PF.Core.Interfaces.Configuration
{
    /// <summary>
    /// 操作日志开关。由应用层的配置对象（如 CommonSettings）实现并注册进容器，
    /// 供 PF.UI.Infrastructure 中的 ViewModelBase 判断是否记录选择性操作日志——
    /// UI 基础设施层不能直接依赖应用层的具体配置类型，故用接口反向暴露这一个开关。
    /// </summary>
    public interface IOperationLogSettings
    {
        /// <summary>是否记录详细界面操作日志</summary>
        bool EnableOperationLog { get; }
    }
}
