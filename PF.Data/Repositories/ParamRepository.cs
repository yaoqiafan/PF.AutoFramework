using Microsoft.EntityFrameworkCore;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Data;
using System.Linq.Expressions;

namespace PF.Data.Repositories
{
    /// <summary>
    /// 参数仓储实现
    /// </summary>
    public class ParamRepository<T> : GenericRepository<T>, IParamRepository<T> where T : class, IEntity, new()
    {
        public ParamRepository(DbContext context) : base(context) { }

        public async Task<T?> GetByNameAsync(string name)
        {
            // 使用反射访问Name属性
            var parameter = Expression.Parameter(typeof(T), "p");
            var nameProperty = Expression.Property(parameter, "Name");
            var constant = Expression.Constant(name);
            var equals = Expression.Equal(nameProperty, constant);
            var lambda = Expression.Lambda<Func<T, bool>>(equals, parameter);

            return await DbSet.FirstOrDefaultAsync(lambda);
        }

        public async Task<List<T>> GetByCategoryAsync(string category)
        {
            // 使用反射访问Category和Name属性
            var parameter = Expression.Parameter(typeof(T), "p");
            var categoryProperty = Expression.Property(parameter, "Category");
            var categoryConstant = Expression.Constant(category);
            var categoryEquals = Expression.Equal(categoryProperty, categoryConstant);
            var lambda = Expression.Lambda<Func<T, bool>>(categoryEquals, parameter);

            return await DbSet.Where(lambda)
                             .OrderBy(p => EF.Property<string>(p, "Name"))
                             .ToListAsync();
        }

        public async Task<bool> ExistsAsync(string name)
        {
            var parameter = Expression.Parameter(typeof(T), "p");
            var nameProperty = Expression.Property(parameter, "Name");
            var constant = Expression.Constant(name);
            var equals = Expression.Equal(nameProperty, constant);
            var lambda = Expression.Lambda<Func<T, bool>>(equals, parameter);

            return await DbSet.AnyAsync(lambda);
        }

        public async Task<int> UpdateVersionAsync(string id, int version)
        {
            var param = await DbSet.FindAsync(id);
            if (param == null) return 0;

            // 使用反射设置Version和UpdateTime
            var type = param.GetType();
            var versionProperty = type.GetProperty("Version");
            var updateTimeProperty = type.GetProperty("UpdateTime");

            if (versionProperty != null && versionProperty.CanWrite)
                versionProperty.SetValue(param, version);

            if (updateTimeProperty != null && updateTimeProperty.CanWrite)
                updateTimeProperty.SetValue(param, DateTime.Now);

            DbSet.Update(param);
            return await Context.SaveChangesAsync();
        }
    }
}
