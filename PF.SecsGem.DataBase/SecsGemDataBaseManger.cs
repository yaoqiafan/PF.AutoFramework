using Microsoft.EntityFrameworkCore;
using PF.Core.Enums;
using PF.Core.Interfaces.Data;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.SecsGem.DataBase.Entities.Command;
using PF.SecsGem.DataBase.Entities.System;
using PF.SecsGem.DataBase.Entities.Variable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PF.SecsGem.DataBase
{
    /// <summary>
    /// SECS/GEM 数据库管理器。
    /// <para>持有线程安全的 <see cref="DbContextOptions{SecsGemDbContext}"/> 单例，
    /// 每次 <see cref="BeginScope"/> 创建一个独立的短生命周期 DbContext 作用域，
    /// 多线程调用各自隔离，避免 DbContext 的线程不安全问题。</para>
    /// </summary>
    public class SecsGemDataBaseManger : ISecsGemDataBase
    {
        private readonly DbContextOptions<SecsGemDbContext> _options;

        /// <summary>
        /// SecsGemDataBaseManger 管理器
        /// </summary>
        public SecsGemDataBaseManger(DbContextOptions<SecsGemDbContext> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// 开启一个工作单元作用域：内部 new 一个独立的短生命周期 DbContext，
        /// 作用域内的仓储操作与 SaveChanges 均绑定该 context，Dispose 时释放。
        /// </summary>
        public ISecsGemDbScope BeginScope() => new SecsGemDbScope(_options);

        /// <summary>
        /// 初始化数据库：若库不存在则自动创建表结构（独立短 context，不经作用域）。
        /// </summary>
        public async Task<bool> InitializationDataBase()
        {
            try
            {
                using var ctx = new SecsGemDbContext(_options);
                await ctx.Database.EnsureCreatedAsync();
                return true;
            }
            catch (Exception ex)
            {
                // 写日志或向上抛均可，此处先返回 false 避免阻断启动
                System.Diagnostics.Trace.WriteLine($"[SecsGemDataBaseManger] InitializationDataBase failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 工作单元作用域实现：持有短生命周期 DbContext + 仓储缓存，
        /// 确保 GetRepository 幂等返回绑定同一 context 的仓储。
        /// </summary>
        private sealed class SecsGemDbScope : ISecsGemDbScope
        {
            private readonly SecsGemDbContext _context;
            private readonly Dictionary<SecsDbSet, object> _repositories;
            private bool _disposed;

            public SecsGemDbScope(DbContextOptions<SecsGemDbContext> options)
            {
                _context = new SecsGemDbContext(options);
                _repositories = new Dictionary<SecsDbSet, object>
                {
                    { SecsDbSet.SystemConfigs,     new GenericRepository<SecsGemSystemEntity>(_context) },
                    { SecsDbSet.CommnadIDs,        new GenericRepository<CommandIDEntity>(_context) },
                    { SecsDbSet.CEIDs,             new GenericRepository<CEIDEntity>(_context) },
                    { SecsDbSet.ReportIDs,         new GenericRepository<ReportIDEntity>(_context) },
                    { SecsDbSet.VIDs,              new GenericRepository<VIDEntity>(_context) },
                    { SecsDbSet.IncentiveCommands, new GenericRepository<IncentiveEntity>(_context) },
                    { SecsDbSet.ResponseCommands,  new GenericRepository<ResponseEntity>(_context) }
                };
            }

            /// <summary>根据枚举获取对应的仓储（幂等，同 scope 内返回同一实例）</summary>
            public IGenericRepository<T> GetRepository<T>(SecsDbSet dbSet) where T : class, IEntity, new()
            {
                if (_repositories.TryGetValue(dbSet, out var repository))
                    return (IGenericRepository<T>)repository;

                throw new ArgumentException($"No repository found for {dbSet}", nameof(dbSet));
            }

            /// <summary>保存所有更改（自动刷新时间戳）</summary>
            public async Task<int> SaveChangesAsync()
            {
                UpdateEntityTimestamps(_context);
                return await _context.SaveChangesAsync();
            }

            private static void UpdateEntityTimestamps(SecsGemDbContext context)
            {
                var entries = context.ChangeTracker.Entries<IEntity>()
                    .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

                foreach (var entry in entries)
                {
                    if (entry.State == EntityState.Added)
                        entry.Entity.CreateTime = DateTime.Now;

                    entry.Entity.UpdateTime = DateTime.Now;
                }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _context?.Dispose();
                _disposed = true;
            }
        }
    }
}
