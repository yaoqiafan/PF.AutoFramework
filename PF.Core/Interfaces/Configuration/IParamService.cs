using PF.Core.Entities.Configuration;
using PF.Core.Entities.Identity;
using PF.Core.Events;
using PF.Core.Interfaces.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Configuration
{
    /// <summary>
    /// 参数服务接口
    /// </summary>
    public interface IParamService
    {

        Task<T?> GetParamAsync<T>(string name) where T : class;
        Task<T> GetParamAsync<T>(string name, T defaultValue) where T : class;

        // 设置参数（带用户信息）
        Task<bool> SetParamAsync<T>(string name, T value, UserInfo? userInfo = null,
            string? description = null) where T : class;

        // 批量设置参数（带用户信息）
        Task<bool> BatchSetParamsAsync<T>(Dictionary<string, T> paramValues,
            UserInfo? userInfo = null, string? description = null) where T : class;

        // 删除参数（带用户信息）
        Task<bool> DeleteParamAsync(string name, UserInfo? userInfo = null);

        Task<List<ParamInfo>> GetAllParamsAsync();
        Task<List<ParamInfo>> GetParamsByCategoryAsync<T>() where T : class, IEntity;


        Task<List<ParamInfo>> GetParamsByCategoryAsync(string typename, string category = default);


        void RegisterParamType<TEntity, TModel>() where TEntity : IEntity where TModel : class;

        // 参数更改事件
        event EventHandler<ParamChangedEventArgs> ParamChanged;
    }
}
