using PF.Core.Entities.Configuration;
using PF.Core.Entities.Identity;
using PF.Core.Events;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Data;
using PF.Core.Interfaces.Logging;
using PF.Data.Entity;
using PF.Data.Entity.Category;
using PF.Data.Repositories;
using System.Reflection;
using System.Text.Json;

namespace PF.Services.Params
{
    /// <summary>
    /// 参数服务实现（无反射、高性能版）
    /// </summary>
    public class ParamService : IParamService
    {
        private readonly IContainerProvider _containerProvider;
        private readonly ILogService _logService;
        private readonly Dictionary<Type, Type> _paramTypeMapping;

        // 参数更改事件
        public event EventHandler<ParamChangedEventArgs>? ParamChanged;

        public ParamService(
            IContainerProvider containerProvider,
            ILogService logService)
        {
            _containerProvider = containerProvider;
            _logService = logService;
            _paramTypeMapping = new Dictionary<Type, Type>
            {
                { typeof(CommonParam), typeof(CommonParam) },
                { typeof(UserLoginParam), typeof(UserLoginParam) },
                { typeof(SystemConfigParam), typeof(SystemConfigParam) }
            };
        }

        protected virtual void OnParamChanged(ParamChangedEventArgs e)
        {
            try
            {
                LogParamChange(e);
                ParamChanged?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                _logService.Error($"Error triggering param change event for {e.ParamName}", exception: ex);
            }
        }

        private void LogParamChange(ParamChangedEventArgs e)
        {
            try
            {
                string oldValueStr = e.OldValue != null ? JsonSerializer.Serialize(e.OldValue) : "null";
                string newValueStr = JsonSerializer.Serialize(e.NewValue);

                string logMessage = $"参数 '{e.ParamName}' 已更改。" +
                                   $"\n用户: {e.UserInfo?.UserName ?? "System"}" +
                                   $"\n用户ID: {e.UserInfo?.UserId ?? "N/A"}" +
                                   $"\n分类: {e.Category}" +
                                   $"\n旧值: {oldValueStr}" +
                                   $"\n新值: {newValueStr}" +
                                   $"\n时间: {e.ChangeTime:yyyy-MM-dd HH:mm:ss}";

                _logService.Info(logMessage, "ParamChange");
            }
            catch (Exception ex)
            {
                _logService.Error($"Error logging param change for {e.ParamName}", exception: ex);
            }
        }

