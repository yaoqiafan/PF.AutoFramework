namespace PF.WorkStation.AutoOcr.Mechanisms
{
    /// <summary>
    /// 寻层扫描互斥锁：确保工位1和工位2的 SearchLayerAsync 不会同时运行，
    /// 避免两台 Z 轴同时执行锁存扫描时因共享运动控制卡资源产生干扰。
    /// </summary>
    internal static class FeedingModelShared
    {
        internal static readonly SemaphoreSlim SearchLayerLock = new SemaphoreSlim(1, 1);
    }
}
