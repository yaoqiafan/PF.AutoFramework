using Microsoft.EntityFrameworkCore;
using PF.Core.Attributes;
using PF.Core.Enums;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Logging;
using PF.Core.Models;
using PF.Data.Context;
using System.Collections.Concurrent;
using System.Reflection;

namespace PF.Services.Alarm
{
    /// <summary>
    /// 报警字典服务实现。
    /// 启动时通过反射扫描 <see cref="AlarmCodes"/> 常量字段，
    /// 再叠加数据库 AlarmDefinitions 表（数据库定义优先级更高，可覆盖代码内置）。
    /// </summary>
    internal sealed class AlarmDictionaryService : IAlarmDictionaryService
    {
        private readonly DbContextOptions<AlarmDbContext> _dbOptions;
        private readonly ILogService? _logger;
        private readonly ConcurrentDictionary<string, AlarmInfo> _dictionary = new(StringComparer.OrdinalIgnoreCase);
        private bool _initialized;

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

            // 2. 叠加数据库扩展条目（数据库覆盖代码内置）
            int dbCount = await LoadFromDatabaseAsync();

            _initialized = true;
            _logger?.Info($"报警字典初始化完成：代码内置 {codeCount} 条，数据库扩展/覆盖 {dbCount} 条，" +
                          $"总计 {_dictionary.Count} 条", "AlarmDictionary");
        }

        /// <inheritdoc/>
        public AlarmInfo GetAlarmInfo(string errorCode)
        {
            if (_dictionary.TryGetValue(errorCode, out var info))
                return info;

            // 兜底：未定义代码返回通用条目，确保故障不被吞噬
            return new AlarmInfo
            {
                ErrorCode    = errorCode,
                Category     = "未定义报警",
                Message      = $"未定义报警 (未知代码: {errorCode})",
                Severity     = AlarmSeverity.Error,
                Solution     = "该报警代码尚未在字典中定义。\n1. 联系技术支持确认代码含义;\n2. 可在 AlarmDefinitions 数据库表中扩展此代码的定义;\n3. 记录报警代码并联系开发人员;",
                IsFromDatabase = false
            };
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, AlarmInfo> GetAll() => _dictionary;

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

                // 不覆盖已有条目；数据库扩展在第二阶段统一覆盖
                if (_dictionary.ContainsKey(code)) continue;

                _dictionary[code] = new AlarmInfo
                {
                    ErrorCode      = code,
                    Category       = attr.Category,
                    Message        = attr.Message,
                    Severity       = attr.Severity,
                    Solution       = attr.Solution,
                    ImagePath = attr.ImagePath,
                    IsFromDatabase = false
                };
                count++;
            }
            return count;
        }

        /// <summary>从数据库 AlarmDefinitions 表加载扩展条目（覆盖内置）</summary>
        private async Task<int> LoadFromDatabaseAsync()
        {
            try
            {
                await using var ctx = new AlarmDbContext(_dbOptions);
                await ctx.Database.EnsureCreatedAsync();

                var definitions = await ctx.AlarmDefinitions.AsNoTracking().ToListAsync();
                int count = 0;

                foreach (var def in definitions)
                {
                    if (string.IsNullOrEmpty(def.ErrorCode)) continue;

                    _dictionary[def.ErrorCode] = new AlarmInfo
                    {
                        ErrorCode      = def.ErrorCode,
                        Category       = def.Category,
                        Message        = def.Message,
                        Severity       = def.Severity,
                        Solution       = def.Solution,
                        IsFromDatabase = true
                    };
                    count++;
                }

                return count;
            }
            catch (Exception ex)
            {
                _logger?.Error("从数据库加载报警字典失败，将仅使用代码内置字典", "AlarmDictionary", ex);
                return 0;
            }
        }
    }
}
