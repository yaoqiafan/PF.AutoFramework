using Microsoft.EntityFrameworkCore;
using PF.Core.Enums;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Logging;
using PF.Core.Models;
using PF.Data.Context;
using PF.Data.Entity.Alarm;
using System.Collections.Concurrent;

namespace PF.Services.Alarm
{
    /// <summary>
    /// 报警业务服务实现。
    /// <list type="bullet">
    ///   <item>活跃报警维护在内存并发字典中，O(1) 查询。</item>
    ///   <item>防抖机制：同一 source + errorCode 在 2 秒内重复触发时，仅更新时间戳，不重复落盘。</item>
    ///   <item>兜底机制：未知 errorCode 自动生成通用记录，故障不被吞噬。</item>
    ///   <item>分表路由：历史记录按年份写入/读取对应的 AlarmRecord_YYYY 表。</item>
    /// </list>
    /// </summary>
    internal sealed class AlarmService : IAlarmService
    {
        private readonly IAlarmDictionaryService _dictionary;
        private readonly DbContextOptions<AlarmDbContext> _dbOptions;
        private readonly ILogService? _logger;

        // key = source，value = 当前活跃报警状态（含防抖信息）
        private readonly ConcurrentDictionary<string, ActiveAlarmState> _activeMap = new();

        // 防抖窗口
        private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(2);

        public AlarmService(
            IAlarmDictionaryService dictionary,
            DbContextOptions<AlarmDbContext> dbOptions,
            ILogService? logger = null)
        {
            _dictionary = dictionary;
            _dbOptions   = dbOptions;
            _logger      = logger;
        }

        // ── IAlarmService ───────────────────────────────────────────────────

        public IReadOnlyList<AlarmRecord> ActiveAlarms =>
            _activeMap.Values.Select(s => s.Record).ToList().AsReadOnly();

        public event EventHandler<AlarmRecord>? AlarmTriggered;
        public event EventHandler<AlarmRecord>? AlarmCleared;

        /// <inheritdoc/>
        public void TriggerAlarm(string source, string errorCode)
        {
            if (string.IsNullOrWhiteSpace(source))   throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(errorCode)) throw new ArgumentNullException(nameof(errorCode));

            var now  = DateTime.Now;
            var info = _dictionary.GetAlarmInfo(errorCode); // 兜底自动处理未知代码

            // 防抖检查
            if (_activeMap.TryGetValue(source, out var existing))
            {
                if (existing.ErrorCode == errorCode &&
                    (now - existing.LastTriggerTime) <= DebounceWindow)
                {
                    // 相同代码、2秒内 → 仅更新时间戳，不落盘
                    existing.LastTriggerTime     = now;
                    existing.Record.TriggerTime  = now;
                    return;
                }

                // 不同代码或超时 → 先清除旧的，再新建
                ClearAlarmInternal(source, now);
            }

            // 构造新记录
            var record = new AlarmRecord
            {
                ErrorCode   = errorCode,
                Source      = source,
                TriggerTime = now,
                IsActive    = true,
                Category    = info.Category,
                Message     = info.Message,
                Severity    = info.Severity,
                Solution    = info.Solution
            };

            var state = new ActiveAlarmState
            {
                ErrorCode       = errorCode,
                LastTriggerTime = now,
                Record          = record
            };

            _activeMap[source] = state;

            // 异步落盘（不阻塞调用方）
            _ = PersistRecordAsync(record);

            _logger?.Warn($"[报警触发] [{info.Severity}] [{errorCode}] {source}: {info.Message}", "AlarmService");
            AlarmTriggered?.Invoke(this, record);
        }

        /// <inheritdoc/>
        public void ClearAlarm(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return;
            ClearAlarmInternal(source, DateTime.Now);
        }

        /// <inheritdoc/>
        public void ClearAllActiveAlarms()
        {
            var now     = DateTime.Now;
            var sources = _activeMap.Keys.ToList();
            foreach (var s in sources)
                ClearAlarmInternal(s, now);

            _logger?.Info("所有活跃报警已清除（复位操作）", "AlarmService");
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<AlarmRecord>> QueryHistoricalAlarmsAsync(
            int year = 0,
            string? category = null,
            AlarmSeverity? minSeverity = null,
            int pageSize = 100,
            int page = 0)
        {
            var targetYear = year > 0 ? year : DateTime.Now.Year;

            try
            {
                await using var ctx = new AlarmDbContext(_dbOptions, targetYear);
                await EnsureYearTableAsync(ctx);

                var query = ctx.AlarmRecords.AsNoTracking().AsQueryable();

                var records = await query
                    .OrderByDescending(r => r.TriggerTime)
                    .Skip(page * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // 联查字典，组装 AlarmRecord 模型
                return records.Select(entity =>
                {
                    var info = _dictionary.GetAlarmInfo(entity.ErrorCode);
                    // Category / Severity 过滤（在内存中做，避免 EF Core 转换复杂度）
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
                        Solution    = info.Solution
                    };
                })
                .Where(r => category == null || r.Category == category)
                .Where(r => minSeverity == null || r.Severity >= minSeverity)
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

        private void ClearAlarmInternal(string source, DateTime clearTime)
        {
            if (!_activeMap.TryRemove(source, out var state)) return;

            state.Record.ClearTime = clearTime;
            state.Record.IsActive  = false;

            _ = UpdateClearTimeAsync(state.Record);

            _logger?.Info($"[报警清除] [{state.ErrorCode}] {source}", "AlarmService");
            AlarmCleared?.Invoke(this, state.Record);
        }

        /// <summary>异步将新记录写入当年分表，并回写 Id</summary>
        private async Task PersistRecordAsync(AlarmRecord record)
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

                record.Id = entity.Id; // 回写自增主键
            }
            catch (Exception ex)
            {
                _logger?.Error($"报警记录落盘失败 [{record.ErrorCode}]", "AlarmService", ex);
            }
        }

        /// <summary>异步更新已有记录的清除时间</summary>
        private async Task UpdateClearTimeAsync(AlarmRecord record)
        {
            if (record.Id == 0) return; // 尚未落盘（极短时间内被清除）

            try
            {
                var year = record.TriggerTime.Year;
                await using var ctx = new AlarmDbContext(_dbOptions, year);

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
            if (dbJustCreated) return; // 全新数据库，EnsureCreated 已建好所有表

            // 数据库已存在 → 检查并按需创建本年分表（跨年场景）
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

        // ── 内部状态类 ──────────────────────────────────────────────────────

        private sealed class ActiveAlarmState
        {
            public string ErrorCode { get; set; } = string.Empty;
            public DateTime LastTriggerTime { get; set; }
            public AlarmRecord Record { get; set; } = null!;
        }
    }
}
