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
    ///   · ResetAll 确保系统复位后信号量回到初始状态，为下一轮启动做准备
    /// </summary>
    public interface IStationSyncService
    {
        /// <summary>
        /// 注册一个具名信号量。
        /// 应在系统启动初始化阶段（单线程）调用，不可重复注册同名信号量。
        /// </summary>
        /// <param name="name">信号量唯一名称</param>
        /// <param name="initialCount">初始可用计数（0=初始阻塞，1=初始放行）</param>
        /// <param name="maxCount">最大计数上限（通常为1，表示互斥）</param>
        void Register(string name, int initialCount = 0, int maxCount = 1);

        /// <summary>
        /// 异步等待指定信号量可用（计数 > 0 时立即通过，否则阻塞）。
        /// 支持 CancellationToken，急停/停止时可立即打断等待。
        /// </summary>
        Task WaitAsync(string name, CancellationToken token = default);

        /// <summary>
        /// 释放指定信号量，将计数 +1，唤醒一个正在等待的工站线程。
        /// </summary>
        void Release(string name);

        /// <summary>
        /// 将所有已注册的信号量复位到其初始计数状态。
        /// 应在 MasterController.ResetAll() 中调用，确保下一次启动状态正确。
        /// </summary>
        void ResetAll();
    }
}
