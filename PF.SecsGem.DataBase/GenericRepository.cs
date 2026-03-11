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

        public GenericRepository(SecsGemDbContext context)
        {
            Context = context;
            DbSet = context.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(int id)
            => await DbSet.FindAsync(id);

        public virtual async Task<IEnumerable<T>> GetAllAsync()
            => await DbSet.ToListAsync();

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
            => await DbSet.Where(predicate).ToListAsync();

        public virtual async Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate)
            => await DbSet.SingleOrDefaultAsync(predicate);

        public virtual async Task<T> AddAsync(T entity)
        {
            var entry = await DbSet.AddAsync(entity);
            return entry.Entity;
        }

        public virtual async Task AddRangeAsync(IEnumerable<T> entities)
            => await DbSet.AddRangeAsync(entities);

        public virtual Task UpdateAsync(T entity)
        {
            DbSet.Update(entity);
            return Task.CompletedTask;
        }

        public virtual Task UpdateRangeAsync(IEnumerable<T> entities)
        {
            DbSet.UpdateRange(entities);
            return Task.CompletedTask;
        }

        public virtual Task RemoveAsync(T entity)
        {
            DbSet.Remove(entity);
            return Task.CompletedTask;
        }

        public virtual Task RemoveRangeAsync(IEnumerable<T> entities)
        {
            DbSet.RemoveRange(entities);
            return Task.CompletedTask;
        }

        public virtual async Task<int> CountAsync()
            => await DbSet.CountAsync();

        public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
            => await DbSet.AnyAsync(predicate);

        public virtual async Task<int> SaveChangesAsync()
            => await Context.SaveChangesAsync();
    }
}
