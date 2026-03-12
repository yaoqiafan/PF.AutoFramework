using System.Text.Json;

namespace PF.Core.Interfaces.Production
{
    /// <summary>
    /// 生产过程数据服务接口。
    /// 与 SecsGem 无关，适用于任何设备的生产数据记录与历史查询。
    /// <para>设计要点：</para>
    /// <list type="bullet">
    ///   <item>泛型写入：<see cref="RecordAsync{TData}"/> 接受任意 POCO 对象，JSON 序列化存储，Schema 无关</item>
    ///   <item>简单查询：<see cref="QueryAsync"/> 直接返回集合，无分页</item>
    ///   <item>强类型查询：<see cref="QueryDataAsync{TData}"/> 自动反序列化 JsonValue 返回原始类型</item>
    ///   <item>多数据库：后端由注入的 DbContextOptions 决定，服务代码不感知</item>
    ///   <item>实时推送：<see cref="DataRecorded"/> 事件在每条数据写入后触发，供 UI 实时订阅</item>
    /// </list>
    /// </summary>
    public interface IProductionDataService
    {
        // ══════════════════════════════════════════════════════
        //  写入
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 记录一条生产数据（异步非阻塞，内部队列写入）。
        /// </summary>
        /// <typeparam name="TData">数据对象类型，任意 POCO 均可</typeparam>
        /// <param name="data">要记录的数据对象</param>
        /// <param name="recordType">记录类型（可选，用于分类过滤）</param>
        Task RecordAsync<TData>(TData data, string? recordType = null);

        // ══════════════════════════════════════════════════════
        //  查询
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 查询生产记录，返回原始实体集合（含 JsonValue 字符串）。
        /// </summary>
        Task<IReadOnlyList<ProductionRecord>> QueryAsync(ProductionQueryFilter filter);

        /// <summary>
        /// 查询并自动反序列化为目标类型，适用于已知数据模型的场景。
        /// </summary>
        /// <typeparam name="TData">JsonValue 反序列化的目标类型</typeparam>
        Task<IReadOnlyList<TData>> QueryDataAsync<TData>(ProductionQueryFilter filter)
            where TData : class;

        // ══════════════════════════════════════════════════════
        //  导出
        // ══════════════════════════════════════════════════════

        /// <summary>导出查询结果到 CSV 文件</summary>
        Task ExportToCsvAsync(ProductionQueryFilter filter, string filePath);

        /// <summary>导出查询结果到 Excel 文件（.xlsx）</summary>
        Task ExportToExcelAsync(ProductionQueryFilter filter, string filePath);

        // ══════════════════════════════════════════════════════
        //  维护
        // ══════════════════════════════════════════════════════

        /// <summary>清理超过指定保留天数的历史数据</summary>
        Task PurgeOldDataAsync(int retentionDays = 90);

        /// <summary>初始化数据库（建表），服务启动时调用</summary>
        Task InitializeAsync();

        // ══════════════════════════════════════════════════════
        //  事件
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 每条数据写入成功后触发（在非 UI 线程），UI 订阅时需通过 Dispatcher.InvokeAsync 切换线程。
        /// </summary>
        event EventHandler<ProductionDataRecordedEventArgs> DataRecorded;
    }

    // ══════════════════════════════════════════════════════════
    //  过滤器
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 生产数据查询过滤器。
    /// 所有字段均为可空（null 表示不过滤该字段），按需填写即可。
    /// </summary>
    public class ProductionQueryFilter
    {
        /// <summary>采集时间起始（含）</summary>
        public DateTime? StartTime { get; set; }

        /// <summary>采集时间截止（含）</summary>
        public DateTime? EndTime { get; set; }

        /// <summary>记录类型（精确匹配，null = 不过滤）</summary>
        public string? RecordType { get; set; }

        /// <summary>关键词（模糊匹配 JsonValue，null = 不过滤）</summary>
        public string? Keyword { get; set; }

        /// <summary>结果数量上限（null = 无限制；建议设置防止超大数据集）</summary>
        public int? MaxCount { get; set; }
    }

    // ══════════════════════════════════════════════════════════
    //  结果 DTO
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 查询结果 DTO。轻量级，不含 EF Core 追踪，适合直接绑定到 UI。
    /// </summary>
    public class ProductionRecord
    {
        public string Id { get; set; } = string.Empty;
        public string JsonValue { get; set; } = string.Empty;
        public string? TypeFullName { get; set; }
        public string? RecordType { get; set; }
        public DateTime RecordTime { get; set; }
        public DateTime CreateTime { get; set; }

        /// <summary>反序列化 JsonValue 为目标类型（便捷方法）</summary>
        public T? Deserialize<T>() where T : class
        {
            if (string.IsNullOrEmpty(JsonValue)) return null;
            return JsonSerializer.Deserialize<T>(JsonValue);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  事件参数
    // ══════════════════════════════════════════════════════════

    public class ProductionDataRecordedEventArgs : EventArgs
    {
        public ProductionRecord Record { get; set; } = null!;
    }
}
