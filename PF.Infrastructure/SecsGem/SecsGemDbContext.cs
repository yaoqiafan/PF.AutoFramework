using Microsoft.EntityFrameworkCore;
using PF.Infrastructure.SecsGem;
using PF.Infrastructure.SecsGem.Entities;
using PF.Infrastructure.SecsGem.Entities.Command;
using PF.Infrastructure.SecsGem.Entities.System;
using PF.Infrastructure.SecsGem.Entities.Variable;

namespace PF.Infrastructure.SecsGem
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
