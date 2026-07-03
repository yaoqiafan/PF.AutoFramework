using PF.Core.Entities.Communication;
using PF.Core.Models;

namespace PF.Core.Interfaces.Communication;

/// <summary>
/// 通讯实例管理服务（结构对齐 <see cref="PF.Core.Interfaces.Device.Hardware.IHardwareManagerService"/>，
/// 但通讯实例之间没有硬件那种父子拓扑依赖，加载流程更简单）。
///
/// 生命周期：
///   ① App 启动时调用 RegisterParamType&lt;CommunicationParam, CommunicationConfig&gt;()
///   ② 调用 RegisterFactory(...) 注册所有实现类的工厂
///   ③ 首次运行时通过 ImportConfigsAsync(defaultConfigs) 写入初始配置
///   ④ 调用 LoadAndInitializeAsync() → 从数据库加载配置 → 实例化 → StartAsync
///      —— 该调用必须先于 IHardwareManagerService.LoadAndInitializeAsync()，
///         因为硬件工厂可能需要通过 GetCommunication&lt;T&gt; 引用已启动的通讯实例
///   ⑤ 其他模块通过 ActiveCommunications / GetCommunication&lt;T&gt; 取用实例引用
///   ⑥ ReloadAllAsync() 支持热重载
/// </summary>
public interface ICommunicationManagerService
{
    /// <summary>当前已激活的通讯实例</summary>
    IEnumerable<ICommunication> ActiveCommunications { get; }

    /// <summary>通讯实例新增时触发</summary>
    event EventHandler<ICommunication>? CommunicationAdded;

    /// <summary>通讯实例移除时触发</summary>
    event EventHandler<string>? CommunicationRemoved;

    /// <summary>注册实现类工厂，key 为 <see cref="CommunicationConfig.ImplementationClassName"/></summary>
    void RegisterFactory(string implementationClassName, Func<CommunicationConfig, ICommunication> factory);

    /// <summary>批量导入配置（首次运行注入默认值场景），upsert 语义</summary>
    Task ImportConfigsAsync(IEnumerable<CommunicationConfig> configs);

    /// <summary>从数据库加载配置，实例化所有已启用的通讯实例并调用 StartAsync</summary>
    Task LoadAndInitializeAsync(IProgress<SplashProgressPayload>? progress = null);

    /// <summary>停止并释放所有活跃实例，重新从数据库加载并启动</summary>
    Task ReloadAllAsync();

    /// <summary>获取指定实例的配置</summary>
    CommunicationConfig? GetConfig(string instanceId);

    /// <summary>保存单条配置：更新内存缓存并写入数据库</summary>
    Task SaveConfigAsync(CommunicationConfig config);

    /// <summary>删除单条配置</summary>
    Task DeleteConfigAsync(string instanceId);

    /// <summary>
    /// 按 InstanceId 查找已激活的通讯实例并转换为指定类型。
    /// T 通常是具体的功能接口（如 IClient/IServer/IFileTransferChannel），不是 ICommunication 本身——
    /// ICommunication 只是调试树用的公共外观，调用方真正要用的是实例实现的功能接口。
    /// 找不到或类型不匹配直接抛 <see cref="InvalidOperationException"/>——
    /// 硬件工厂依赖某个通讯实例存在属于配置正确性问题，应在启动时立刻暴露。
    /// </summary>
    T GetCommunication<T>(string instanceId) where T : class;
}
