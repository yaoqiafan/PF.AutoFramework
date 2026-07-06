namespace PF.Infrastructure.Communication.FileTransfer.Internal;

/// <summary>
/// 自适应分片大小计算。分片是应用层负载均衡/校验粒度概念，与网络层 MTU/巨帧无关——
/// 目标是让每条 Lane 分到足够多的分片（轮询能抹平速度差异），同时单片足够大（头部开销可忽略）。
/// </summary>
internal static class ChunkSizeCalculator
{
    public static int Calculate(long totalSize, int laneCount, int targetChunksPerLane, int minChunkSizeBytes, int maxChunkSizeBytes)
    {
        laneCount = Math.Max(1, laneCount);
        targetChunksPerLane = Math.Max(1, targetChunksPerLane);

        var ideal = totalSize / (laneCount * (long)targetChunksPerLane);
        return (int)Math.Clamp(ideal, minChunkSizeBytes, maxChunkSizeBytes);
    }
}
