using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using PF.Core.Interfaces.Production;
using PF.Data;
using PF.Data.Entity.Category;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace PF.Services.Production
{
    /// <summary>
    /// 生产过程数据服务实现。
    /// <para>写入模式：Channel（有界队列 10000）+ 单消费者后台线程，RecordAsync 非阻塞立即返回。</para>
    /// <para>读取模式：每次查询 new 独立 DbContext（线程安全），AsNoTracking 优化只读性能。</para>
    /// <para>多数据库：通过注入 DbContextOptions&lt;ProductionDbContext&gt; 切换后端，服务代码不感知。</para>
    /// </summary>
    public class ProductionDataService : IProductionDataService, IDisposable
    {
        private readonly DbContextOptions<ProductionDbContext> _dbOptions;

        // 写入专用（单消费者线程，无并发问题）
        private ProductionDbContext? _writeContext;
        private readonly Channel<ProductionDataEntity> _writeChannel =
            Channel.CreateBounded<ProductionDataEntity>(new BoundedChannelOptions(10000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = true
            });

        private readonly CancellationTokenSource _cts = new();
        private Task? _consumerTask;
        private bool _disposed;

        public event EventHandler<ProductionDataRecordedEventArgs>? DataRecorded;

        public ProductionDataService(DbContextOptions<ProductionDbContext> dbOptions)
        {
            _dbOptions = dbOptions ?? throw new ArgumentNullException(nameof(dbOptions));
        }

        // ══════════════════════════════════════════════════════
        //  初始化
        // ══════════════════════════════════════════════════════

        public async Task InitializeAsync()
        {
            // 建表（若不存在）
            await using var ctx = new ProductionDbContext(_dbOptions);
            await ctx.Database.EnsureCreatedAsync();

            // 创建写专用 DbContext 并启动消费者线程
            _writeContext = new ProductionDbContext(_dbOptions);
            _consumerTask = Task.Factory.StartNew(
                ConsumeAsync,
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        // ══════════════════════════════════════════════════════
        //  写入
        // ══════════════════════════════════════════════════════

        public async Task RecordAsync<TData>(TData data,
                                              string? name = null,
                                              string? deviceId = null,
                                              string? recordType = null,
                                              string? batchId = null)
        {
            var entity = new ProductionDataEntity
            {
                ID = Guid.NewGuid().ToString(),
                Name = name ?? typeof(TData).Name,
                JsonValue = JsonSerializer.Serialize(data),
                TypeFullName = typeof(TData).FullName,
                Category = typeof(TData).Name,
                DeviceId = deviceId,
                RecordType = recordType,
                BatchId = batchId,
                RecordTime = DateTime.Now,
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now
            };

            await _writeChannel.Writer.WriteAsync(entity, _cts.Token);
        }

        // 后台消费者：单线程写入，保证 DbContext 线程安全
        private async Task ConsumeAsync()
        {
            try
            {
                await foreach (var entity in _writeChannel.Reader.ReadAllAsync(_cts.Token))
                {
                    try
                    {
                        _writeContext!.ProductionData.Add(entity);
                        await _writeContext.SaveChangesAsync(_cts.Token);
                        _writeContext.Entry(entity).State = EntityState.Detached;

                        // 触发实时推送（UI 需 Dispatcher.InvokeAsync 切线程）
                        DataRecorded?.Invoke(this, new ProductionDataRecordedEventArgs
                        {
                            Record = MapToRecord(entity)
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[ProductionDataService] 写入失败: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        // ══════════════════════════════════════════════════════
        //  查询
        // ══════════════════════════════════════════════════════

        public async Task<IReadOnlyList<ProductionRecord>> QueryAsync(ProductionQueryFilter filter)
        {
            await using var ctx = new ProductionDbContext(_dbOptions);
            var q = BuildQuery(ctx, filter);
            var entities = await q.OrderByDescending(x => x.RecordTime).ToListAsync();
            return entities.Select(MapToRecord).ToList();
        }

        public async Task<IReadOnlyList<TData>> QueryDataAsync<TData>(ProductionQueryFilter filter)
            where TData : class
        {
            var records = await QueryAsync(filter);
            var results = new List<TData>(records.Count);
            foreach (var r in records)
            {
                var obj = r.Deserialize<TData>();
                if (obj != null) results.Add(obj);
            }
            return results;
        }

        private IQueryable<ProductionDataEntity> BuildQuery(
            ProductionDbContext ctx, ProductionQueryFilter filter)
        {
            var q = ctx.ProductionData.AsNoTracking().AsQueryable();

            if (filter.StartTime.HasValue)
                q = q.Where(x => x.RecordTime >= filter.StartTime.Value);
            if (filter.EndTime.HasValue)
                q = q.Where(x => x.RecordTime <= filter.EndTime.Value);
            if (!string.IsNullOrEmpty(filter.DeviceId))
                q = q.Where(x => x.DeviceId == filter.DeviceId);
            if (!string.IsNullOrEmpty(filter.RecordType))
                q = q.Where(x => x.RecordType == filter.RecordType);
            if (!string.IsNullOrEmpty(filter.BatchId))
                q = q.Where(x => x.BatchId == filter.BatchId);
            if (!string.IsNullOrEmpty(filter.Keyword))
                q = q.Where(x => x.Name.Contains(filter.Keyword)
                               || x.JsonValue.Contains(filter.Keyword));
            if (filter.MaxCount.HasValue)
                q = q.Take(filter.MaxCount.Value);

            return q;
        }

        // ══════════════════════════════════════════════════════
        //  导出
        // ══════════════════════════════════════════════════════

        public async Task ExportToCsvAsync(ProductionQueryFilter filter, string filePath)
        {
            var records = await QueryAsync(filter);
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            // 标题行
            await writer.WriteLineAsync(
                "ID,Name,DeviceId,RecordType,BatchId,RecordTime,Category,TypeFullName,JsonValue,Remarks");

            foreach (var r in records)
            {
                await writer.WriteLineAsync(string.Join(",",
                    EscapeCsv(r.Id),
                    EscapeCsv(r.Name),
                    EscapeCsv(r.DeviceId),
                    EscapeCsv(r.RecordType),
                    EscapeCsv(r.BatchId),
                    r.RecordTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    EscapeCsv(r.Category),
                    EscapeCsv(r.TypeFullName),
                    EscapeCsv(r.JsonValue),
                    EscapeCsv(r.Remarks)));
            }
        }

        public async Task ExportToExcelAsync(ProductionQueryFilter filter, string filePath)
        {
            var records = await QueryAsync(filter);
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await Task.Run(() =>
            {
                IWorkbook workbook = new XSSFWorkbook();
                ISheet sheet = workbook.CreateSheet("生产数据");

                // 标题行样式
                var headerStyle = workbook.CreateCellStyle();
                var headerFont = workbook.CreateFont();
                headerFont.IsBold = true;
                headerStyle.SetFont(headerFont);

                string[] headers =
                [
                    "ID", "名称", "设备ID", "记录类型", "批次", "采集时间",
                    "分类", "数据类型", "JSON数据", "备注"
                ];
                var headerRow = sheet.CreateRow(0);
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = headerRow.CreateCell(i);
                    cell.SetCellValue(headers[i]);
                    cell.CellStyle = headerStyle;
                }

                // 数据行
                int rowIdx = 1;
                foreach (var r in records)
                {
                    var row = sheet.CreateRow(rowIdx++);
                    row.CreateCell(0).SetCellValue(r.Id);
                    row.CreateCell(1).SetCellValue(r.Name);
                    row.CreateCell(2).SetCellValue(r.DeviceId ?? "");
                    row.CreateCell(3).SetCellValue(r.RecordType ?? "");
                    row.CreateCell(4).SetCellValue(r.BatchId ?? "");
                    row.CreateCell(5).SetCellValue(r.RecordTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    row.CreateCell(6).SetCellValue(r.Category);
                    row.CreateCell(7).SetCellValue(r.TypeFullName ?? "");
                    row.CreateCell(8).SetCellValue(r.JsonValue);
                    row.CreateCell(9).SetCellValue(r.Remarks ?? "");
                }

                // 自动列宽（最多 255 字符宽）
                for (int i = 0; i < headers.Length; i++)
                    sheet.AutoSizeColumn(i);

                using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                workbook.Write(fs);
            });
        }

        // ══════════════════════════════════════════════════════
        //  维护
        // ══════════════════════════════════════════════════════

        public async Task PurgeOldDataAsync(int retentionDays = 90)
        {
            var cutoff = DateTime.Now.AddDays(-retentionDays);
            await using var ctx = new ProductionDbContext(_dbOptions);
            var old = await ctx.ProductionData
                .Where(x => x.RecordTime < cutoff)
                .ToListAsync();
            if (old.Count > 0)
            {
                ctx.ProductionData.RemoveRange(old);
                await ctx.SaveChangesAsync();
            }
        }

        // ══════════════════════════════════════════════════════
        //  私有辅助
        // ══════════════════════════════════════════════════════

        private static ProductionRecord MapToRecord(ProductionDataEntity e) => new()
        {
            Id = e.ID,
            Name = e.Name,
            JsonValue = e.JsonValue,
            TypeFullName = e.TypeFullName,
            Category = e.Category,
            DeviceId = e.DeviceId,
            RecordType = e.RecordType,
            RecordTime = e.RecordTime,
            BatchId = e.BatchId,
            CreateTime = e.CreateTime,
            Remarks = e.Remarks
        };

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        // ══════════════════════════════════════════════════════
        //  Dispose
        // ══════════════════════════════════════════════════════

        public void Dispose()
        {
            if (_disposed) return;
            _writeChannel.Writer.TryComplete();
            _cts.Cancel();
            try { _consumerTask?.Wait(TimeSpan.FromSeconds(5)); } catch { }
            _writeContext?.Dispose();
            _cts.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
