namespace PF.Workstation.Demo.Sync
{
    /// <summary>
    /// 本工站方案的流水线信号量名称常量
    ///
    /// ═══════════════════════════════════════════════════════════════════════
    ///  双信号量互锁时序图（Dual-Semaphore Interlocking Timeline）
    /// ═══════════════════════════════════════════════════════════════════════
    ///
    ///  初始状态：SlotEmpty=1  ProductReady=0
    ///
    ///  时间 →
    ///  ─────────────────────────────────────────────────────────────────────
    ///  取放工站(A)  [Wait SlotEmpty=1✓]──[PickAsync]──[PlaceAsync]──[Release ProductReady]
    ///                                                                        │
    ///  点胶工站(B)  ──────────[Wait ProductReady=0 阻塞]────────────────────[✓获取]──[Dispense]──[Release SlotEmpty]
    ///                                                                                                    │
    ///  取放工站(A)  ──────────────────[Wait SlotEmpty=0 阻塞]─────────────────────────────────────────[✓获取]──[下一轮]
    ///
    ///  结果：A 和 B 永远交替执行，不会出现：
    ///    · B 在没有产品时点胶（ProductReady=0 时 B 被阻塞）
    ///    · A 在 B 未完成时叠料（SlotEmpty=0 时 A 被阻塞）
    /// ═══════════════════════════════════════════════════════════════════════
    ///
    ///  扩展方法：若需新增工站协同，在此文件添加新的信号量名称常量，
    ///  并在 MasterController 构造函数中调用 Register。
    /// </summary>
    public static class WorkstationSignals
    {
        /// <summary>
        /// 工作台槽位空闲信号
        /// · 初始计数 = 1（系统启动时槽位为空，取放工站可立即开始第一轮放料）
        /// · 取放工站（A）在每轮动作前 Wait，点胶工站（B）完成后 Release
        /// </summary>
        public const string SlotEmpty = "SlotEmpty";

        /// <summary>
        /// 产品已到位信号
        /// · 初始计数 = 0（系统启动时无产品，点胶工站初始阻塞）
        /// · 取放工站（A）放料完成后 Release，点胶工站（B）在每轮动作前 Wait
        /// </summary>
        public const string ProductReady = "ProductReady";
    }
}
