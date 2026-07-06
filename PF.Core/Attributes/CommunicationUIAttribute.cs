namespace PF.Core.Attributes
{
    /// <summary>
    /// 通讯调试界面路由特性，供 CommunicationDebugView 通过反射自动发现并注册跳转目标。
    ///
    /// 使用示例：
    ///   [CommunicationUI("FileTransferDebugView")]
    ///   public sealed class FileTransferChannel : IFileTransferChannel, ICommunication { ... }
    ///
    /// 发现机制：
    ///   CommunicationDebugViewModel 遍历 ICommunicationManagerService.ActiveCommunications，
    ///   对每个实例的运行时类型读取此特性取得 ViewName，
    ///   并通过 IRegionManager.RequestNavigate 驱动右侧内容区域切换。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CommunicationUIAttribute : Attribute
    {
        /// <summary>Prism 导航视图名称（与 RegisterForNavigation 注册时使用的 key 一致）</summary>
        public string ViewName { get; }

        /// <summary>初始化通讯UI特性</summary>
        public CommunicationUIAttribute(string viewName)
        {
            ViewName = viewName;
        }
    }
}
