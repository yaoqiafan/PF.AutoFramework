using PF.Core.Entities.Configuration;
using PF.Core.Entities.Identity;
using PF.Core.Events;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Data;
using PF.Core.Interfaces.Logging;
using PF.Data.Entity.Category;
using PF.Data.Repositories;
using System.Reflection;
using System.Text.Json;

namespace PF.Services.Params
{
    /// <summary>
    /// 参数服务实现（包含更改事件）
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

        /// <summary>
        /// 触发参数更改事件
        /// </summary>
        protected virtual void OnParamChanged(ParamChangedEventArgs e)
        {
            try
            {
                // 记录参数更改日志
                LogParamChange(e);

                // 触发事件
                ParamChanged?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                _logService.Error($"Error triggering param change event for {e.ParamName}", exception: ex);
            }
        }

        /// <summary>
        /// 记录参数更改日志
        /// </summary>
        private void LogParamChange(ParamChangedEventArgs e)
        {
            try
            {
                string oldValueStr = e.OldValue != null ? JsonSerializer.Serialize(e.OldValue) : "null";
                string newValueStr = JsonSerializer.Serialize(e.NewValue);

                string logMessage = $"参数 '{e.ParamName}' 已更改。" +
                                   $"\n用户: {e.UserInfo.UserName}" +
                                   $"\n用户ID: {e.UserInfo.UserId}" +
                                   $"\n分类: {e.Category}" +
                                   $"\n旧值: {oldValueStr}" +
                                   $"\n新值: {newValueStr}" +
                                   $"\n时间: {e.ChangeTime:yyyy-MM-dd HH:mm:ss}";

                // 使用日志服务记录
                _logService.Info(logMessage, "ParamChange");
            }
            catch (Exception ex)
            {
                _logService.Error($"Error logging param change for {e.ParamName}", exception: ex);
            }
        }

