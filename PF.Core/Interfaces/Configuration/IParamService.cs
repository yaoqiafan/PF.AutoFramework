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
        Task<T?> GetParamAsync<T>(string name) where T : class;
        Task<T> GetParamAsync<T>(string name, T defaultValue) where T : class;

        // 设置参数（带用户信息）
        Task<bool> SetParamAsync<T>(string name, T value, UserInfo? userInfo = null,
            string? description = null) where T : class;
        // 增加按实体类型名(typeName)保存参数的重载
        Task<bool> SetParamAsync(string typeName, string name, object value, UserInfo? userInfo = null, string? description = null);
        // 批量设置参数（带用户信息）
        Task<bool> BatchSetParamsAsync<T>(Dictionary<string, T> paramValues,
            UserInfo? userInfo = null, string? description = null) where T : class;

        // 重点修改：改为泛型，明确指定删除的参数类型
        Task<bool> DeleteParamAsync<T>(string name, UserInfo? userInfo = null) where T : class;
        Task<bool> DeleteParamAsync(string typeName, string name, UserInfo? userInfo = null);
        Task<List<ParamInfo>> GetAllParamsAsync();
        Task<List<ParamInfo>> GetParamsByCategoryAsync<T>() where T : class, IEntity;

        Task<List<ParamInfo>> GetParamsByCategoryAsync(string typename, string category = default);

        void RegisterParamType<TEntity, TModel>() where TEntity : IEntity where TModel : class;

        // 参数更改事件
        event EventHandler<ParamChangedEventArgs> ParamChanged;
    }
}