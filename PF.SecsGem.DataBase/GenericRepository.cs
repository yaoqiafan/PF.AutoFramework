using Microsoft.EntityFrameworkCore;
using PF.Core.Interfaces.Data;
using System.Linq.Expressions;

namespace PF.SecsGem.DataBase
{
    /// <summary>
    /// 通用仓储实现，依赖 SecsGemDbContext
    /// </summary>
    public class GenericRepository<T> : IGenericRepository<T> where T : class, new()
    {
        protected SecsGemDbContext Context { get; }
        protected DbSet<T> DbSet { get; }

        /// <summary>
        /// GenericRepository 仓储
        /// </summary>
        public GenericRepository(SecsGemDbContext context)
        {
            Context = context;
            DbSet = context.Set<T>();
        }

        /// <summary>
        /// 获取ByIdAsync
        /// </summary>
        public virtual async Task<T?> GetByIdAsync(int id)
            => await DbSet.FindAsync(id);

        /// <summary>
        /// 获取AllAsync
        /// </summary>
        public virtual async Task<IEnumerable<T>> GetAllAsync()
            => await DbSet.ToListAsync();

        /// <summary>
        /// FindAsync异步操作
        /// </summary>
        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
            => await DbSet.Where(predicate).ToListAsync();

        /// <summary>
        /// SingleOrDefaultAsync异步操作
        /// </summary>
        public virtual async Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate)
            => await DbSet.SingleOrDefaultAsync(predicate);

        /// <summary>
        /// 添加Async
        /// </summary>
        public virtual async Task<T> AddAsync(T entity)
        {
            var entry = await DbSet.AddAsync(entity);
            return entry.Entity;
        }

        /// <summary>
        /// 添加RangeAsync
        /// </summary>
        public virtual async Task AddRangeAsync(IEnumerable<T> entities)
            => await DbSet.AddRangeAsync(entities);

        /// <summary>
        /// 更新Async
        /// </summary>
        public virtual Task UpdateAsync(T entity)
        {
            DbSet.Update(entity);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 更新RangeAsync
        /// </summary>
        public virtual Task UpdateRangeAsync(IEnumerable<T> entities)
        {
            DbSet.UpdateRange(entities);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 移除Async
        /// </summary>
        public virtual Task RemoveAsync(T entity)
        {
            DbSet.Remove(entity);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 移除RangeAsync
        /// </summary>
        public virtual Task RemoveRangeAsync(IEnumerable<T> entities)
        {
            DbSet.RemoveRange(entities);
            return Task.CompletedTask;
        }

        /// <summary>
        /// CountAsync异步操作
        /// </summary>
        public virtual async Task<int> CountAsync()
            => await DbSet.CountAsync();

        /// <summary>
        /// AnyAsync异步操作
        /// </summary>
        public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
            => await DbSet.AnyAsync(predicate);

        /// <summary>
        /// 保存ChangesAsync
        /// </summary>
        public virtual async Task<int> SaveChangesAsync()
            => await Context.SaveChangesAsync();
    }
}
