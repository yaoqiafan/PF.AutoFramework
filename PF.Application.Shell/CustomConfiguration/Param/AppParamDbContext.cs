using Microsoft.EntityFrameworkCore;
using PF.Common.Core.Param.Entity;
using PF.Common.Core.Param.Entity.Category;
using PF.Common.Core.Param.Entity.Category.Default;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Application.Shell.CustomConfiguration.Param
{

    public enum ParamType
    {
        CommonParams,
        UserLoginParams,
        SystemConfigParams
    }


    public class AppParamDbContext : DbContext
    {
        public AppParamDbContext(DbContextOptions<AppParamDbContext> options) : base(options) { }

        public DbSet<CommonParam> CommonParams { get; set; }
        public DbSet<UserLoginParam> UserLoginParams { get; set; }
        public DbSet<SystemConfigParam> SystemConfigParams { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 配置参数表的索引
            modelBuilder.Entity<CommonParam>()
                .HasIndex(p => p.Name)
                .IsUnique();

            modelBuilder.Entity<UserLoginParam>()
                .HasIndex(p => new { p.Name})
                .IsUnique();

            modelBuilder.Entity<SystemConfigParam>()
                .HasIndex(p => p.Name)
                .IsUnique();
        }

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
            // 确保数据库已创建
            await Database.EnsureCreatedAsync(cancellationToken);

            // 初始化CommonParams
            await EnsureParametersExistAsync(
                CommonParams,
                defaultParam.GetCommonDefaults(),
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
