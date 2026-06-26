using Microsoft.EntityFrameworkCore;
using PF.Data.Entity;
using PF.Data.Entity.Category;
using PF.Data.Entity.Category.Basic;

namespace PF.Application.Base.Configuration
{
    /// <summary>参数存储分类：用户登录参数 / 系统配置参数 / 硬件参数。</summary>
    public enum ParamType
    {
        /// <summary>用户登录参数表。</summary>
        UserLoginParams,
        /// <summary>系统配置参数表。</summary>
        SystemConfigParams,
        /// <summary>硬件连接参数表。</summary>
        HardwareParams
    }

    /// <summary>
    /// 参数数据库上下文（UserLoginParam / SystemConfigParam / HardwareParam 三表通用）
    /// </summary>
    public class AppParamDbContext : DbContext
    {
        /// <summary>构造函数，注入 EF Core 配置选项。</summary>
        public AppParamDbContext(DbContextOptions<AppParamDbContext> options) : base(options) { }

        /// <summary>用户登录参数表。</summary>
        public DbSet<UserLoginParam> UserLoginParams { get; set; }
        /// <summary>系统配置参数表。</summary>
        public DbSet<SystemConfigParam> SystemConfigParams { get; set; }
        /// <summary>硬件连接参数表。</summary>
        public DbSet<HardwareParam> HardwareParams { get; set; }

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserLoginParam>()
                .HasIndex(p => new { p.Name })
                .IsUnique();

            modelBuilder.Entity<SystemConfigParam>()
                .HasIndex(p => p.Name)
                .IsUnique();

            modelBuilder.Entity<HardwareParam>()
                .HasIndex(p => p.Name)
                .IsUnique();
        }

        /// <summary>保存更改并自动维护 CreateTime / UpdateTime 审计字段。</summary>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is ParamEntity &&
                           (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                    ((ParamEntity)entry.Entity).CreateTime = DateTime.Now;
                ((ParamEntity)entry.Entity).UpdateTime = DateTime.Now;
            }

            return await base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>确保数据库已建表，并根据 <paramref name="defaultParam"/> 补全缺失的默认参数行、移除已废弃行。</summary>
        public async Task EnsureDefaultParametersCreatedAsync(IDefaultParam defaultParam, CancellationToken cancellationToken = default)
        {
            await Database.EnsureCreatedAsync(cancellationToken);

            await Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS HardwareParams (
                    ID          TEXT NOT NULL,
                    Name        TEXT NOT NULL,
                    JsonValue   TEXT,
                    TypeFullName TEXT,
                    Category    TEXT,
                    Description TEXT,
                    CreateTime  TEXT NOT NULL DEFAULT '',
                    UpdateTime  TEXT NOT NULL DEFAULT '',
                    Version     INTEGER NOT NULL DEFAULT 0,
                    CONSTRAINT PK_HardwareParams PRIMARY KEY (ID)
                );", cancellationToken);

            await Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_HardwareParams_Name ON HardwareParams (Name);",
                cancellationToken);

            await EnsureParametersExistAsync(UserLoginParams, defaultParam.GetUsersDefaults(), cancellationToken);
            await EnsureParametersExistAsync(SystemConfigParams, defaultParam.GetSystemDefaults(), cancellationToken);
            await EnsureParametersExistAsync(HardwareParams, defaultParam.GetHardwareDefaults(), cancellationToken);
        }

        private async Task EnsureParametersExistAsync<T>(
            DbSet<T> dbSet,
            Dictionary<string, T> defaultParameters,
            CancellationToken cancellationToken) where T : ParamEntity
        {
            if (defaultParameters == null || !defaultParameters.Any())
                return;

            var allExisting = await dbSet.ToListAsync(cancellationToken);

            var staleParameters = allExisting
                .Where(p => !defaultParameters.ContainsKey(p.Name))
                .ToList();

            if (staleParameters.Any())
            {
                dbSet.RemoveRange(staleParameters);
                await SaveChangesAsync(cancellationToken);
            }

            var existingNames = allExisting
                .Where(p => defaultParameters.ContainsKey(p.Name))
                .Select(p => p.Name)
                .ToList();

            var missingParameters = defaultParameters
                .Where(kvp => !existingNames.Contains(kvp.Key))
                .Select(kvp => kvp.Value)
                .ToList();

            if (missingParameters.Any())
            {
                await dbSet.AddRangeAsync(missingParameters, cancellationToken);
                await SaveChangesAsync(cancellationToken);
            }
        }
    }
}
