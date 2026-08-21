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

        // 后台执行：校验会对目录下所有 .hdev 计算 MD5，文件多/盘慢时耗时显著，
        // 不得阻塞构造函数（DI 容器解析发生在启动路径上）
        _ = Task.Run(() =>
        {
            try { AlgorithmVerifier.VerifyAndUpdate(_procedureDirectory, _logger); }
            catch (Exception ex) { _logger.Warn("[Vision] 算法文件校验后台任务异常", LogCategories.Vision, ex); }
        });
    }

    public IReadOnlyList<EngineMode> ActiveEngines =>
        Enum.GetValues<EngineMode>()
            .Where(m => _engines[(int)m] != null)
            .ToList();

    public bool IsActive(EngineMode mode) => _engines[(int)mode] != null;

    // 与 IsActive 同构的无锁读：数组元素读写是原子的，Release/Dispose 会把槽位置 null，
    // 因此引擎释放后此处自然返回 null——不加锁是为了能在属性 getter 中安全调用
    public IVisionService? TryGet(EngineMode mode) => _engines[(int)mode];

    public IVisionService GetOrCreate(EngineMode mode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // 统一走锁：无锁快速路径会与 ReleaseAsync/Dispose 竞态，
        // 可能返回一个即将/已经被释放的引擎实例（调用频率低，锁开销可忽略）
        _lock.Wait();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

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

        // 持锁释放：与 GetOrCreate/ReleaseAsync 互斥，防止并发创建的引擎逃逸释放
        _lock.Wait();
        try
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = 0; i < _engines.Length; i++)
            {
                _engines[i]?.Dispose();
                _engines[i] = null;
            }
        }
        finally { _lock.Release(); }

        _lock.Dispose();
    }
}
