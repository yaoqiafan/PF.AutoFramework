using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Vision;
using PF.Vision.Halcon.Models;

namespace PF.Vision.Halcon.Services;

/// <summary>
/// 三模式视觉引擎管理器实现。
/// 每个槽位持有一个 <see cref="HalconVisionService"/>，按需懒创建，显式释放。
/// 线程安全：对槽位数组的读写通过 SemaphoreSlim(1,1) 保护。
/// </summary>
public sealed class VisionContextManager : IVisionContextManager, IDisposable
{
    private readonly string         _procedureDirectory;
    private readonly ILogService    _logger;
    private readonly SemaphoreSlim  _lock = new(1, 1);

    // 三个可空槽位，index == (int)EngineMode
    private readonly HalconVisionService?[] _engines = new HalconVisionService?[3];

    private bool _disposed;

    public VisionContextManager(string procedureDirectory, ILogService logger)
    {
        _procedureDirectory = procedureDirectory;
        _logger             = logger;

        // 非阻塞：只写 Warn 日志，不影响启动
        AlgorithmVerifier.VerifyAndUpdate(_procedureDirectory, _logger);
    }

    public IReadOnlyList<EngineMode> ActiveEngines =>
        Enum.GetValues<EngineMode>()
            .Where(m => _engines[(int)m] != null)
            .ToList();

    public bool IsActive(EngineMode mode) => _engines[(int)mode] != null;

    public IVisionService GetOrCreate(EngineMode mode)
    {
        // 快速路径：槽位非空时无锁返回（只会有创建/释放并发，且引用赋值是原子的）
        if (_engines[(int)mode] is { } existing) return existing;

        _lock.Wait();
        try
        {
            // 双重检查，防止并发创建
            if (_engines[(int)mode] is { } raceWinner) return raceWinner;

            var config = mode switch
            {
                EngineMode.Debug    => VisionEngineConfig.Debug,
                EngineMode.Offline  => VisionEngineConfig.Offline,
                _                   => VisionEngineConfig.Production,
            };

            var engine = new HalconVisionService(_procedureDirectory, _logger, config);
            _engines[(int)mode] = engine;
            _logger.Info($"[Vision] 引擎拉起: {mode} (cap={config.ChannelCapacity})", LogCategories.Vision);
            return engine;
        }
        finally { _lock.Release(); }
    }

    public async Task ReleaseAsync(EngineMode mode)
    {
        await _lock.WaitAsync();
        try
        {
            var engine = _engines[(int)mode];
            if (engine is null) return;

            _engines[(int)mode] = null;
            engine.Dispose();
            _logger.Info($"[Vision] 引擎释放: {mode}", LogCategories.Vision);
        }
        finally { _lock.Release(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        for (int i = 0; i < _engines.Length; i++)
        {
            _engines[i]?.Dispose();
            _engines[i] = null;
        }
        _lock.Dispose();
    }
}
