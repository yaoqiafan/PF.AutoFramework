namespace PF.Core.Interfaces.Sync
{
    /// <summary>
    /// 工站间信号量同步服务接口
    ///
    /// 用于实现多工站流水线协同，基于双信号量互锁（Dual-Semaphore Interlocking）模式：
    ///
    ///   信号量 A (初始=1) ──► 工站1 等待A → 动作 → 释放B
    ///   信号量 B (初始=0) ──► 工站2 等待B → 动作 → 释放A
    ///
    /// 设计原则：
    ///   · 所有信号量在系统启动时通过 Register 预先注册，运行时只做 Wait/Release
    ///   · 信号量名称由调用方以常量字符串定义，服务本身不感知业务含义
    ///   · scope 参数将信号量按工站分组，支持单工站维度的独立复位（默认 "global"）
    ///   · ResetAll / ResetScope 确保系统复位后信号量回到初始状态，为下一轮启动做准备
    /// </summary>
    public interface IStationSyncService
    {
        /// <summary>
        /// 注册一个具名信号量。
        /// 应在系统启动初始化阶段（单线程）调用，不可重复注册同名信号量。
        /// </summary>
        /// <param name="name">信号量唯一名称（在同一 scope 内唯一）</param>
        /// <param name="initialCount">初始可用计数（0=初始阻塞，1=初始放行）</param>
        /// <param name="maxCount">最大计数上限（通常为1，表示互斥）</param>
        /// <param name="scope">所属工站分组名，默认 "global"</param>
        void Register(string name, int initialCount = 0, int maxCount = 1, string scope = "global");

        /// <summary>
        /// 异步等待指定信号量可用（计数 > 0 时立即通过，否则阻塞）。
        /// 内部将业务令牌与 scope 的复位广播令牌合并，
        /// 急停/停止/ResetScope 任意一个触发均可立即打断等待。
        /// </summary>
        /// <param name="name">信号量名称</param>
        /// <param name="token">业务取消令牌</param>
        /// <param name="scope">所属工站分组名，默认 "global"</param>
        Task WaitAsync(string name, CancellationToken token = default, string scope = "global");

        /// <summary>
        /// 释放指定信号量，将计数 +1，唤醒一个正在等待的工站线程。
        /// </summary>
        /// <param name="name">信号量名称</param>
        /// <param name="scope">所属工站分组名，默认 "global"</param>
        void Release(string name, string scope = "global");

        /// <summary>
        /// 将所有已注册的信号量（跨所有 scope）复位到其初始计数状态。
        /// 应在 MasterController 所有子工站成功复位后调用。
        /// </summary>
        void ResetAll();

        /// <summary>
        /// 将指定 scope 下的所有信号量复位到其初始计数状态。
        /// 内部先广播取消令牌（排空飞行中的 WaitAsync），
        /// 再 Dispose 旧信号量并以初始参数重建新实例。
        /// </summary>
        /// <param name="scope">要复位的工站分组名</param>
        void ResetScope(string scope);

        /// <summary>
        /// 快照读取所有已注册信号量的当前状态（只读，供监控 UI 轮询）。
        /// 返回字典：Key = "scope/name"，Value = (初始计数, 当前可用计数)。
        /// </summary>
        IReadOnlyDictionary<string, (int InitialCount, int CurrentCount)> GetSnapshot();
    }
}
