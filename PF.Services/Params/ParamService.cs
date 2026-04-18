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
    /// 参数服务实现（高性能版）
    /// 核心设计：通过显式的类型映射字典替代高频的反射扫描，
    /// 结合 JSON 序列化比较机制，减少不必要的数据库写操作。
    /// </summary>
    public class ParamService : IParamService
    {
        /// <summary>IoC 容器提供者，用于在方法内部创建生命周期作用域（Scope）</summary>
        private readonly IContainerProvider _containerProvider;

        /// <summary>日志服务</summary>
        private readonly ILogService _logService;

        /// <summary>领域模型类型与数据库实体类型（Entity）的映射字典，用于避免反射开销</summary>
        private readonly Dictionary<Type, Type> _paramTypeMapping;

        /// <summary>
        /// 当任何参数成功保存或删除，且实际值发生改变时触发的全局事件。
        /// 可用于通知 UI 刷新或触发关联的硬件动作。
        /// </summary>
        public event EventHandler<ParamChangedEventArgs>? ParamChanged;

        /// <summary>
        /// 实例化 <see cref="ParamService"/>
        /// </summary>
        /// <param name="containerProvider">容器提供者</param>
        /// <param name="logService">日志服务</param>
        public ParamService(
            IContainerProvider containerProvider,
            ILogService logService)
        {
            _containerProvider = containerProvider;
            _logService = logService;

            // 初始化默认的类型映射关系
            _paramTypeMapping = new Dictionary<Type, Type>
            {
                { typeof(UserLoginParam), typeof(UserLoginParam) },
                { typeof(SystemConfigParam), typeof(SystemConfigParam) }
            };
        }

        /// <summary>
        /// 内部触发 <see cref="ParamChanged"/> 事件的受保护方法。
        /// 包含了对事件执行异常的兜底捕获，防止单个订阅者的异常导致整个服务崩溃。
        /// </summary>
        /// <param name="e">参数更改事件数据</param>
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

        /// <summary>
        /// 记录参数变更的详细日志，包括旧值、新值及操作人信息。
        /// </summary>
        /// <param name="e">参数更改事件数据</param>
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
        /// 动态设置参数（基于字符串类型名称，支持匿名或动态类型值）。
        /// 自动解决并规避了纯 dynamic 泛型传入仓储时引发的解析报错问题。
        /// </summary>
        /// <param name="typeName">模型或实体的全类型名称</param>
        /// <param name="name">参数键名（唯一标识）</param>
        /// <param name="value">要保存的参数值对象</param>
        /// <param name="userInfo">操作用户信息（用于日志审计）</param>
        /// <param name="description">参数的备注描述</param>
        /// <returns>操作成功返回 true，否则返回 false</returns>
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

                // 根据类型名称进行简单的分类推断
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
        /// 泛型版本：设置参数（带用户信息追踪）。推荐优先使用此方法以保证类型安全。
        /// </summary>
        /// <typeparam name="T">参数模型类型</typeparam>
        /// <param name="name">参数键名（唯一标识）</param>
        /// <param name="value">要保存的强类型参数实例</param>
        /// <param name="userInfo">操作用户信息</param>
        /// <param name="description">参数备注描述</param>
        /// <returns>操作成功返回 true，否则返回 false</returns>
        public async Task<bool> SetParamAsync<T>(string name, T value, UserInfo? userInfo = null, string? description = null) where T : class
        {
            using var scope = _containerProvider.CreateScope();
            try
            {
                var entityType = DetermineEntityType<T>();
                dynamic? repository = CreateRepository(scope, entityType);
                if (repository == null)
                    throw new InvalidOperationException($"Repository for type {entityType.Name} not found");

                // 利用 dynamic 调用仓储方法，但将结果强转为 ParamEntity 基类（避免反射读取属性的性能损耗）
                ParamEntity? existing = await repository.GetByNameAsync(name);
                var jsonValue = JsonSerializer.Serialize(value);
                var category = GetCategoryFromType(typeof(T));
                object? oldValue = null;

                if (existing != null)
                {
                    // 【核心优化】：如果新旧值序列化后的 JSON 完全相同，直接跳过保存
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

                // 触发事件
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
        /// 批量更新/设置多项参数（复用单项更新的业务逻辑）
        /// </summary>
        /// <typeparam name="T">参数模型类型</typeparam>
        /// <param name="paramValues">字典：键为参数名，值为参数实例</param>
        /// <param name="userInfo">操作用户信息</param>
        /// <param name="description">统一备注描述</param>
        /// <returns>全部或部分成功返回 true，发生灾难性异常返回 false</returns>
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
        /// 删除指定名称的泛型参数（增加了泛型限制以准确定位具体的数据库表）
        /// </summary>
        /// <typeparam name="T">参数模型类型</typeparam>
        /// <param name="name">待删除的参数名称</param>
        /// <param name="userInfo">操作用户信息</param>
        /// <returns>删除成功返回 true，未找到或失败返回 false</returns>
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

                    // 触发参数删除事件（利用匿名对象模拟新值，告知已被删除）
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
        /// 基于字符串类型名称删除指定参数
        /// </summary>
        /// <param name="typeName">模型或实体的全类型名称</param>
        /// <param name="name">待删除的参数名称</param>
        /// <param name="userInfo">操作用户信息</param>
        /// <returns>删除成功返回 true，未找到或失败返回 false</returns>
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
        /// 根据参数名和目标类型反序列化并获取参数对象
        /// </summary>
        /// <typeparam name="T">期望反序列化的目标类型</typeparam>
        /// <param name="name">参数键名</param>
        /// <returns>找到并成功反序列化则返回实例，否则返回默认值(null)</returns>
        public async Task<T?> GetParamAsync<T>(string name)
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

        /// <summary>
        /// 获取参数，若不存在则返回用户指定的默认回退值
        /// </summary>
        /// <typeparam name="T">期望反序列化的目标类型</typeparam>
        /// <param name="name">参数键名</param>
        /// <param name="defaultValue">未命中时的回退默认值</param>
        /// <returns>找到的数据或传入的 defaultValue</returns>
        public async Task<T> GetParamAsync<T>(string name, T defaultValue)
        {
            var result = await GetParamAsync<T>(name);
            return result ?? defaultValue;
        }

        /// <summary>
        /// 跨类型/跨表检索系统中的所有参数记录
        /// </summary>
        /// <returns>统一映射后的 ParamInfo 信息列表，按分类和名称排序</returns>
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
        /// 根据实体泛型类别获取特定表内的所有参数
        /// </summary>
        /// <typeparam name="T">必须是实现了 <see cref="IEntity"/> 的实体类</typeparam>
        /// <returns>属于该实体的参数列表，按名称排序</returns>
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
        /// 根据指定的字符串类别名称查询参数 (多用于 UI 层下拉框联动查询)
        /// </summary>
        /// <param name="typename">模型或实体类名</param>
        /// <param name="category">具体的业务分类名 (传空或"全部"则等效于查全表)</param>
        /// <returns>符合要求的参数列表</returns>
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
        /// 将底层的 Entity 转换为跨层传输的 DTO(ParamInfo)
        /// </summary>
        /// <param name="param">数据库提取的实体模型</param>
        /// <returns>给 UI 绑定的 DTO 模型</returns>
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

        /// <summary>
        /// 利用反射动态创建对应实体的泛型仓储 (Repository)。
        /// 返回 dynamic 的目的是为了让调用方能直接使用 await repository.GetByNameAsync() 等方法，
        /// 而无需在此处书写冗长复杂的 MakeGenericMethod 反射调用。
        /// </summary>
        /// <param name="scope">当前的作用域上下文</param>
        /// <param name="entityType">要构建的实体类型 Type</param>
        /// <returns>实例化的泛型仓储对象，如果出错返回 null</returns>
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

        /// <summary>
        /// 基于泛型 T，在缓存字典中确定它对应的持久化实体类型(Entity)
        /// </summary>
        private Type DetermineEntityType<T>()
        {
            var type = typeof(T);

            // 防御性编程：如果传入的已经是直接继承自 ParamEntity 的实体类型，直接返回自身
            if (typeof(ParamEntity).IsAssignableFrom(type))
                return type;

            return _paramTypeMapping.TryGetValue(type, out var entityType)
                ? entityType
                : typeof(SystemConfigParam); // 默认兜底映射到系统配置表
        }

        /// <summary>
        /// 基于字符串类型名，在缓存字典中确定它对应的持久化实体类型
        /// </summary>
        private Type DetermineEntityType(string typename)
        {
            var type = GetTypeFromAnyAssembly(typename);
            if (type == null) return typeof(SystemConfigParam);

            if (typeof(ParamEntity).IsAssignableFrom(type))
                return type;

            return _paramTypeMapping.TryGetValue(type, out var entityType)
                ? entityType
                : typeof(SystemConfigParam);
        }

        /// <summary>
        /// 简易归类器：根据类型的命名，为其赋予默认的分组(Category)标签
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
        /// 动态向服务注册领域模型(Model)与数据库实体(Entity)的映射关系。
        /// 这样在做读写操作时，能根据传入的业务 Model 自动路由至指定的数据库表。
        /// </summary>
        /// <typeparam name="TEntity">必须实现了 IEntity 的数据库实体类</typeparam>
        /// <typeparam name="TModel">业务侧使用的强类型数据模型类</typeparam>
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
        /// 暴力全量扫描程序集：从当前的 AppDomain 中跨 Assembly 搜索指定的类型名称
        /// </summary>
        /// <param name="typeName">类的全名或短名</param>
        /// <returns>成功匹配的 Type 对象，否则返回 null</returns>
        public static Type? GetTypeFromAnyAssembly(string typeName)
        {
            // 优先尝试标准方法解析
            Type? type = Type.GetType(typeName);
            if (type != null) return type;

            // 在所有已加载的程序集中查找
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            // 处理附带强名称/程序集版本后缀的字符串 (例如: "MyNamespace.MyClass, MyAssembly")
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
                catch { } // 吞掉加载异常，允许返回 null
            }
            return null;
        }
    }
}