        /// <summary>
        /// 设置参数（指定实体表名，支持值类型，解决 dynamic 泛型报错问题）
        /// </summary>
        public async Task<bool> SetParamAsync(string typeName, string name, object value, UserInfo? userInfo = null, string? description = null)
        {
            using var scope = _containerProvider.CreateScope();
            try
            {
                var entityType = DetermineEntityType(typeName);
                dynamic? repository = CreateRepository(scope, entityType);
                if (repository == null) return false;

                ParamEntity? existing = await repository.GetByNameAsync(name);
                var jsonValue = JsonSerializer.Serialize(value);

                string category = "Common";
                if (typeName.Contains("UserLoginParam")) category = "UserLogin";
                else if (typeName.Contains("SystemConfigParam")) category = "SystemConfig";

                object? oldValue = null;

                if (existing != null)
                {
                    // 【核心优化】：如果新旧值序列化后的 JSON 完全相同，说明值没有发生实际改变，直接跳过保存和日志
                    if (existing.JsonValue == jsonValue)
                    {
                        return true;
                    }

                    try
                    {
                        oldValue = JsonSerializer.Deserialize(existing.JsonValue, value.GetType());
                    }
                    catch (Exception ex)
                    {
                        _logService.Warn($"Failed to deserialize old value for param {name}", exception: ex);
                    }

                    existing.JsonValue = jsonValue;
                    existing.Description = description ?? existing.Description;
                    existing.TypeFullName = value.GetType().FullName ?? value.GetType().Name;
                    existing.UpdateTime = DateTime.Now;
                    existing.Version++;

                    await repository.UpdateAsync((dynamic)existing);
                }
                else
                {
                    var param = Activator.CreateInstance(entityType) as ParamEntity;
                    if (param == null) return false;

                    param.ID = Guid.NewGuid().ToString();
                    param.Name = name;
                    param.Description = description ?? string.Empty;
                    param.JsonValue = jsonValue;
                    param.TypeFullName = value.GetType().FullName ?? value.GetType().Name;
                    param.Category = category;

                    var now = DateTime.Now;
                    param.CreateTime = now;
                    param.UpdateTime = now;
                    param.Version = 1;

                    await repository.AddAsync((dynamic)param);
                }

                await repository.SaveChangesAsync();

                // 只有实际发生了数据改变才会触发该事件并记录日志
                OnParamChanged(new ParamChangedEventArgs(category, name, value, oldValue, userInfo));
                return true;
            }
            catch (Exception ex)
            {
                _logService.Error($"Error setting param {name} of type {typeName}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 设置参数（带用户信息，泛型版本）
        /// </summary>
        public async Task<bool> SetParamAsync<T>(string name, T value, UserInfo? userInfo = null, string? description = null) where T : class
        {
            using var scope = _containerProvider.CreateScope();
            try
            {
                var entityType = DetermineEntityType<T>();
                dynamic? repository = CreateRepository(scope, entityType);
                if (repository == null)
                    throw new InvalidOperationException($"Repository for type {entityType.Name} not found");

                // 利用 dynamic 调用仓储方法，但将结果强转为 ParamEntity 基类（避免反射读取属性）
                ParamEntity? existing = await repository.GetByNameAsync(name);
                var jsonValue = JsonSerializer.Serialize(value);
                var category = GetCategoryFromType(typeof(T));
                object? oldValue = null;

                if (existing != null)
                {
                    // 【核心优化】：如果新旧值序列化后的 JSON 完全相同，说明值没有发生实际改变，直接跳过保存和日志
                    if (existing.JsonValue == jsonValue)
                    {
                        return true;
                    }

                    try
                    {
                        oldValue = JsonSerializer.Deserialize(existing.JsonValue, typeof(T));
                    }
                    catch (Exception ex)
                    {
                        _logService.Warn($"Failed to deserialize old value for param {name}", exception: ex);
                    }

                    // 强类型赋值，抛弃反射
                    existing.JsonValue = jsonValue;
                    existing.Description = description ?? existing.Description;
                    existing.TypeFullName = typeof(T).FullName ?? typeof(T).Name;
                    existing.UpdateTime = DateTime.Now;
                    existing.Version++; // 建议在 Entity 中配合 [ConcurrencyCheck] 特性实现真正的乐观锁

                    await repository.UpdateAsync((dynamic)existing);
                }
                else
                {
                    // 强类型实例化与赋值，抛弃反射
                    var param = Activator.CreateInstance(entityType) as ParamEntity;
                    if (param == null) return false;

                    param.ID = Guid.NewGuid().ToString();
                    param.Name = name;
                    param.Description = description ?? string.Empty;
                    param.JsonValue = jsonValue;
                    param.TypeFullName = typeof(T).FullName ?? typeof(T).Name;
                    param.Category = category;

                    var now = DateTime.Now;
                    param.CreateTime = now;
                    param.UpdateTime = now;
                    param.Version = 1;

                    await repository.AddAsync((dynamic)param);
                }

                await repository.SaveChangesAsync();

                // 只有发生变化才会执行
                OnParamChanged(new ParamChangedEventArgs(category, name, value, oldValue, userInfo));
                return true;
            }
            catch (Exception ex)
            {
                _logService.Error($"Error setting param {name} for type {typeof(T).Name}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 批量设置参数
        /// </summary>
        public async Task<bool> BatchSetParamsAsync<T>(Dictionary<string, T> paramValues, UserInfo? userInfo = null, string? description = null) where T : class
        {
            if (paramValues == null || paramValues.Count == 0) return true;
            try
            {
                var category = GetCategoryFromType(typeof(T));
                var userToUse = userInfo ?? UserInfo.SystemUser;

                foreach (var kvp in paramValues)
                {
                    var result = await SetParamAsync(kvp.Key, kvp.Value, userToUse, description);
                    if (!result) _logService.Warn($"Failed to set param {kvp.Key} in batch operation");
                }

                _logService.Info($"批量设置 {paramValues.Count} 个参数完成。用户: {userToUse.UserName}, 分类: {category}", "ParamChange");
                return true;
            }
            catch (Exception ex)
            {
                _logService.Error("Error in batch setting params", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 删除参数（增加了泛型限制以定位具体的表）
        /// </summary>
        public async Task<bool> DeleteParamAsync<T>(string name, UserInfo? userInfo = null) where T : class
        {
            using var scope = _containerProvider.CreateScope();
            try
            {
                var entityType = DetermineEntityType<T>();
                dynamic? repository = CreateRepository(scope, entityType);
                if (repository == null) return false;

                ParamEntity? param = await repository.GetByNameAsync(name);
                if (param != null)
                {
                    await repository.RemoveAsync((dynamic)param);
                    await repository.SaveChangesAsync();

                    object? oldValue = null;
                    try
                    {
                        if (!string.IsNullOrEmpty(param.JsonValue) && !string.IsNullOrEmpty(param.TypeFullName))
                        {
                            var type = Type.GetType(param.TypeFullName);
                            if (type != null)
                            {
                                oldValue = JsonSerializer.Deserialize(param.JsonValue, type);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Warn($"Failed to deserialize old value for param {name}", exception: ex);
                    }

                    OnParamChanged(new ParamChangedEventArgs(
                        category: param.Category,
                        paramName: name,
                        newValue: new { Action = "Deleted", Time = DateTime.Now },
                        oldValue: oldValue,
                        userInfo: userInfo
                    ));

                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logService.Error($"Error deleting param {name}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 删除参数
        /// </summary>
        public async Task<bool> DeleteParamAsync(string typeName, string name, UserInfo? userInfo = null)
        {
            using var scope = _containerProvider.CreateScope();
            try
            {
                // 1. 通过字符串解析出真实的 Type
                var entityType = DetermineEntityType(typeName);

                // 2. 创建对应的仓储
                dynamic? repository = CreateRepository(scope, entityType);
                if (repository == null) return false;

                // 3. 执行精准查询并删除
                ParamEntity? param = await repository.GetByNameAsync(name);
                if (param != null)
                {
                    await repository.RemoveAsync((dynamic)param);
                    await repository.SaveChangesAsync();

                    // 4. 反序列化旧值用于日志记录
                    object? oldValue = null;
                    try
                    {
                        if (!string.IsNullOrEmpty(param.JsonValue) && !string.IsNullOrEmpty(param.TypeFullName))
                        {
                            var type = Type.GetType(param.TypeFullName);
                            if (type != null)
                            {
                                oldValue = JsonSerializer.Deserialize(param.JsonValue, type);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Warn($"Failed to deserialize old value for param {name}", exception: ex);
                    }

                    // 5. 触发事件
                    OnParamChanged(new ParamChangedEventArgs(
                        category: param.Category,
                        paramName: name,
                        newValue: new { Action = "Deleted", Time = DateTime.Now },
                        oldValue: oldValue,
                        userInfo: userInfo
                    ));

                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logService.Error($"Error deleting param {name} of type {typeName}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 根据参数名获取参数
        /// </summary>
        public async Task<T?> GetParamAsync<T>(string name) where T : class
        {
            using var scope = _containerProvider.CreateScope();
            try
            {
                var entityType = DetermineEntityType<T>();
                dynamic? repository = CreateRepository(scope, entityType);

                if (repository == null)
                    throw new InvalidOperationException($"Repository for type {entityType.Name} not found");

                ParamEntity? param = await repository.GetByNameAsync(name);
                if (param == null || string.IsNullOrEmpty(param.JsonValue)) return default;

                return JsonSerializer.Deserialize<T>(param.JsonValue);
            }
            catch (Exception ex)
            {
                _logService.Error($"Error getting param {name} for type {typeof(T).Name}", exception: ex);
                return default;
            }
        }

        public async Task<T> GetParamAsync<T>(string name, T defaultValue) where T : class
        {
            var result = await GetParamAsync<T>(name);
            return result ?? defaultValue;
        }

        /// <summary>
        /// 获取所有参数
        /// </summary>
        public async Task<List<ParamInfo>> GetAllParamsAsync()
        {
            var result = new List<ParamInfo>();
            using var scope = _containerProvider.CreateScope();

            foreach (var mapping in _paramTypeMapping)
            {
                dynamic? repository = CreateRepository(scope, mapping.Value);
                if (repository != null)
                {
                    IEnumerable<object> paramsList = await repository.GetAllAsync();
                    foreach (ParamEntity param in paramsList)
                    {
                        result.Add(MapToParamInfo(param));
                    }
                }
            }
            return result.OrderBy(p => p.Category).ThenBy(p => p.Name).ToList();
        }

        /// <summary>
        /// 根据分类获取参数 (泛型)
        /// </summary>
        public async Task<List<ParamInfo>> GetParamsByCategoryAsync<T>() where T : class, IEntity
        {
            var result = new List<ParamInfo>();
            using var scope = _containerProvider.CreateScope();
            var entityType = DetermineEntityType<T>();
            dynamic? repository = CreateRepository(scope, entityType);

            if (repository != null)
            {
                IEnumerable<object> paramsList = await repository.GetAllAsync();
                foreach (ParamEntity param in paramsList)
                {
                    result.Add(MapToParamInfo(param));
                }
            }
            return result.OrderBy(p => p.Name).ToList();
        }

        /// <summary>
        /// 根据分类获取参数 (字符串重载)
        /// </summary>
        public async Task<List<ParamInfo>> GetParamsByCategoryAsync(string typename, string category = "")
        {
            var result = new List<ParamInfo>();
            using var scope = _containerProvider.CreateScope();
            var entityType = DetermineEntityType(typename);
            dynamic? repository = CreateRepository(scope, entityType);

            if (repository != null)
            {
                IEnumerable<object> paramsList;
                if (!string.IsNullOrEmpty(category) && category != "全部")
                {
                    paramsList = await repository.GetByCategoryAsync(category);
                }
                else
                {
                    paramsList = await repository.GetAllAsync();
                }

                foreach (ParamEntity param in paramsList)
                {
                    result.Add(MapToParamInfo(param));
                }
            }
            return result.OrderBy(p => p.Name).ToList();
        }

        // --- 私有辅助方法 ---

        /// <summary>
        /// 将 ParamEntity 映射为 ParamInfo 的强类型辅助方法
        /// </summary>
        private ParamInfo MapToParamInfo(ParamEntity param)
        {
            return new ParamInfo
            {
                Id = param.ID,
                Name = param.Name,
                Description = param.Description,
                TypeName = param.TypeFullName,
                Category = param.Category,
                Value = param.JsonValue,
                UpdateTime = param.UpdateTime
            };
        }

        private dynamic? CreateRepository(IScopedProvider scope, Type entityType)
        {
            try
            {
                var dbContext = scope.Resolve<Microsoft.EntityFrameworkCore.DbContext>();
                if (dbContext == null) return null;

                var repositoryType = typeof(ParamRepository<>).MakeGenericType(entityType);
                return Activator.CreateInstance(repositoryType, dbContext);
            }
            catch (Exception ex)
            {
                _logService.Error($"Error creating repository for type {entityType.Name}", exception: ex);
                return null;
            }
        }

        private Type DetermineEntityType<T>() where T : class
        {
            var type = typeof(T);

            // 修复潜在Bug：如果传入的已经是 Entity 类型，直接返回
            if (typeof(ParamEntity).IsAssignableFrom(type))
                return type;

            return _paramTypeMapping.TryGetValue(type, out var entityType)
                ? entityType
                : typeof(CommonParam);
        }

        private Type DetermineEntityType(string typename)
        {
            var type = GetTypeFromAnyAssembly(typename);
            if (type == null) return typeof(CommonParam);

            if (typeof(ParamEntity).IsAssignableFrom(type))
                return type;

            return _paramTypeMapping.TryGetValue(type, out var entityType)
                ? entityType
                : typeof(CommonParam);
        }

        private string GetCategoryFromType(Type type)
        {
            return type.Name switch
            {
                nameof(UserLoginParam) => "UserLogin",
                nameof(SystemConfigParam) => "SystemConfig",
                _ => "Common"
            };
        }

        public void RegisterParamType<TEntity, TModel>()
            where TEntity : IEntity
            where TModel : class
        {
            var entityType = typeof(TEntity);
            var modelType = typeof(TModel);

            if (!_paramTypeMapping.ContainsKey(modelType))
            {
                _paramTypeMapping[modelType] = entityType;
            }
        }

        public static Type? GetTypeFromAnyAssembly(string typeName)
        {
            Type? type = Type.GetType(typeName);
            if (type != null) return type;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            int commaIndex = typeName.LastIndexOf(',');
            if (commaIndex > 0)
            {
                string assemblyName = typeName.Substring(commaIndex + 1).Trim();
                string shortTypeName = typeName.Substring(0, commaIndex).Trim();
                try
                {
                    Assembly assembly = Assembly.Load(assemblyName);
                    return assembly.GetType(shortTypeName);
                }
                catch { }
            }
            return null;
        }
    }
}