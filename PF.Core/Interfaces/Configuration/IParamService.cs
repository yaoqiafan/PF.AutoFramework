using PF.Core.Entities.Configuration;
using PF.Core.Entities.Identity;
using PF.Core.Events;
using PF.Core.Interfaces.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Configuration
{
    /// <summary>
    /// 参数服务接口
    /// </summary>
    public interface IParamService
    {
        /// <summary>根据名称获取参数值</summary>
        Task<T?> GetParamAsync<T>(string name);
        /// <summary>根据名称获取参数值，带默认值</summary>
        Task<T> GetParamAsync<T>(string name, T defaultValue);

        /// <summary>设置参数值（泛型）</summary>
        Task<bool> SetParamAsync<T>(string name, T value, UserInfo? userInfo = null,
            string? description = null) where T : class;
        /// <summary>设置参数值（按类型名）</summary>
        Task<bool> SetParamAsync(string typeName, string name, object value, UserInfo? userInfo = null, string? description = null);
        /// <summary>批量设置参数值</summary>
        Task<bool> BatchSetParamsAsync<T>(Dictionary<string, T> paramValues,
            UserInfo? userInfo = null, string? description = null) where T : class;

        /// <summary>删除参数（泛型）</summary>
        Task<bool> DeleteParamAsync<T>(string name, UserInfo? userInfo = null) where T : class;
        /// <summary>删除参数（按类型名）</summary>
        Task<bool> DeleteParamAsync(string typeName, string name, UserInfo? userInfo = null);
        /// <summary>获取所有参数</summary>
        Task<List<ParamInfo>> GetAllParamsAsync();
        /// <summary>根据泛型分类获取参数列表</summary>
        Task<List<ParamInfo>> GetParamsByCategoryAsync<T>() where T : class, IEntity;

        /// <summary>根据类型名和分类获取参数列表</summary>
        Task<List<ParamInfo>> GetParamsByCategoryAsync(string typename, string category = default);

        /// <summary>注册参数类型映射</summary>
        void RegisterParamType<TEntity, TModel>() where TEntity : IEntity where TModel : class;

        /// <summary>参数变更事件</summary>
        event EventHandler<ParamChangedEventArgs> ParamChanged;
    }
}
