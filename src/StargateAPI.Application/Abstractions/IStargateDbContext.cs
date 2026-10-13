using Microsoft.EntityFrameworkCore;
using StargateAPI.Application.Logging;
using StargateAPI.Domain;

namespace StargateAPI.Application.Abstractions
{
    /// <summary>
    /// The use cases' view of the database: entity sets and saving. Implemented in Infrastructure;
    /// nothing here is specific to a database provider.
    /// </summary>
    public interface IStargateDbContext
    {
        DbSet<Person> People { get; }

        DbSet<AstronautDetail> AstronautDetails { get; }

        DbSet<AstronautDuty> AstronautDuties { get; }

        DbSet<ProcessLog> ProcessLogs { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
