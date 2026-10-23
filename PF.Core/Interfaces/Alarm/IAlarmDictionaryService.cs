using PF.Core.Enums;
using PF.Core.Models;

namespace PF.Core.Interfaces.Alarm
{
    /// <summary>
    /// 报警字典服务：程序启动时反射加载代码内置报警（<c>[AlarmInfo]</c>），
    /// 并与数据库 AlarmDefinitions 表同步清除权限等级，以 O(1) 并发字典提供运行时查询。
    /// <para>
    /// 同步规则（与默认参数一致）：代码有、库里没有的插入（等级取特性默认值）；
    /// 库里已有的等级以数据库为准，其余文案字段以代码为准刷新；代码已删除的报警码从库中移除。
    /// </para>
    /// </summary>
    public interface IAlarmDictionaryService
    {
        /// <summary>
        /// 在程序启动阶段调用，完成反射扫描 + 数据库同步 + 字典初始化。
        /// 必须在 <see cref="IAlarmService"/> 投入使用前完成。
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// 根据报警代码查询字典条目。
        /// 若代码不存在，返回通用兜底条目（不返回 null）。
        /// </summary>
        AlarmInfo GetAlarmInfo(string errorCode);

        /// <summary>获取全部字典条目的只读视图</summary>
        IReadOnlyDictionary<string, AlarmInfo> GetAll();

        /// <summary>
        /// 查询手动清除/复位该报警所需的最低用户等级。
        /// 未定义的报警码返回 <see cref="UserLevel.Operator"/>（与特性默认值一致）。
        /// </summary>
        UserLevel GetRequiredLevel(string errorCode);

        /// <summary>
        /// 批量修改报警码的清除权限等级：写入数据库并即时刷新内存字典，随后引发 <see cref="RequiredLevelsChanged"/>。
        /// </summary>
        /// <param name="levels">报警码 → 新等级（不在字典中的报警码忽略）</param>
        /// <param name="operatorName">操作人，用于审计日志</param>
        /// <returns>写库成功返回 true；失败时内存字典保持不变</returns>
        Task<bool> UpdateRequiredLevelsAsync(IReadOnlyDictionary<string, UserLevel> levels, string operatorName);

        /// <summary>清除权限等级被修改后引发（可能在后台线程）</summary>
        event EventHandler? RequiredLevelsChanged;
    }
}
