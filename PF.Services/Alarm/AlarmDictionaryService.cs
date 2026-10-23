using Microsoft.EntityFrameworkCore;
using PF.Core.Attributes;
using PF.Core.Enums;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Logging;
using PF.Core.Models;
using PF.Data.Context;
using PF.Data.Entity.Alarm;
using System.Collections.Concurrent;
using System.Reflection;

namespace PF.Services.Alarm
{
    /// <summary>
    /// 报警字典服务实现。
    /// <list type="number">
    ///   <item>反射扫描代码中的 <c>[AlarmInfo]</c> 常量，作为报警文案的唯一来源。</item>
    ///   <item>与数据库 AlarmDefinitions 表同步（同默认参数加载方式）：缺的插入、多的删除；
    ///         清除权限等级以数据库为准，其余文案字段以代码为准刷新。</item>
    /// </list>
    /// 不再从数据库合并加载扩展报警码（数据库条目不能新增或覆盖报警文案）。
    /// </summary>
    internal sealed class AlarmDictionaryService : IAlarmDictionaryService
    {
        private const string LogCategory = "AlarmDictionary";

        private readonly DbContextOptions<AlarmDbContext> _dbOptions;
        private readonly ILogService? _logger;
        private readonly ConcurrentDictionary<string, AlarmInfo> _dictionary = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, byte> _unknownCodes = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _dbLock = new(1, 1);
        private bool _initialized;

        /// <inheritdoc/>
        public event EventHandler? RequiredLevelsChanged;

        public AlarmDictionaryService(DbContextOptions<AlarmDbContext> dbOptions, ILogService? logger = null)
        {
            _dbOptions = dbOptions;
            _logger    = logger;
        }

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            if (_initialized) return;

            // 1. 反射扫描所有已加载程序集中打了 [AlarmInfo] 的 const string 字段
            int codeCount = LoadFromReflection();

            // 2. 与数据库同步：写入新报警码、刷新文案、移除已删除的报警码，并读回现场配置的清除等级
            await SyncDatabaseAsync();

            _initialized = true;

            // 按严重级别、清除等级与分类汇总统计，便于运维快速核对报警字典规模
            var all   = _dictionary.Values.ToList();
            var fatal = all.Count(i => i.Severity == AlarmSeverity.Fatal);
            var error = all.Count(i => i.Severity == AlarmSeverity.Error);
            var warn  = all.Count(i => i.Severity == AlarmSeverity.Warning);
            var info  = all.Count(i => i.Severity == AlarmSeverity.Information);
            var levels = string.Join("  ", all
                .GroupBy(i => i.RequiredLevel)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key.ToDisplayName()}:{g.Count()}"));
            var categories = all
                .Select(v => v.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            _logger?.Info(
                $"报警字典初始化完成：共 {codeCount} 条 — 致命:{fatal}  错误:{error}  警告:{warn}  信息:{info}  " +
                $"清除等级[{levels}]  分类({categories.Count}): [{string.Join(", ", categories)}]",
                LogCategory);
        }

