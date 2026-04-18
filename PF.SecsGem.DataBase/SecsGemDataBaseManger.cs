using Microsoft.EntityFrameworkCore;
using PF.Core.Enums;
using PF.Core.Interfaces.Data;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.SecsGem.DataBase.Entities.Command;
using PF.SecsGem.DataBase.Entities.System;
using PF.SecsGem.DataBase.Entities.Variable;

namespace PF.SecsGem.DataBase
{
    /// <summary>
    /// IDisposable
    /// </summary>
    public class SecsGemDataBaseManger : ISecsGemDataBase, IDisposable
    {
        private readonly SecsGemDbContext _context;
        private readonly Dictionary<SecsDbSet, object> _repositories;
        private bool _disposed = false;

        /// <summary>
        /// SecsGemDataBaseManger 管理器
        /// </summary>
        public SecsGemDataBaseManger(SecsGemDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

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

        /// <summary>
        /// 初始化数据库：若库不存在则自动创建表结构。
        /// Bug3 Fix: 原来抛 NotImplementedException，现在正确实现。
        /// </summary>
        public async Task<bool> InitializationDataBase()
        {
            try
            {
                // EnsureCreatedAsync：库不存在时按当前模型创建；已存在则不做任何操作。
                // 若将来需要迁移历史，可改为 MigrateAsync()。
                await _context.Database.EnsureCreatedAsync();
                return true;
            }
            catch (Exception ex)
            {
                // 写日志或向上抛均可，此处先返回 false 避免阻断启动
                Console.WriteLine($"[SecsGemDataBaseManger] InitializationDataBase failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>根据枚举获取对应的仓储</summary>
        public IGenericRepository<T> GetRepository<T>(SecsDbSet dbSet) where T : class, IEntity, new()
        {
            if (_repositories.TryGetValue(dbSet, out var repository))
                return (IGenericRepository<T>)repository;

            throw new ArgumentException($"No repository found for {dbSet}", nameof(dbSet));
        }

        /// <summary>保存所有更改（自动刷新时间戳）</summary>
        public async Task<int> SaveChangesAsync()
        {
            UpdateEntityTimestamps();
            return await _context.SaveChangesAsync();
        }

        private void UpdateEntityTimestamps()
        {
            var entries = _context.ChangeTracker.Entries<IEntity>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                    entry.Entity.CreateTime = DateTime.Now;

                entry.Entity.UpdateTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    _context?.Dispose();

                _disposed = true;
            }
        }
    }
}
