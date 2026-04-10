using PF.Core.Models;

namespace PF.Core.Interfaces.Alarm
{
    /// <summary>
    /// 报警字典服务：负责在程序启动时合并加载代码内置报警和数据库扩展报警，
    /// 并以 O(1) 并发字典提供运行时查询。
    /// </summary>
    public interface IAlarmDictionaryService
    {
        /// <summary>
        /// 在程序启动阶段调用，完成反射扫描 + 数据库加载 + 字典初始化。
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
    }
}
