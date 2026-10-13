using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StargateAPI.Domain;
using StargateAPI.Application.Logging;
using StargateAPI.Infrastructure.Identity;
using StargateAPI.Infrastructure.Persistence;

namespace StargateAPI.Tests;

public class MigrationTests
{
    private const string InitialCreate = "20240122154939_InitialCreate";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Guid_conversion_aborts_on_a_populated_legacy_database_and_leaves_it_unchanged()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(Ct);
        await using var context = new StargateContext(
            new DbContextOptionsBuilder<StargateContext>().UseSqlite(connection).Options);
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(InitialCreate, Ct);
        await context.Database.ExecuteSqlRawAsync("INSERT INTO \"Person\" (\"Name\") VALUES ('Legacy Person');", Ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => migrator.MigrateAsync(cancellationToken: Ct));

        Assert.Contains("populated_database_conversion_not_supported", failure.ToString());
        Assert.Equal([InitialCreate], await context.Database.GetAppliedMigrationsAsync(Ct));
        Assert.Equal("INTEGER", await ScalarAsync(connection, "SELECT \"type\" FROM pragma_table_info('Person') WHERE \"name\" = 'Id';"));
        Assert.Equal("1|Legacy Person", await ScalarAsync(connection, "SELECT \"Id\" || '|' || \"Name\" FROM \"Person\";"));
    }

    [Fact]
    public async Task Full_migration_chain_applies_to_an_empty_database()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(Ct);
        await using var context = new StargateContext(
            new DbContextOptionsBuilder<StargateContext>().UseSqlite(connection).Options);

        await context.Database.MigrateAsync(Ct);

        Assert.Empty(await context.Database.GetPendingMigrationsAsync(Ct));
        Assert.Equal("TEXT", await ScalarAsync(connection, "SELECT \"type\" FROM pragma_table_info('Person') WHERE \"name\" = 'Id';"));
        var openIndex = await ScalarAsync(connection,
            "SELECT \"sql\" FROM sqlite_master WHERE \"name\" = 'IX_AstronautDuty_PersonId_Open';");
        Assert.Contains("WHERE \"DutyEndDate\" IS NULL", openIndex);
    }

    private static async Task<string?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync(Ct))?.ToString();
    }
}
