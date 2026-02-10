using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Data.Context
{
    /// <summary>
    /// 通用数据库上下文工厂
    /// </summary>
    /// <typeparam name="TContext">数据库上下文类型</typeparam>
    public static class DbContextFactory<TContext> where TContext : DbContext
    {
        private static string? _connectionString;
        private static Action<DbContextOptionsBuilder<TContext>>? _configureAction;
        private static readonly ConcurrentDictionary<string, DbContextOptions<TContext>> _optionsCache = new();

        /// <summary>
        /// 初始化连接字符串
        /// </summary>
        /// <param name="connectionString">连接字符串</param>
        public static void Initialize(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _optionsCache.Clear(); // 清除缓存
        }

        /// <summary>
        /// 配置数据库上下文选项
        /// </summary>
        /// <param name="configureAction">配置动作</param>
        public static void Configure(Action<DbContextOptionsBuilder<TContext>> configureAction)
        {
            _configureAction = configureAction ?? throw new ArgumentNullException(nameof(configureAction));
            _optionsCache.Clear(); // 清除缓存
        }

        /// <summary>
        /// 创建数据库上下文选项（缓存版本）
        /// </summary>
        public static DbContextOptions<TContext> CreateDbContextOptions()
        {
            if (string.IsNullOrEmpty(_connectionString) && _configureAction == null)
            {
                throw new InvalidOperationException("请先调用 Initialize 或 Configure 方法进行初始化");
            }

            // 使用连接字符串作为缓存键
            string cacheKey = _connectionString ?? "configured";

            if (!_optionsCache.TryGetValue(cacheKey, out var options))
            {
                var optionsBuilder = new DbContextOptionsBuilder<TContext>();

                // 优先使用配置动作
                if (_configureAction != null)
                {
                    _configureAction(optionsBuilder);
                }
                else if (!string.IsNullOrEmpty(_connectionString))
                {
                    // 确保配置数据库提供程序
                    optionsBuilder.UseSqlite(_connectionString);
                }

                options = optionsBuilder.Options;
                _optionsCache.TryAdd(cacheKey, options);
            }

            return options;
        }

        /// <summary>
        /// 创建数据库上下文实例
        /// </summary>
        public static TContext CreateDbContext()
        {
            var options = CreateDbContextOptions();
            return Activator.CreateInstance(typeof(TContext), options) as TContext
                ?? throw new InvalidOperationException($"无法创建 {typeof(TContext).Name} 实例");
        }

        /// <summary>
        /// 创建带特定连接字符串的数据库上下文选项
        /// </summary>
        /// <param name="connectionString">连接字符串</param>
        public static DbContextOptions<TContext> CreateDbContextOptions(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("连接字符串不能为空", nameof(connectionString));
            }

            // 检查缓存
            if (_optionsCache.TryGetValue(connectionString, out var cachedOptions))
            {
                return cachedOptions;
            }

            // 创建新的选项
            var optionsBuilder = new DbContextOptionsBuilder<TContext>();

            // 如果已经配置了动作，使用配置动作，否则使用默认的SQLite
            if (_configureAction != null)
            {
                _configureAction(optionsBuilder);
            }
            else
            {
                optionsBuilder.UseSqlite(connectionString);
            }

            var options = optionsBuilder.Options;
            _optionsCache.TryAdd(connectionString, options);

            return options;
        }

        /// <summary>
        /// 创建带特定连接字符串的数据库上下文实例
        /// </summary>
        /// <param name="connectionString">连接字符串</param>
        public static TContext CreateDbContext(string connectionString)
        {
            var options = CreateDbContextOptions(connectionString);
            return Activator.CreateInstance(typeof(TContext), options) as TContext
                ?? throw new InvalidOperationException($"无法创建 {typeof(TContext).Name} 实例");
        }

        /// <summary>
        /// 清除所有缓存的选项
        /// </summary>
        public static void ClearCache()
        {
            _optionsCache.Clear();
        }

        /// <summary>
        /// 获取或设置连接字符串
        /// </summary>
        public static string? ConnectionString
        {
            get => _connectionString;
            set
            {
                if (_connectionString != value)
                {
                    _connectionString = value;
                    ClearCache(); // 连接字符串变更时清除缓存
                }
            }
        }

        /// <summary>
        /// 获取是否已初始化
        /// </summary>
        public static bool IsInitialized => !string.IsNullOrEmpty(_connectionString) || _configureAction != null;
    }
}
