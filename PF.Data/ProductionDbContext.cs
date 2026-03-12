using Microsoft.EntityFrameworkCore;
using PF.Data.Entity.Category;

namespace PF.Data
{
    /// <summary>
    /// 生产数据独立数据库上下文。
    /// 数据库文件与系统参数数据库（SystemParamsCollection.db）完全隔离，
    /// 默认路径：%APPDATA%\PFAutoFrameWork\ProductionHistory.db。
    /// 多数据库后端（SQLite / SQL Server / MySQL）通过注入
    /// DbContextOptions&lt;ProductionDbContext&gt; 切换，其余代码零改动。
    /// </summary>
    public class ProductionDbContext : DbContext
    {
        public ProductionDbContext(DbContextOptions<ProductionDbContext> options)
            : base(options)
        {
        }

        public DbSet<ProductionDataEntity> ProductionData { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 时间范围查询索引
            modelBuilder.Entity<ProductionDataEntity>()
                .HasIndex(p => p.RecordTime)
                .HasDatabaseName("IX_ProductionData_RecordTime");

            // 记录类型索引
            modelBuilder.Entity<ProductionDataEntity>()
                .HasIndex(p => p.RecordType)
                .HasDatabaseName("IX_ProductionData_RecordType");
        }
    }
}
