using Microsoft.EntityFrameworkCore;
using PF.Data.Entity;
using PF.Data.Entity.Category;
using PF.Data.Entity.Category.Basic;

namespace PF.Application.Shell.CustomConfiguration.Param
{

    /// <summary>
    /// ParamType 枚举
    /// </summary>
    public enum ParamType
    {
        /// <summary>
        /// UserLoginParams
        /// </summary>
        UserLoginParams,
        /// <summary>
        /// SystemConfigParams
        /// </summary>
        SystemConfigParams,
        /// <summary>
        /// HardwareParams
        /// </summary>
        HardwareParams
    }


    /// <summary>
    /// DbContext 数据库上下文
    /// </summary>
    public class AppParamDbContext : DbContext
    {
        /// <summary>
        /// AppParamDbContext 数据库上下文
        /// </summary>
        public AppParamDbContext(DbContextOptions<AppParamDbContext> options) : base(options) { }

        /// <summary>
        /// UserLogins参数
        /// </summary>
        public DbSet<UserLoginParam> UserLoginParams { get; set; }
        /// <summary>
        /// SystemParams配置
        /// </summary>
        public DbSet<SystemConfigParam> SystemConfigParams { get; set; }
        /// <summary>
        /// Hardwares参数
        /// </summary>
        public DbSet<HardwareParam> HardwareParams { get; set; }

        /// <summary>
        /// OnModelCreating 模型
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

           
            modelBuilder.Entity<UserLoginParam>()
                .HasIndex(p => new { p.Name})
                .IsUnique();

            modelBuilder.Entity<SystemConfigParam>()
                .HasIndex(p => p.Name)
                .IsUnique();

            modelBuilder.Entity<HardwareParam>()
                .HasIndex(p => p.Name)
                .IsUnique();
        }

        /// <summary>
        /// 保存ChangesAsync
        /// </summary>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // 自动设置UpdateTime
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is ParamEntity &&
                           (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    ((ParamEntity)entry.Entity).CreateTime = DateTime.Now;
                }
                ((ParamEntity)entry.Entity).UpdateTime = DateTime.Now;
            }

            return await base.SaveChangesAsync(cancellationToken);
        }



        /// <summary>
        /// 确保默认参数存在，如果不存在则创建
        /// </summary>
        public async Task EnsureDefaultParametersCreatedAsync(IDefaultParam defaultParam,CancellationToken cancellationToken = default)
        {
            // 确保数据库已创建（全新安装场景：创建所有表）
            await Database.EnsureCreatedAsync(cancellationToken);

            // 兼容已有数据库（升级场景）：确保 HardwareParams 表存在
            // EnsureCreatedAsync 只在数据库不存在时建表，已有库需要手动补建
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

         
            // 初始化UserLoginParams
            await EnsureParametersExistAsync(
                UserLoginParams,
                defaultParam.GetUsersDefaults(),
                cancellationToken);

            // 初始化SystemConfigParams
            await EnsureParametersExistAsync(
                SystemConfigParams,
                defaultParam.GetSystemDefaults(),
                cancellationToken);

            // 初始化HardwareParams（默认硬件设备配置）
            await EnsureParametersExistAsync(
                HardwareParams,
                defaultParam.GetHardwareDefaults(),
                cancellationToken);
        }

        /// <summary>
        /// 通用方法：确保参数存在，如果不存在则创建
        /// </summary>
        private async Task EnsureParametersExistAsync<T>(
            DbSet<T> dbSet,
            Dictionary<string, T> defaultParameters,
            CancellationToken cancellationToken) where T : ParamEntity
        {
            if (defaultParameters == null || !defaultParameters.Any())
                return;

            // 获取已存在的参数名称
            var existingNames = await dbSet
                .Where(p => defaultParameters.Keys.Contains(p.Name))
                .Select(p => p.Name)
                .ToListAsync(cancellationToken);

            // 找出不存在的参数
            var missingParameters = defaultParameters
                .Where(kvp => !existingNames.Contains(kvp.Key))
                .Select(kvp => kvp.Value)
                .ToList();

            // 添加不存在的参数
            if (missingParameters.Any())
            {
                await dbSet.AddRangeAsync(missingParameters, cancellationToken);
                await SaveChangesAsync(cancellationToken);
            }
        }

    }
}
