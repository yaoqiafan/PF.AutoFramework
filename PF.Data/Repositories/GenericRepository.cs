using Microsoft.EntityFrameworkCore;
using PF.Core.Interfaces.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace PF.Data.Repositories
{
    /// <summary>
    /// 通用仓库类
    /// </summary>
    public class GenericRepository<T> : IGenericRepository<T> where T : class, new()
    {
        /// <summary>
        /// 构造
        /// </summary>
        /// <param name="context">数据上下文</param>
        public GenericRepository(DbContext context)
        {
            Context = context;
            DbSet = context.Set<T>();
        }

        /// <summary>
        /// 数据上下文对象
        /// </summary>
        protected DbContext Context { get; }

        /// <summary>
        /// DbSet对象
        /// </summary>
        protected DbSet<T> DbSet { get; }

        /// <inheritdoc/>
        public virtual async Task<T?> GetByIdAsync(int id)
        {
            return await DbSet.FindAsync(id);
        }

        /// <inheritdoc/>
        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await DbSet.ToListAsync();
        }

        /// <inheritdoc/>
        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await DbSet.Where(predicate).ToListAsync();
        }

        /// <inheritdoc/>
        public virtual async Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await DbSet.SingleOrDefaultAsync(predicate);
        }

        /// <inheritdoc/>
        public virtual async Task<T> AddAsync(T entity)
        {
            var entry = await DbSet.AddAsync(entity);
            return entry.Entity;
        }

        /// <inheritdoc/>
        public virtual async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await DbSet.AddRangeAsync(entities);
        }

        /// <inheritdoc/>
        public virtual async Task UpdateAsync(T entity)
        {
            DbSet.Update(entity);
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public virtual async Task UpdateRangeAsync(IEnumerable<T> entities)
        {
            DbSet.UpdateRange(entities);
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public virtual async Task RemoveAsync(T entity)
        {
            DbSet.Remove(entity);
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public virtual async Task RemoveRangeAsync(IEnumerable<T> entities)
        {
            DbSet.RemoveRange(entities);
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public virtual async Task<int> CountAsync()
        {
            return await DbSet.CountAsync();
        }

        /// <inheritdoc/>
        public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            return await DbSet.AnyAsync(predicate);
        }

        /// <inheritdoc/>
        public virtual async Task<int> SaveChangesAsync()
        {
            return await Context.SaveChangesAsync();
        }
    }
}