        /// <inheritdoc/>
        public AlarmInfo GetAlarmInfo(string errorCode)
        {
            if (_dictionary.TryGetValue(errorCode, out var info))
                return info;

            // 每个未知代码仅 Warn 一次，避免高并发下刷屏
            if (_unknownCodes.TryAdd(errorCode, 0))
            {
                _logger?.Warn(
                    $"发现未定义报警代码 [{errorCode}]，建议将其添加至 AlarmCodes 常量（[AlarmInfo] 特性）。",
                    LogCategory);
            }

            // 兜底：未定义代码返回通用条目，确保故障不被吞噬
            return new AlarmInfo
            {
                ErrorCode    = errorCode,
                Category     = "未定义报警",
                Message      = $"未定义报警 (未知代码: {errorCode})",
                MessageEn    = "Undefined alarm",
                Severity     = AlarmSeverity.Error,
                Solution     = "该报警代码尚未在字典中定义。\n1. 联系技术支持确认代码含义;\n2. 记录报警代码并联系开发人员在 AlarmCodes 中补充定义;",
                MessageID    = -1,
                MessageIDHex = "NoneError"
            };
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, AlarmInfo> GetAll() => _dictionary;

        /// <inheritdoc/>
        public UserLevel GetRequiredLevel(string errorCode)
        {
            if (string.IsNullOrEmpty(errorCode)) return UserLevel.Operator;
            return _dictionary.TryGetValue(errorCode, out var info) ? info.RequiredLevel : UserLevel.Operator;
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateRequiredLevelsAsync(IReadOnlyDictionary<string, UserLevel> levels, string operatorName)
        {
            if (levels == null || levels.Count == 0) return true;

            var changes = new List<(AlarmInfo Info, UserLevel NewLevel)>();
            foreach (var (code, level) in levels)
            {
                if (!_dictionary.TryGetValue(code, out var info)) continue;
                var newLevel = NormalizeLevel(level);
                if (info.RequiredLevel != newLevel)
                    changes.Add((info, newLevel));
            }

            if (changes.Count == 0) return true;

            await _dbLock.WaitAsync();
            try
            {
                await using var ctx = new AlarmDbContext(_dbOptions);
                var codes = changes.Select(c => c.Info.ErrorCode).ToList();
                var rows = (await ctx.AlarmDefinitions.Where(r => codes.Contains(r.ErrorCode)).ToListAsync())
                    .ToDictionary(r => r.ErrorCode, StringComparer.OrdinalIgnoreCase);

                foreach (var (info, newLevel) in changes)
                {
                    if (rows.TryGetValue(info.ErrorCode, out var row))
                    {
                        row.RequiredLevel = newLevel;
                    }
                    else
                    {
                        // 启动同步失败等情况下库里可能缺行，这里补上
                        var entity = new AlarmDefinitionEntity { ErrorCode = info.ErrorCode, RequiredLevel = newLevel };
                        ApplyCodeDefinition(entity, info);
                        ctx.AlarmDefinitions.Add(entity);
                    }
                }

                await ctx.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger?.Error("保存报警清除权限等级失败", LogCategory, ex);
                return false;
            }
            finally
            {
                _dbLock.Release();
            }

            foreach (var (info, newLevel) in changes)
            {
                _dictionary[info.ErrorCode] = info.WithRequiredLevel(newLevel);
                _logger?.Info(
                    $"[异常权限] 用户[{operatorName}] 修改报警清除等级 | 代码：{info.ErrorCode} | 报警：{info.Message} | " +
                    $"{info.RequiredLevel.ToDisplayName()} → {newLevel.ToDisplayName()}",
                    "操作日志");
            }

            try { RequiredLevelsChanged?.Invoke(this, EventArgs.Empty); }
            catch (Exception ex) { _logger?.Error("RequiredLevelsChanged 订阅者异常", LogCategory, ex); }

            return true;
        }

        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 扫描 AppDomain 中所有已加载程序集，收集任意类层次中打了 [AlarmInfo] 标签的 const string 字段。
        /// 支持 AlarmCodes（核心层）和 AlarmCodesExtensions（工站层）等多处定义，无需手动注册。
        /// </summary>
        private int LoadFromReflection()
        {
            int count = 0;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { count += ScanAssembly(assembly); }
                catch { /* 跳过无法反射的动态程序集 */ }
            }
            return count;
        }

        private int ScanAssembly(Assembly assembly)
        {
            int count = 0;
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch { return 0; }

            foreach (var type in types)
            {
                count += ScanTypeFields(type);
                foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                    count += ScanTypeFields(nested);
            }
            return count;
        }

        private int ScanTypeFields(Type type)
        {
            int count = 0;
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(string) || !field.IsLiteral) continue;

                var attr = field.GetCustomAttribute<AlarmInfoAttribute>();
                if (attr == null) continue;

                var code = (string?)field.GetValue(null);
                if (string.IsNullOrEmpty(code)) continue;

                // 重复定义时保留先扫描到的条目
                if (_dictionary.ContainsKey(code)) continue;

                var level = NormalizeLevel(attr.ClearLevel);
                _dictionary[code] = new AlarmInfo
                {
                    ErrorCode            = code,
                    Category             = attr.Category,
                    Message              = attr.Message,
                    MessageEn            = attr.MessageEn,
                    Severity             = attr.Severity,
                    Solution             = attr.Solution,
                    ImagePath            = attr.ImagePath,
                    MessageID            = attr.MessageID,
                    MessageIDHex         = attr.MessageIDHex,
                    RequiredLevel        = level,
                    DefaultRequiredLevel = level,
                };
                count++;
            }
            return count;
        }