        /// <summary>
        /// 设置参数（带用户信息）
        /// </summary>
        public async Task<bool> SetParamAsync<T>(string name, T value,
            UserInfo? userInfo = null, string? description = null) where T : class
        {
            using var scope = _containerProvider.CreateScope();
            try
            {
                var entityType = DetermineEntityType<T>();

                // 创建仓储实例
                var repository = CreateRepository(scope, entityType);
                if (repository == null)
                    throw new InvalidOperationException($"Repository for type {entityType.Name} not found");

                var existing = await repository.GetByNameAsync(name);
                var jsonValue = JsonSerializer.Serialize(value);
                var category = GetCategoryFromType(typeof(T));
                object? oldValue = null;

                // 获取旧值（用于日志记录）
                if (existing != null)
                {
                    try
                    {
                        oldValue = JsonSerializer.Deserialize(existing.JsonValue, typeof(T));
                    }
                    catch (Exception ex)
                    {
                        _logService.Warn($"Failed to deserialize old value for param {name}", exception: ex);
                    }
                }

                if (existing != null)
                {
                    // 更新现有参数
                    existing.JsonValue = jsonValue;
                    existing.Description = description ?? existing.Description;
                    existing.TypeFullName = typeof(T).FullName ?? typeof(T).Name;
                    existing.UpdateTime = DateTime.Now;
                    existing.Version++;

                    await repository.UpdateAsync(existing);
                }
                else
                {
                    // 创建新参数
                    var param = Activator.CreateInstance(entityType);
                    if (param == null) return false;

                    // 设置基本属性
                    var type = param.GetType();
                    var idProp = type.GetProperty("ID");
                    var nameProp = type.GetProperty("Name");
                    var descProp = type.GetProperty("Description");
                    var jsonProp = type.GetProperty("JsonValue");
                    var typeProp = type.GetProperty("TypeFullName");
                    var categoryProp = type.GetProperty("Category");
                    var createProp = type.GetProperty("CreateTime");
                    var updateProp = type.GetProperty("UpdateTime");
                    var versionProp = type.GetProperty("Version");

                    if (idProp != null && idProp.CanWrite)
                        idProp.SetValue(param, Guid.NewGuid().ToString());

                    if (nameProp != null && nameProp.CanWrite)
                        nameProp.SetValue(param, name);

                    if (descProp != null && descProp.CanWrite)
                        descProp.SetValue(param, description ?? string.Empty);

                    if (jsonProp != null && jsonProp.CanWrite)
                        jsonProp.SetValue(param, jsonValue);

                    if (typeProp != null && typeProp.CanWrite)
                        typeProp.SetValue(param, typeof(T).FullName ?? typeof(T).Name);

                    if (categoryProp != null && categoryProp.CanWrite)
                        categoryProp.SetValue(param, category);

                    var now = DateTime.Now;
                    if (createProp != null && createProp.CanWrite)
                        createProp.SetValue(param, now);

                    if (updateProp != null && updateProp.CanWrite)
                        updateProp.SetValue(param, now);

                    if (versionProp != null && versionProp.CanWrite)
                        versionProp.SetValue(param, 1);

                    await repository.AddAsync((dynamic)param);
                }

                await repository.SaveChangesAsync();

                // 触发参数更改事件
                OnParamChanged(new ParamChangedEventArgs(
                    category: category,
                    paramName: name,
                    newValue: value,
                    oldValue: oldValue,
                    userInfo: userInfo
                ));

                return true;
            }
            catch (Exception ex)
            {
                _logService.Error($"Error setting param {name} for type {typeof(T).Name}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 批量设置参数（带用户信息）
        /// </summary>
        public async Task<bool> BatchSetParamsAsync<T>(Dictionary<string, T> paramValues,
            UserInfo? userInfo = null, string? description = null) where T : class
        {
            if (paramValues == null || paramValues.Count == 0)
                return true;

            try
            {
                var category = GetCategoryFromType(typeof(T));
                var userToUse = userInfo ?? UserInfo.SystemUser;

                foreach (var kvp in paramValues)
                {
                    var result = await SetParamAsync(kvp.Key, kvp.Value, userToUse, description);
                    if (!result)
                    {
                        _logService.Warn($"Failed to set param {kvp.Key} in batch operation");
                    }
                }

                // 记录批量操作完成日志
                if (paramValues.Count > 0)
                {
                    _logService.Info($"批量设置 {paramValues.Count} 个参数完成。用户: {userToUse.UserName}, 分类: {category}", "ParamChange");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logService.Error("Error in batch setting params", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 删除参数（带用户信息）- 物理删除
        /// </summary>
        public async Task<bool> DeleteParamAsync(string name, UserInfo? userInfo = null)
        {
            using var scope = _containerProvider.CreateScope();
            try
            {
                foreach (var mapping in _paramTypeMapping)
                {
                    var repository = CreateRepository(scope, mapping.Value);
                    if (repository != null)
                    {
                        var param = await repository.GetByNameAsync(name);
                        if (param != null)
                        {
                            // 物理删除操作
                            await repository.RemoveAsync(param);
                            await repository.SaveChangesAsync();

                            // 触发参数删除事件
                            object? oldValue = null;
                            try
                            {
                                var jsonProp = param.GetType().GetProperty("JsonValue");
                                var typeProp = param.GetType().GetProperty("TypeFullName");

                                if (jsonProp != null && typeProp != null)
                                {
                                    var jsonValue = jsonProp.GetValue(param) as string;
                                    var typeName = typeProp.GetValue(param) as string;

                                    if (!string.IsNullOrEmpty(jsonValue) && !string.IsNullOrEmpty(typeName))
                                    {
                                        var type = Type.GetType(typeName);
                                        if (type != null)
                                        {
                                            oldValue = JsonSerializer.Deserialize(jsonValue, type);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logService.Warn($"Failed to deserialize old value for param {name}", exception: ex);
                            }

                            OnParamChanged(new ParamChangedEventArgs(
                                category: GetPropertyValue<string>(param, "Category") ?? "Unknown",
                                paramName: name,
                                newValue: new { Action = "Deleted", Time = DateTime.Now },
                                oldValue: oldValue,
                                userInfo: userInfo
                            ));

                            return true;
                        }
                    }
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
        /// 根据参数名获取参数
        /// </summary>
        public async Task<T?> GetParamAsync<T>(string name) where T : class
        {
            using var scope = _containerProvider.CreateScope();
            try
            {
                var entityType = DetermineEntityType<T>();
                var repository = CreateRepository(scope, entityType);

                if (repository == null)
                    throw new InvalidOperationException($"Repository for type {entityType.Name} not found");

                var param = await repository.GetByNameAsync(name);
                if (param == null) return default;

                var jsonProp = param.GetType().GetProperty("JsonValue");
                if (jsonProp == null) return default;

                var jsonValue = jsonProp.GetValue(param) as string;
                if (string.IsNullOrEmpty(jsonValue)) return default;

                var strres = JsonSerializer.Deserialize<T>(jsonValue);
                return strres;
            }
            catch (Exception ex)
            {
                _logService.Error($"Error getting param {name} for type {typeof(T).Name}", exception: ex);
                return default;
            }
        }

        /// <summary>
        /// 获取参数，如果不存在则返回默认值
        /// </summary>
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
                var repository = CreateRepository(scope, mapping.Value);
                if (repository != null)
                {
                    var paramsList = await repository.GetAllAsync();
                    foreach (var param in paramsList)
                    {
                        result.Add(new ParamInfo
                        {
                            Id = GetPropertyValue<string>(param, "ID") ?? string.Empty,
                            Name = GetPropertyValue<string>(param, "Name") ?? string.Empty,
                            Description = GetPropertyValue<string>(param, "Description") ?? string.Empty,
                            TypeName = GetPropertyValue<string>(param, "TypeFullName") ?? string.Empty,
                            Category = GetPropertyValue<string>(param, "Category") ?? string.Empty,
                            UpdateTime = GetPropertyValue<DateTime>(param, "UpdateTime")
                        });
                    }
                }
            }

            return result.OrderBy(p => p.Category).ThenBy(p => p.Name).ToList();
        }

        /// <summary>
        /// 根据分类获取参数
        /// </summary>
        public async Task<List<ParamInfo>> GetParamsByCategoryAsync<T>() where T : class,IEntity
        {
            var result = new List<ParamInfo>();

            using var scope = _containerProvider.CreateScope();
            var entityType = DetermineEntityType<T>();
            var repository = CreateRepository(scope, entityType);
            if (repository != null)
            {
                var paramsList = await repository.GetAllAsync();
                foreach (var param in paramsList)
                {
                    result.Add(new ParamInfo
                    {
                        Id = GetPropertyValue<string>(param, "ID") ?? string.Empty,
                        Name = GetPropertyValue<string>(param, "Name") ?? string.Empty,
                        Description = GetPropertyValue<string>(param, "Description") ?? string.Empty,
                        TypeName = GetPropertyValue<string>(param, "TypeFullName") ?? string.Empty,
                        Category = GetPropertyValue<string>(param, "Category") ?? string.Empty,
                        Value = GetPropertyValue<string>(param, "JsonValue") ?? string.Empty,
                        UpdateTime = GetPropertyValue<DateTime>(param, "UpdateTime")
                    });
                }
            }


            return result.OrderBy(p => p.Name).ToList();
        }

        


        /// <summary>
        /// 根据分类获取参数
        /// </summary>
        public async Task<List<ParamInfo>> GetParamsByCategoryAsync(string typename,string category ="")
        {
            var result = new List<ParamInfo>();

            using var scope = _containerProvider.CreateScope();
            var entityType = DetermineEntityType(typename);
            var repository = CreateRepository(scope, entityType);
            if (repository != null)
            {
                if (category != null)
                {
                    if (category=="全部")
                    {
                        var paramsList = await repository.GetAllAsync();
                        foreach (var param in paramsList)
                        {
                            result.Add(new ParamInfo
                            {
                                Id = GetPropertyValue<string>(param, "ID") ?? string.Empty,
                                Name = GetPropertyValue<string>(param, "Name") ?? string.Empty,
                                Description = GetPropertyValue<string>(param, "Description") ?? string.Empty,
                                TypeName = GetPropertyValue<string>(param, "TypeFullName") ?? string.Empty,
                                Category = GetPropertyValue<string>(param, "Category") ?? string.Empty,
                                Value = GetPropertyValue<string>(param, "JsonValue") ?? string.Empty,
                                UpdateTime = GetPropertyValue<DateTime>(param, "UpdateTime")
                            });
                        }
                    }
                    else
                    {
                        var paramsList = await repository.GetByCategoryAsync(category);
                        foreach (var param in paramsList)
                        {
                            result.Add(new ParamInfo
                            {
                                Id = GetPropertyValue<string>(param, "ID") ?? string.Empty,
                                Name = GetPropertyValue<string>(param, "Name") ?? string.Empty,
                                Description = GetPropertyValue<string>(param, "Description") ?? string.Empty,
                                TypeName = GetPropertyValue<string>(param, "TypeFullName") ?? string.Empty,
                                Category = GetPropertyValue<string>(param, "Category") ?? string.Empty,
                                Value = GetPropertyValue<string>(param, "JsonValue") ?? string.Empty,
                                UpdateTime = GetPropertyValue<DateTime>(param, "UpdateTime")
                            });
                        }
                    }

                  
                }
                else 
                {
                    var paramsList = await repository.GetAllAsync();
                    foreach (var param in paramsList)
                    {
                        result.Add(new ParamInfo
                        {
                            Id = GetPropertyValue<string>(param, "ID") ?? string.Empty,
                            Name = GetPropertyValue<string>(param, "Name") ?? string.Empty,
                            Description = GetPropertyValue<string>(param, "Description") ?? string.Empty,
                            TypeName = GetPropertyValue<string>(param, "TypeFullName") ?? string.Empty,
                            Category = GetPropertyValue<string>(param, "Category") ?? string.Empty,
                            Value = GetPropertyValue<string>(param, "JsonValue") ?? string.Empty,
                            UpdateTime = GetPropertyValue<DateTime>(param, "UpdateTime")
                        });
                    }
                }
            }


            return result.OrderBy(p => p.Name).ToList();
        }

        /// <summary>
        /// 创建仓储实例
        /// </summary>
        private dynamic? CreateRepository(IScopedProvider scope, Type entityType)
        {
            try
            {
                // 获取 DbContext
                var dbContext = scope.Resolve<Microsoft.EntityFrameworkCore.DbContext>();
                if (dbContext == null)
                    throw new InvalidOperationException("DbContext not found in service container");

                // 创建仓储类型
                var repositoryType = typeof(ParamRepository<>).MakeGenericType(entityType);

                // 创建仓储实例
                var repository = Activator.CreateInstance(repositoryType, dbContext);
                return repository;
            }
            catch (Exception ex)
            {
                _logService.Error($"Error creating repository for type {entityType.Name}", exception: ex);
                return null;
            }
        }

        /// <summary>
        /// 确定实体类型
        /// </summary>
        private Type DetermineEntityType<T>() where T : class
        {
            var type = typeof(T);
            return _paramTypeMapping.TryGetValue(type, out var entityType)
                ? entityType
                : typeof(CommonParam); // 默认使用通用参数表
        }

        /// <summary>
        /// 确定实体类型
        /// </summary>
        private Type DetermineEntityType(string typename) 
        {
            var type = GetTypeFromAnyAssembly(typename);
            return _paramTypeMapping.TryGetValue(type, out var entityType)
                ? entityType
                : typeof(CommonParam); // 默认使用通用参数表
        }


        /// <summary>
        /// 根据类型获取分类
        /// </summary>
        private string GetCategoryFromType(Type type)
        {
            return type.Name switch
            {
                nameof(UserLoginParam) => "UserLogin",
                nameof(SystemConfigParam) => "SystemConfig",
                _ => "Common"
            };
        }

        /// <summary>
        /// 注册新的参数类型（用于扩展）
        /// </summary>
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

        /// <summary>
        /// 获取属性值辅助方法
        /// </summary>
        private TValue? GetPropertyValue<TValue>(object obj, string propertyName)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName);
                if (prop != null && prop.CanRead)
                {
                    var value = prop.GetValue(obj);
                    if (value is TValue typedValue)
                    {
                        return typedValue;
                    }
                }
            }
            catch
            {
                // 忽略获取属性值时的错误
            }
            return default;
        }



        public static Type GetTypeFromAnyAssembly(string typeName)
        {
            // 尝试直接获取
            Type type = Type.GetType(typeName);
            if (type != null) return type;

            // 在当前域所有程序集中查找
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            // 尝试加载程序集限定名
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