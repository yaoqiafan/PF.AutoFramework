using Microsoft.EntityFrameworkCore;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Data;
using PF.Data.Entity; // 引入 ParamEntity 以便进行类型转换
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PF.Data.Repositories
{
    /// <summary>
    /// 参数仓储实现（无手动表达式树版）
    /// </summary>
    public class ParamRepository<T> : GenericRepository<T>, IParamRepository<T> where T : class, IEntity, new()
    {
        public ParamRepository(DbContext context) : base(context) { }

        public async Task<T?> GetByNameAsync(string name)
        {
            // 使用 EF.Property 替代手写反射 Expression，EF Core 会自动将其翻译为 SQL 查询
            return await DbSet.FirstOrDefaultAsync(p => EF.Property<string>(p, "Name") == name);
        }

        public async Task<List<T>> GetByCategoryAsync(string category)
        {
            return await DbSet.Where(p => EF.Property<string>(p, "Category") == category)
                             .OrderBy(p => EF.Property<string>(p, "Name"))
                             .ToListAsync();
        }

        public async Task<bool> ExistsAsync(string name)
        {
            return await DbSet.AnyAsync(p => EF.Property<string>(p, "Name") == name);
        }

        public async Task<int> UpdateVersionAsync(string id, int version)
        {
            var param = await DbSet.FindAsync(id);
            if (param == null) return 0;

            // 在 Data 层，实体确定继承自 ParamEntity，直接类型转换赋值即可
            if (param is ParamEntity paramEntity)
            {
                paramEntity.Version = version;
                paramEntity.UpdateTime = DateTime.Now;
            }
            else
            {
                // 作为极致的后备防卫方案：使用 EF Core 跟踪器直接修改属性值
                Context.Entry(param).Property("Version").CurrentValue = version;
                Context.Entry(param).Property("UpdateTime").CurrentValue = DateTime.Now;
            }

            DbSet.Update(param);
            return await Context.SaveChangesAsync();
        }
    }
}