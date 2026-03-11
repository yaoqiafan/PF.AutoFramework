using Microsoft.EntityFrameworkCore;
using PF.Core.Enums;
using PF.Core.Interfaces.Data;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.Infrastructure.SecsGem.Entities.Command;
using PF.Infrastructure.SecsGem.Entities.System;
using PF.Infrastructure.SecsGem.Entities.Variable;

namespace PF.Infrastructure.SecsGem.DataBase
{
    public class SecsGemDataBaseManger : ISecsGemDataBase, IDisposable
    {
        private readonly SecsGemDbContext _context;
        private readonly Dictionary<SecsDbSet, object> _repositories;
        private bool _disposed = false;



        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="context">数据库上下文</param>
        public SecsGemDataBaseManger(SecsGemDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            // 初始化字典映射
            _repositories = new Dictionary<SecsDbSet, object>
        {
            { SecsDbSet.SystemConfigs, new GenericRepository<SecsGemSystemEntity>(_context) },
            { SecsDbSet.CommnadIDs, new GenericRepository<CommandIDEntity>(_context) },
            { SecsDbSet.CEIDs, new GenericRepository<CEIDEntity>(_context) },
            { SecsDbSet.ReportIDs, new GenericRepository<ReportIDEntity>(_context) },
            { SecsDbSet.VIDs, new GenericRepository<VIDEntity>(_context) },
            { SecsDbSet.IncentiveCommands, new GenericRepository<IncentiveEntity>(_context) },
            { SecsDbSet.ResponseCommands, new GenericRepository<ResponseEntity>(_context) }
        };
        }



        public async Task<bool> InitializationDataBase()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 根据枚举获取对应的仓储
        /// </summary>
        public IGenericRepository<T> GetRepository<T>(SecsDbSet dbSet) where T : class, IEntity, new()
        {
            if (_repositories.TryGetValue(dbSet, out var repository))
            {
                return (IGenericRepository<T>)repository;
            }

            throw new ArgumentException($"No repository found for {dbSet}", nameof(dbSet));
        }


        /// <summary>
        /// 保存所有更改
        /// </summary>
        public async Task<int> SaveChangesAsync()
        {
            // 更新实体的更新时间
            UpdateEntityTimestamps();
            return await _context.SaveChangesAsync();
        }
        /// <summary>
        /// 更新实体时间戳
        /// </summary>
        private void UpdateEntityTimestamps()
        {
            var entities = _context.ChangeTracker.Entries<IEntity>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entity in entities)
            {
                if (entity.State == EntityState.Added)
                {
                    entity.Entity.CreateTime = DateTime.Now;
                }
                entity.Entity.UpdateTime = DateTime.Now;
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
                {
                    _context?.Dispose();
                }
                _disposed = true;
            }
        }


    }
}
