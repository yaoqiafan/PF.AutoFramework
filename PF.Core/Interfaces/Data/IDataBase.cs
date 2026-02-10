using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Data
{
    public interface IDataBase
    {
        /// <summary>
        /// 根据枚举获取对应的仓储
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="dbSet">表枚举</param>
        /// <returns>对应的仓储</returns>
        IGenericRepository<T> GetRepository<T>(string dbSet) where T : class, IEntity, new();


        Task<bool> InitializationDataBase();

        /// <summary>
        /// 保存所有更改
        /// </summary>
        Task<int> SaveChangesAsync();
    }
}
