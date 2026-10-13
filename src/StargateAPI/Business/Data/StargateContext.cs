using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace StargateAPI.Business.Data
{
    /// <summary>
    /// The single application database: astronaut data, process logs, and the ASP.NET Core
    /// Identity tables plus server-side sessions for signing in.
    /// </summary>
    public class StargateContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public DbSet<Person> People { get; set; }
        public DbSet<AstronautDetail> AstronautDetails { get; set; }
        public DbSet<AstronautDuty> AstronautDuties { get; set; }
        public DbSet<ProcessLog> ProcessLogs { get; set; }
        public DbSet<AuthSession> AuthSessions { get; set; }

        public StargateContext(DbContextOptions<StargateContext> options)
        : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Identity's own table configuration first, then this application's.
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(StargateContext).Assembly);
        }
    }
}
