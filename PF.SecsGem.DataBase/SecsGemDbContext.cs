using Microsoft.EntityFrameworkCore;
using PF.SecsGem.DataBase.Entities.Command;
using PF.SecsGem.DataBase.Entities.System;
using PF.SecsGem.DataBase.Entities.Variable;

namespace PF.SecsGem.DataBase
{
    /// <summary>
    /// SecsGem 独立数据库上下文
    /// </summary>
    public class SecsGemDbContext : DbContext
    {
        /// <summary>
        /// SecsGemDbContext 数据库上下文
        /// </summary>
        public SecsGemDbContext(DbContextOptions<SecsGemDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Systems配置
        /// </summary>
        public DbSet<SecsGemSystemEntity> SystemConfigs { get; set; }
        /// <summary>
        /// Commnads标识
        /// </summary>
        public DbSet<CommandIDEntity> CommnadIDs { get; set; }
        /// <summary>
        /// CEs标识
        /// </summary>
        public DbSet<CEIDEntity> CEIDs { get; set; }
        /// <summary>
        /// Reports标识
        /// </summary>
        public DbSet<ReportIDEntity> ReportIDs { get; set; }
        /// <summary>
        /// Vs标识
        /// </summary>
        public DbSet<VIDEntity> VIDs { get; set; }
        /// <summary>
        /// IncentiveCommands
        /// </summary>
        public DbSet<IncentiveEntity> IncentiveCommands { get; set; }
        /// <summary>
        /// ResponseCommands
        /// </summary>
        public DbSet<ResponseEntity> ResponseCommands { get; set; }

        /// <summary>配置数据模型创建规则</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
