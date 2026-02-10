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
        Task<T?> GetByNameAsync(string name);
        Task<List<T>> GetByCategoryAsync(string category);
        Task<bool> ExistsAsync(string name);
        Task<int> UpdateVersionAsync(string id, int version);
    }
}
