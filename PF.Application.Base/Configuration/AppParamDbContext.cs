using Microsoft.EntityFrameworkCore;
using PF.Data.Entity;
using PF.Data.Entity.Category;
using PF.Data.Entity.Category.Basic;

namespace PF.Application.Base.Configuration
{
    /// <summary>参数存储分类：用户登录参数 / 系统配置参数 / 硬件参数 / 通讯参数。</summary>
    public enum ParamType
    {
        /// <summary>用户登录参数表。</summary>
        UserLoginParams,
        /// <summary>系统配置参数表。</summary>
        SystemConfigParams,
        /// <summary>硬件连接参数表。</summary>
        HardwareParams,
        /// <summary>通讯实例参数表。</summary>
        CommunicationParams
    }

    /// <summary>
    /// 参数数据库上下文（UserLoginParam / SystemConfigParam / HardwareParam / CommunicationParam 四表通用）
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
        /// <summary>通讯实例参数表。</summary>
        public DbSet<CommunicationParam> CommunicationParams { get; set; }

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

            modelBuilder.Entity<CommunicationParam>()
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
                    Remarks     TEXT,
                    CreateTime  TEXT NOT NULL DEFAULT '',
                    UpdateTime  TEXT NOT NULL DEFAULT '',
                    Version     INTEGER NOT NULL DEFAULT 0,
                    CONSTRAINT PK_HardwareParams PRIMARY KEY (ID)
                );", cancellationToken);

            await Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_HardwareParams_Name ON HardwareParams (Name);",
                cancellationToken);

            // CommunicationParams 表是在 SystemParamsCollection.db 已经存在之后才加入的，
            // EnsureCreatedAsync 只在数据库文件整个不存在时才会按当前 Model 建表，
            // 已经建过库的机器上必须靠这段显式 SQL 才能补出这张表（同 HardwareParams 的处理方式）。
            await Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS CommunicationParams (
                    ID          TEXT NOT NULL,
                    Name        TEXT NOT NULL,
                    JsonValue   TEXT,
                    TypeFullName TEXT,
                    Category    TEXT,
                    Description TEXT,
                    Remarks     TEXT,
                    CreateTime  TEXT NOT NULL DEFAULT '',
                    UpdateTime  TEXT NOT NULL DEFAULT '',
                    Version     INTEGER NOT NULL DEFAULT 0,
                    CONSTRAINT PK_CommunicationParams PRIMARY KEY (ID)
                );", cancellationToken);

            await Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_CommunicationParams_Name ON CommunicationParams (Name);",
                cancellationToken);

            // v1.0.1 的上述建表 SQL 曾漏掉 Remarks 列（EF 模型经 BasicEntity 映射了该列，
            // OnModelCreating 未 Ignore），导致老库上对这两张表的首次 EF 查询直接抛
            // "no such column: Remarks"。全新库由 EnsureCreatedAsync 按完整模型建表不受影响，
            // 但凡是跑过 v1.0.1 裸 SQL 建表的机器都会留下缺列的表，这里幂等补齐。
            await AddColumnIfMissingAsync("HardwareParams", "Remarks", "TEXT", cancellationToken);
            await AddColumnIfMissingAsync("CommunicationParams", "Remarks", "TEXT", cancellationToken);

            await EnsureParametersExistAsync(UserLoginParams, defaultParam.GetUsersDefaults(), cancellationToken);
            await EnsureParametersExistAsync(SystemConfigParams, defaultParam.GetSystemDefaults(), cancellationToken);
            await EnsureParametersExistAsync(HardwareParams, defaultParam.GetHardwareDefaults(), cancellationToken);
            await EnsureParametersExistAsync(CommunicationParams, defaultParam.GetCommunicationDefaults(), cancellationToken);
        }

        /// <summary>
        /// 幂等补列：目标表缺少指定列时执行 ALTER TABLE ADD COLUMN，已存在则跳过。
        /// SQLite 的 ADD COLUMN 对已存在列会直接报错，故先经 PRAGMA table_info 检查。
        /// </summary>
        private async Task AddColumnIfMissingAsync(string tableName, string columnName, string columnType,
            CancellationToken cancellationToken)
        {
            var connection = Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info({tableName});";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }

            await Database.ExecuteSqlRawAsync(
                $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType};", cancellationToken);
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
