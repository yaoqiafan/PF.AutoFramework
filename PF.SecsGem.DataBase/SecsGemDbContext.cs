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
        public SecsGemDbContext(DbContextOptions<SecsGemDbContext> options)
            : base(options)
        {
        }

        public DbSet<SecsGemSystemEntity> SystemConfigs { get; set; }
        public DbSet<CommandIDEntity> CommnadIDs { get; set; }
        public DbSet<CEIDEntity> CEIDs { get; set; }
        public DbSet<ReportIDEntity> ReportIDs { get; set; }
        public DbSet<VIDEntity> VIDs { get; set; }
        public DbSet<IncentiveEntity> IncentiveCommands { get; set; }
        public DbSet<ResponseEntity> ResponseCommands { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