        /// <summary>
        /// 与数据库 AlarmDefinitions 表同步（同默认参数的加载方式）：
        /// 代码有、库里没有 → 插入（等级取特性默认值）；库里已有 → 等级读回内存，文案按代码刷新；
        /// 库里有、代码已删除 → 删除（删除前逐条写日志）。失败时仅使用代码默认等级，不影响报警功能。
        /// </summary>
        private async Task SyncDatabaseAsync()
        {
            await _dbLock.WaitAsync();
            try
            {
                await using var ctx = new AlarmDbContext(_dbOptions);
                await ctx.Database.EnsureCreatedAsync();
                await EnsureRequiredLevelColumnAsync(ctx);

                var rows = await ctx.AlarmDefinitions.ToListAsync();
                var rowMap = rows
                    .GroupBy(r => r.ErrorCode, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                int inserted = 0, refreshed = 0, customized = 0;

                foreach (var (code, info) in _dictionary.ToArray())
                {
                    if (rowMap.TryGetValue(code, out var row))
                    {
                        if (ApplyCodeDefinition(row, info)) refreshed++;

                        var dbLevel = NormalizeLevel(row.RequiredLevel);
                        if (row.RequiredLevel != dbLevel) row.RequiredLevel = dbLevel;
                        if (dbLevel != info.RequiredLevel) _dictionary[code] = info.WithRequiredLevel(dbLevel);
                        if (dbLevel != info.DefaultRequiredLevel) customized++;
                    }
                    else
                    {
                        var entity = new AlarmDefinitionEntity { ErrorCode = code, RequiredLevel = info.DefaultRequiredLevel };
                        ApplyCodeDefinition(entity, info);
                        ctx.AlarmDefinitions.Add(entity);
                        inserted++;
                    }
                }

                var stale = rows.Where(r => !_dictionary.ContainsKey(r.ErrorCode)).ToList();
                foreach (var row in stale)
                {
                    _logger?.Warn(
                        $"移除代码中已不存在的报警码 [{row.ErrorCode}]：{row.Message}（分类：{row.Category}，清除等级：{row.RequiredLevel.ToDisplayName()}）",
                        LogCategory);
                }
                ctx.AlarmDefinitions.RemoveRange(stale);

                await ctx.SaveChangesAsync();

                _logger?.Info(
                    $"报警字典数据库同步完成：新增 {inserted} 条，刷新文案 {refreshed} 条，移除 {stale.Count} 条，" +
                    $"现场自定义清除等级 {customized} 条",
                    LogCategory);
            }
            catch (Exception ex)
            {
                _logger?.Error("报警字典数据库同步失败，清除权限等级将使用代码默认值", LogCategory, ex);
            }
            finally
            {
                _dbLock.Release();
            }
        }

        /// <summary>
        /// 老库的 AlarmDefinitions 表由 EnsureCreated 按旧模型建出，没有 RequiredLevel 列，
        /// EnsureCreated 在库已存在时不会补列，这里幂等补齐（默认 0 = Operator，保持升级前"所有人可清除"的行为）。
        /// </summary>
        private static async Task EnsureRequiredLevelColumnAsync(AlarmDbContext ctx)
        {
            var connection = ctx.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(AlarmDefinitions);";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (string.Equals(reader.GetString(1), nameof(AlarmDefinitionEntity.RequiredLevel), StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }

            await ctx.Database.ExecuteSqlRawAsync(
                "ALTER TABLE AlarmDefinitions ADD COLUMN RequiredLevel INTEGER NOT NULL DEFAULT 0;");
        }

        /// <summary>把代码定义的文案字段写入实体，返回是否有变化（等级字段不动，以数据库为准）</summary>
        private static bool ApplyCodeDefinition(AlarmDefinitionEntity entity, AlarmInfo info)
        {
            bool changed =
                entity.Category     != info.Category  ||
                entity.Message      != info.Message   ||
                entity.MessageEn    != info.MessageEn ||
                entity.Severity     != info.Severity  ||
                entity.Solution     != info.Solution  ||
                entity.MessageID    != info.MessageID ||
                entity.MessageIDHex != info.MessageIDHex;

            if (!changed) return false;

            entity.Category     = info.Category;
            entity.Message      = info.Message;
            entity.MessageEn    = info.MessageEn;
            entity.Severity     = info.Severity;
            entity.Solution     = info.Solution;
            entity.MessageID    = info.MessageID;
            entity.MessageIDHex = info.MessageIDHex;
            return true;
        }

        /// <summary>清除等级只允许 Operator ~ SuperUser，越界值（含 Null）按 Operator 处理</summary>
        private static UserLevel NormalizeLevel(UserLevel level)
            => level is >= UserLevel.Operator and <= UserLevel.SuperUser ? level : UserLevel.Operator;
    }
}
