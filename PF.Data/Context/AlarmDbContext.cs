using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PF.Data.Entity.Alarm;

namespace PF.Data.Context
{
    /// <summary>
    /// 报警独立数据库上下文。
    /// 与主参数数据库（SystemParamsCollection.db）、生产数据库（ProductionHistory.db）
    /// 物理隔离，存储于 AlarmHistory.db。
    ///
    /// 年份分表：<see cref="AlarmRecordEntity"/> 的物理表名根据 <see cref="CurrentYear"/>
    /// 动态生成（如 AlarmRecord_2026）。通过自定义 <see cref="AlarmModelCacheKeyFactory"/>
    /// 让 EF Core 为每个年份独立缓存 Model，实现透明分表。
    /// </summary>
    public class AlarmDbContext : DbContext
    {
        /// <summary>当前上下文对应的年份（决定分表名称）</summary>
        public int CurrentYear { get; }

        public AlarmDbContext(DbContextOptions<AlarmDbContext> options, int year = 0)
            : base(options)
        {
            CurrentYear = year > 0 ? year : DateTime.Now.Year;
        }

        public DbSet<AlarmDefinitionEntity> AlarmDefinitions { get; set; } = null!;
        public DbSet<AlarmRecordEntity> AlarmRecords { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // 注入自定义 ModelCacheKey，使 EF Core 按年份区分缓存的 Model
            optionsBuilder.ReplaceService<IModelCacheKeyFactory, AlarmModelCacheKeyFactory>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 报警字典表（共享，不分年份）
            modelBuilder.Entity<AlarmDefinitionEntity>(entity =>
            {
                entity.ToTable("AlarmDefinitions");
                entity.HasKey(e => e.ErrorCode);
                entity.Property(e => e.ErrorCode).HasMaxLength(64);
                entity.Property(e => e.Category).HasMaxLength(64);
                entity.Property(e => e.Message).HasMaxLength(512);
                entity.Property(e => e.Solution).HasMaxLength(4096);
            });

            // 报警流水表（按年份分表）
            var recordTable = $"AlarmRecord_{CurrentYear}";
            modelBuilder.Entity<AlarmRecordEntity>(entity =>
            {
                entity.ToTable(recordTable);
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.ErrorCode).HasMaxLength(64);
                entity.Property(e => e.Source).HasMaxLength(128);

                entity.HasIndex(e => new { e.Source, e.IsActive })
                      .HasDatabaseName($"IX_{recordTable}_Source_IsActive");

                entity.HasIndex(e => e.TriggerTime)
                      .HasDatabaseName($"IX_{recordTable}_TriggerTime");

                entity.HasIndex(e => e.IsActive)
                      .HasDatabaseName($"IX_{recordTable}_IsActive");
            });
        }
    }

    /// <summary>
    /// 自定义 Model 缓存键工厂：将年份纳入缓存键，
    /// 确保 EF Core 为每个年份独立编译并缓存 Model（分表透明化）。
    /// </summary>
    internal sealed class AlarmModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) =>
            context is AlarmDbContext alarmCtx
                ? (context.GetType(), alarmCtx.CurrentYear, designTime)
                : (context.GetType(), designTime);
    }
}
