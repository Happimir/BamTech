using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StargateAPI.Infrastructure.Persistence;

namespace StargateAPI.Infrastructure.Identity
{
    /// <summary>
    /// Idempotently creates the Admin/Reader roles and the "admin"/"reader" demo accounts.
    /// Passwords come from configuration (user-secrets or environment variables). In Development
    /// only, unset passwords fall back to the fixed, documented demo passwords below so a local demo
    /// always uses known credentials; every other environment must configure its own.
    /// Existing accounts keep their passwords; only missing roles are added.
    /// Run with: dotnet run --project src/StargateAPI -- seed-demo-users
    /// </summary>
    public static class DemoUserSeeder
    {
        public const string Command = "seed-demo-users";
        public const string AdminUserName = "admin";
        public const string ReaderUserName = "reader";

        // Public by design: local demo credentials, never used outside Development.
        public const string DevelopmentAdminPassword = "Admin!2026";
        public const string DevelopmentReaderPassword = "Reader!2026";

        /// <summary>
        /// The passwords to seed: configured values first, then the fixed demo passwords only in
        /// Development. Null when a password is missing outside Development.
        /// </summary>
        public static (string Admin, string Reader)? ResolvePasswords(IConfiguration configuration, IHostEnvironment environment)
        {
            var adminPassword = configuration["DemoUsers:AdminPassword"];
            var readerPassword = configuration["DemoUsers:ReaderPassword"];
            if (environment.IsDevelopment())
            {
                adminPassword = string.IsNullOrWhiteSpace(adminPassword) ? DevelopmentAdminPassword : adminPassword;
                readerPassword = string.IsNullOrWhiteSpace(readerPassword) ? DevelopmentReaderPassword : readerPassword;
            }
            if (string.IsNullOrWhiteSpace(adminPassword) || string.IsNullOrWhiteSpace(readerPassword))
            {
                return null;
            }
            return (adminPassword, readerPassword);
        }

        /// <summary>Command-line entry point. Returns a process exit code.</summary>
        public static async Task<int> RunAsync(IServiceProvider services, IConfiguration configuration, IHostEnvironment environment, ILogger logger)
        {
            var passwords = ResolvePasswords(configuration, environment);
            if (passwords is null)
            {
                logger.LogError(
                    "Set DemoUsers:AdminPassword and DemoUsers:ReaderPassword (user-secrets or environment variables) first. " +
                    "The built-in demo passwords apply only in Development; this environment is {Environment}.",
                    environment.EnvironmentName);
                return 1;
            }
            var (adminPassword, readerPassword) = passwords.Value;

            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
            if ((await context.Database.GetPendingMigrationsAsync()).Any())
            {
                logger.LogError("The database is not up to date. Run 'dotnet ef database update' first.");
                return 1;
            }

            var errors = await SeedAsync(scope.ServiceProvider, adminPassword, readerPassword);
            foreach (var error in errors)
            {
                logger.LogError("Seeding failed: {Error}", error);
            }
            if (errors.Count == 0)
            {
                logger.LogInformation("Demo users are ready: '{Admin}' (Admin) and '{Reader}' (Reader).", AdminUserName, ReaderUserName);
            }
            return errors.Count == 0 ? 0 : 1;
        }

        /// <summary>Creates missing roles and accounts. Returns Identity error descriptions, if any.</summary>
        public static async Task<IReadOnlyList<string>> SeedAsync(IServiceProvider services, string adminPassword, string readerPassword)
        {
            var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var errors = new List<string>();

            foreach (var role in new[] { Roles.Admin, Roles.Reader })
            {
                if (!await roles.RoleExistsAsync(role))
                {
                    Collect(errors, await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() }));
                }
            }

            foreach (var (userName, role, password) in new[]
            {
                (AdminUserName, Roles.Admin, adminPassword),
                (ReaderUserName, Roles.Reader, readerPassword)
            })
            {
                var user = await users.FindByNameAsync(userName);
                if (user is null)
                {
                    user = new ApplicationUser { UserName = userName };
                    var created = await users.CreateAsync(user, password);
                    Collect(errors, created);
                    if (!created.Succeeded)
                    {
                        continue;
                    }
                }
                if (!await users.IsInRoleAsync(user, role))
                {
                    Collect(errors, await users.AddToRoleAsync(user, role));
                }
            }

            return errors;
        }

        private static void Collect(List<string> errors, IdentityResult result) =>
            errors.AddRange(result.Errors.Select(e => e.Description));
    }
}
