using PF.Core.Interfaces.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Configuration
{
    /// <summary>
    /// 参数仓储接口
    /// </summary>
    public interface IParamRepository<T> : IGenericRepository<T> where T : class, IEntity, new()
    {
        /// <summary>根据名称获取参数</summary>
        Task<T?> GetByNameAsync(string name);
        /// <summary>根据分类获取参数列表</summary>
        Task<List<T>> GetByCategoryAsync(string category);
        /// <summary>判断参数是否存在</summary>
        Task<bool> ExistsAsync(string name);
        /// <summary>更新参数版本</summary>
        Task<int> UpdateVersionAsync(string id, int version);
    }
}
