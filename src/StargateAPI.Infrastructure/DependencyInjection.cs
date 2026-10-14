using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StargateAPI.Application.Abstractions;
using StargateAPI.Infrastructure.Persistence;

namespace StargateAPI.Infrastructure
{
    public static class DependencyInjection
    {
        /// <summary>SQLite persistence and the implementations of the Application's abstractions.</summary>
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
        {
            services.AddDbContext<StargateContext>(options =>
                options.UseSqlite(ResolveSqliteConnectionString(
                    configuration.GetConnectionString("StarbaseApiDatabase"),
                    environment.ContentRootPath)));

            // The use cases see the same scoped context through their abstraction.
            services.AddScoped<IStargateDbContext>(provider => provider.GetRequiredService<StargateContext>());
            services.AddScoped<IWriteTransactions, SqliteWriteTransactions>();
            services.AddSingleton<IPersistenceErrors, SqlitePersistenceErrors>();
            services.AddSingleton<IRequestOutcomeLog, RequestOutcomeLog>();
            return services;
        }

        // A relative SQLite path would resolve against the process working directory, so starting
        // the API from another folder would silently create a second, empty database. Anchor it to
        // the content root instead; absolute paths and in-memory databases are left unchanged.
        private static string ResolveSqliteConnectionString(string? connectionString, string contentRootPath)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'StarbaseApiDatabase' is not configured.");
            }

            var csb = new SqliteConnectionStringBuilder(connectionString);
            if (csb.Mode != SqliteOpenMode.Memory
                && !string.IsNullOrEmpty(csb.DataSource)
                && csb.DataSource != ":memory:"
                && !Path.IsPathRooted(csb.DataSource))
            {
                csb.DataSource = Path.GetFullPath(Path.Combine(contentRootPath, csb.DataSource));
            }

            // Bound how long a command waits on another writer's lock (SQLITE_BUSY becomes a 503)
            // unless the connection string sets its own value; the provider default is 30 seconds.
            if (csb.DefaultTimeout == 30)
            {
                csb.DefaultTimeout = 5;
            }

            return csb.ToString();
        }
    }
}
