using Microsoft.EntityFrameworkCore;
using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Logging;
using PF.Core.Models;
using PF.Data.Context;
using PF.Data.Entity.Alarm;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace PF.Services.Alarm
{
    /// <summary>
    /// 报警业务服务实现。
    /// <list type="bullet">
    ///   <item>复合键 (Source, ErrorCode)：同一工站可同时持有多个不同代码的活跃报警，互不覆盖。</item>
    ///   <item>幂等触发：相同复合键已存在时直接跳过，不重复落盘。</item>
    ///   <item>有界 Channel 持久化队列（容量 10000，DropOldest 背压）：有序串行写入，防止竞态与内存 OOM。</item>
    ///   <item>兜底机制：未知 errorCode 自动生成通用记录，故障不被吞噬。</item>
    ///   <item>分表路由：历史记录按年份写入/读取对应的 AlarmRecord_YYYY 表。</item>
    /// </list>
    /// </summary>
    internal sealed class AlarmService : IAlarmService, IDisposable
    {
        private readonly IAlarmDictionaryService _dictionary;
        private readonly DbContextOptions<AlarmDbContext> _dbOptions;
        private readonly ILogService? _logger;
        private readonly IAlarmEventPublisher? _publisher;

        // 复合键：(Source, ErrorCode) → 同一工站可并发持有多条不同代码的活跃报警
        private readonly ConcurrentDictionary<(string Source, string ErrorCode), ActiveAlarmState> _activeMap = new();

        // 有界持久化队列：容量 10000，背压策略改为 Wait。
        // 原 DropOldest 在高并发停机时会将最早入队的首发故障报警挤出，导致根因丢失；
        // Wait 模式确保写入端阻塞等待空位，首发报警不被后续报警覆盖。
        // 容量 10000 已足够大，正常情况下不会触发背压；若数据库持续死锁，
        // 写入方阻塞是合理的背压信号，优于静默丢弃关键故障信息。
        private readonly Channel<PersistJob> _persistChannel = Channel.CreateBounded<PersistJob>(
            new BoundedChannelOptions(10_000)
            {
                FullMode                      = BoundedChannelFullMode.Wait,
                SingleReader                  = true,
                SingleWriter                  = false,
                AllowSynchronousContinuations = false
            });

        private readonly Task _persistWorker;
        private bool _disposed;

        // 持久化队列因满而丢弃的作业计数（仅用于诊断，正常应恒为 0；非 0 即表示数据库持续阻塞）
        private long _droppedPersistCount;

        public AlarmService(
            IAlarmDictionaryService dictionary,
            DbContextOptions<AlarmDbContext> dbOptions,
            IAlarmEventPublisher? publisher = null,
            ILogService? logger = null)
        {
            _dictionary = dictionary;
            _dbOptions  = dbOptions;
            _publisher  = publisher;
            _logger     = logger;

            // 启动单一后台串行消费者（保证数据库 ID 生成与回写不发生竞态）
            _persistWorker = Task.Run(RunPersistWorkerAsync);
        }

        // ── IAlarmService ───────────────────────────────────────────────────

        public IReadOnlyList<AlarmRecord> ActiveAlarms =>
            _activeMap.Values.Select(s => s.Record).ToList().AsReadOnly();

        // 保留 C# 事件供现有订阅者使用（Phase 3 迁移至 EventAggregator 后移除）
        public event EventHandler<AlarmRecord>? AlarmTriggered;
        public event EventHandler<AlarmRecord>? AlarmCleared;

        /// <inheritdoc/>
        public bool TriggerAlarm(string source, string errorCode)
            => TriggerAlarm(source, errorCode, runtimeMessage: null);

        /// <inheritdoc/>
        public bool TriggerAlarm(string source, string errorCode, string? runtimeMessage)
        {
            // 兜底降级：来源/错误码缺失时不再抛异常。抛异常会中断上游报警级联
            // （OnSubStationAlarm 抛出后不再 Fire(Error)，导致主控漏进报警态），
            // 改用占位值确保报警仍被记录、不被静默吞没。
            if (string.IsNullOrWhiteSpace(source))    source    = "未知来源";
            if (string.IsNullOrWhiteSpace(errorCode)) errorCode = AlarmCodes.System.UndefinedAlarm;

            var key = (source, errorCode);

            // 幂等：相同复合键已存在则跳过，返回 false 表示非首次触发（唯一去重源）
            if (_activeMap.ContainsKey(key)) return false;

            var now  = DateTime.Now;
            var info = _dictionary.GetAlarmInfo(errorCode); // 兜底自动处理未知代码
            var record = new AlarmRecord
            {
                ErrorCode   = errorCode,
                Source      = source,
                TriggerTime = now,
                IsActive    = true,
                Category    = info.Category,
                Message     = runtimeMessage ?? info.Message,  // 运行时消息优先
                Severity    = info.Severity,
                ImagePath   = info.ImagePath,
                Solution    = info.Solution
            };

            var state = new ActiveAlarmState { Record = record };

            // TryAdd 保证并发安全：若另一线程抢先插入相同 key 则跳过
            if (!_activeMap.TryAdd(key, state)) return false;

            EnqueuePersist(new PersistJob.Insert(record));

            _logger?.Warn($"[报警触发] [{info.Severity}] [{errorCode}] {source}: {info.Message}", "AlarmService");
            AlarmTriggered?.Invoke(this, record);
            _publisher?.PublishAlarmTriggered(record);
            return true;
        }

        /// <inheritdoc/>
        public void ClearAlarm(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return;

            var now  = DateTime.Now;
            var keys = _activeMap.Keys.Where(k => k.Source == source).ToList();
            foreach (var key in keys)
                ClearAlarmInternal(key, now);
        }

        /// <inheritdoc/>
        public void ClearAlarm(string source, string errorCode)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(errorCode)) return;
            ClearAlarmInternal((source, errorCode), DateTime.Now);
        }

        /// <inheritdoc/>
        public void ClearAllActiveAlarms()
        {
            var now  = DateTime.Now;
            var keys = _activeMap.Keys.ToList();
            foreach (var key in keys)
                ClearAlarmInternal(key, now);

            _logger?.Info("所有活跃报警已清除（复位操作）", "AlarmService");
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<AlarmRecord>> QueryHistoricalAlarmsAsync(
            int year = 0,
            DateTime? startTime = null,
            DateTime? endTime = null,
            string? category = null,
            AlarmSeverity? severity = null,
            string? source = null,
            string? errorCode = null,
            string? descriptionKeyword = null,
            int pageSize = 5000,
            int page = 0)
        {
            var targetYear = year > 0 ? year : (startTime?.Year ?? DateTime.Now.Year);

            try
            {
                await using var ctx = new AlarmDbContext(_dbOptions, targetYear);
                await EnsureYearTableAsync(ctx);

                var entities = await ctx.AlarmRecords
                    .AsNoTracking()
                    .OrderByDescending(r => r.TriggerTime)
                    .Skip(page * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // 联查字典并在内存层应用所有过滤条件
                return entities
                    .Select(entity =>
                    {
                        var info = _dictionary.GetAlarmInfo(entity.ErrorCode);
                        return new AlarmRecord
                        {
                            Id          = entity.Id,
                            ErrorCode   = entity.ErrorCode,
                            Source      = entity.Source,
                            TriggerTime = entity.TriggerTime,
                            ClearTime   = entity.ClearTime,
                            IsActive    = entity.IsActive,
                            Category    = info.Category,
                            Message     = info.Message,
                            Severity    = info.Severity,
                            ImagePath   = info.ImagePath,
                            Solution    = info.Solution
                        };
                    })
                    .Where(r => startTime          == null || r.TriggerTime >= startTime)
                    .Where(r => endTime            == null || r.TriggerTime <= endTime)
                    .Where(r => category           == null || r.Category == category)
                    .Where(r => severity           == null || r.Severity == severity)
                    .Where(r => source             == null || r.Source == source)
                    .Where(r => errorCode          == null || (r.ErrorCode?.Contains(errorCode, StringComparison.OrdinalIgnoreCase) ?? false))
                    .Where(r => descriptionKeyword == null || (r.Message?.Contains(descriptionKeyword, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList()
                    .AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger?.Error($"查询 {targetYear} 年报警历史失败", "AlarmService", ex);
                return Array.Empty<AlarmRecord>();
            }
        }

        // ── 私有方法 ────────────────────────────────────────────────────────

        private void ClearAlarmInternal((string Source, string ErrorCode) key, DateTime clearTime)
        {
            if (!_activeMap.TryRemove(key, out var state)) return;

            state.Record.ClearTime = clearTime;
            state.Record.IsActive  = false;

            EnqueuePersist(new PersistJob.UpdateClear(state.Record));

            _logger?.Info($"[报警清除] [{key.ErrorCode}] {key.Source}", "AlarmService");
            AlarmCleared?.Invoke(this, state.Record);
            _publisher?.PublishAlarmCleared(state.Record);
            _publisher?.PublishHardwareResetRequested(new HardwareResetRequest
            {
                Source     = key.Source,
                ErrorCodes = new[] { key.ErrorCode }
            });
        }

        /// <summary>
        /// 将持久化作业写入队列；队列满（数据库持续阻塞）时不静默丢弃，
        /// 而是升级为 Fatal 日志并累加丢弃计数，使关键报警信息的丢失对运维可见。
        /// </summary>
        private void EnqueuePersist(PersistJob job)
        {
            if (_persistChannel.Writer.TryWrite(job)) return;

            var dropped = Interlocked.Increment(ref _droppedPersistCount);
            SafeLog(logger => logger.Fatal(
                $"[AlarmService] 持久化队列已满，报警落盘作业被丢弃（累计 {dropped} 条）。" +
                "数据库可能持续阻塞，请立即检查。", "AlarmService", null));
        }

        /// <summary>
        /// 安全日志：吞掉日志组件自身可能抛出的异常（如关机时 LogService 已释放），
        /// 防止持久化 worker 因记日志失败而意外退出。
        /// </summary>
        private void SafeLog(Action<ILogService> log)
        {
            try { if (_logger != null) log(_logger); } catch { /* 日志失败不得影响主流程 */ }
        }

        /// <summary>
        /// 串行消费持久化队列。单读取者保证：Insert 落盘后 ID 回写再处理 UpdateClear，无竞态。
        /// 外层 while 重启外壳：即使 ReadAllAsync 枚举器或日志组件意外抛出，
        /// worker 也会重启而非永久退出（否则此后所有报警都会被静默丢弃）。
        /// </summary>
        private async Task RunPersistWorkerAsync()
        {
            while (!_disposed)
            {
                try
                {
                    await foreach (var job in _persistChannel.Reader.ReadAllAsync())
                    {
                        try
                        {
                            if (job is PersistJob.Insert ins)
                                await PersistInsertAsync(ins.Record);
                            else if (job is PersistJob.UpdateClear upd)
                                await PersistUpdateClearAsync(upd.Record);
                        }
                        catch (Exception ex)
                        {
                            SafeLog(logger => logger.Error("持久化队列工作项失败", "AlarmService", ex));
                        }
                    }
                    // ReadAllAsync 正常结束 = 通道已 Complete（Dispose 流程），退出 worker
                    break;
                }
                catch (Exception ex)
                {
                    SafeLog(logger => logger.Error("持久化 worker 异常，准备重启", "AlarmService", ex));
                }
            }
        }

        /// <summary>将新报警记录写入当年分表，并回写自增 Id</summary>
        private async Task PersistInsertAsync(AlarmRecord record)
        {
            try
            {
                var year = record.TriggerTime.Year;
                await using var ctx = new AlarmDbContext(_dbOptions, year);
                await EnsureYearTableAsync(ctx);

                var entity = new AlarmRecordEntity
                {
                    ErrorCode   = record.ErrorCode,
                    Source      = record.Source,
                    TriggerTime = record.TriggerTime,
                    IsActive    = true
                };

                ctx.AlarmRecords.Add(entity);
                await ctx.SaveChangesAsync();

                record.Id = entity.Id; // 回写自增主键（串行保证无竞态）
            }
            catch (Exception ex)
            {
                _logger?.Error($"报警记录落盘失败 [{record.ErrorCode}]", "AlarmService", ex);
            }
        }

        /// <summary>更新已有记录的清除时间</summary>
        private async Task PersistUpdateClearAsync(AlarmRecord record)
        {
            try
            {
                var year = record.TriggerTime.Year;
                await using var ctx = new AlarmDbContext(_dbOptions, year);

                // Id==0 说明此前 Insert 落盘失败（异常被吞，主键未回写）。
                // 单读者串行保证 Insert 作业必先于本 UpdateClear 处理，故 Id==0 只可能是 Insert 失败。
                // 此时库中无对应行可更新，降级为补插一条已闭合的完整记录，
                // 保证报警的触发+清除信息进入历史，不因 Insert 失败而彻底丢失。
                if (record.Id == 0)
                {
                    await EnsureYearTableAsync(ctx);
                    ctx.AlarmRecords.Add(new AlarmRecordEntity
                    {
                        ErrorCode   = record.ErrorCode,
                        Source      = record.Source,
                        TriggerTime = record.TriggerTime,
                        ClearTime   = record.ClearTime,
                        IsActive    = false
                    });
                    await ctx.SaveChangesAsync();
                    return;
                }

                var entity = await ctx.AlarmRecords.FindAsync(record.Id);
                if (entity == null) return;

                entity.ClearTime = record.ClearTime;
                entity.IsActive  = false;
                await ctx.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger?.Error($"更新报警清除时间失败 [Id={record.Id}]", "AlarmService", ex);
            }
        }

        /// <summary>
        /// 确保当年分表已创建（幂等）。
        /// EnsureCreated 首次调用时创建整库结构（含当年分表）；
        /// 后续跨年时，数据库已存在，EnsureCreated 不会建新表，
        /// 故额外使用 CREATE TABLE IF NOT EXISTS 兜底建表。
        /// </summary>
        private static async Task EnsureYearTableAsync(AlarmDbContext ctx)
        {
            bool dbJustCreated = await ctx.Database.EnsureCreatedAsync();
            if (dbJustCreated) return;

            var tableName = $"AlarmRecord_{ctx.CurrentYear}";
            await ctx.Database.ExecuteSqlRawAsync($"""
                CREATE TABLE IF NOT EXISTS "{tableName}" (
                    "Id"          INTEGER NOT NULL CONSTRAINT "PK_{tableName}" PRIMARY KEY AUTOINCREMENT,
                    "ErrorCode"   TEXT    NOT NULL DEFAULT '',
                    "Source"      TEXT    NOT NULL DEFAULT '',
                    "TriggerTime" TEXT    NOT NULL,
                    "ClearTime"   TEXT    NULL,
                    "IsActive"    INTEGER NOT NULL DEFAULT 0
                )
                """);

            await ctx.Database.ExecuteSqlRawAsync($"""
                CREATE INDEX IF NOT EXISTS "IX_{tableName}_Source_IsActive"
                ON "{tableName}" ("Source", "IsActive")
                """);

            await ctx.Database.ExecuteSqlRawAsync($"""
                CREATE INDEX IF NOT EXISTS "IX_{tableName}_TriggerTime"
                ON "{tableName}" ("TriggerTime")
                """);

            await ctx.Database.ExecuteSqlRawAsync($"""
                CREATE INDEX IF NOT EXISTS "IX_{tableName}_IsActive"
                ON "{tableName}" ("IsActive")
                """);
        }

        // ── IDisposable ─────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 关闭写入端，RunPersistWorkerAsync 的 ReadAllAsync 循环将自然退出
            _persistChannel.Writer.Complete();

            // 等待队列排空，最多 3 秒（超时保护，防止主线程挂起）
            Task.WaitAny(_persistWorker, Task.Delay(TimeSpan.FromSeconds(3)));

            if (!_persistWorker.IsCompleted)
                _logger?.Warn("[AlarmService] Dispose 超时：持久化队列未在 3s 内排空", "AlarmService");
        }

        // ── 内部状态类 ──────────────────────────────────────────────────────

        private sealed class ActiveAlarmState
        {
            public AlarmRecord Record { get; set; } = null!;
        }

        // ── 持久化任务判别联合类型 ──────────────────────────────────────────

        private abstract class PersistJob
        {
            public sealed class Insert(AlarmRecord record) : PersistJob
            {
                public AlarmRecord Record { get; } = record;
            }

            public sealed class UpdateClear(AlarmRecord record) : PersistJob
            {
                public AlarmRecord Record { get; } = record;
            }
        }
    }
}
