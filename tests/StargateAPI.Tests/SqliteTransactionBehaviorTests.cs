using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// Pins the provider behaviour that write serialization relies on, so a package upgrade that
/// changes it fails here rather than surfacing later as lost updates or spurious 503s.
/// </summary>
public class SqliteTransactionBehaviorTests
{
    private const int SqliteBusy = 5;

    // A hard limit: a lock test that waits instead of failing must not hang the suite.
    [Fact(Timeout = 30_000)]
    public async Task Ef_begin_transaction_takes_the_write_lock_before_anything_is_read()
    {
        var ct = TestContext.Current.CancellationToken;
        var file = TestDatabaseDirectory.NewDatabasePath("lock");
        try
        {
            await using var context = new StargateContext(
                new DbContextOptionsBuilder<StargateContext>().UseSqlite($"Data Source={file}").Options);
            await context.Database.MigrateAsync(ct);

            // The same call AssignDutyHandler makes, with no reads or writes yet.
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            // Note: a timeout of 0 means "wait forever" in Microsoft.Data.Sqlite, not "fail fast".
            await using var other = new SqliteConnection($"Data Source={file};Default Timeout=1");
            await other.OpenAsync(ct);
            await using var command = other.CreateCommand();
            command.CommandText = "BEGIN IMMEDIATE;";

            // A deferred BEGIN would hold no lock yet and this would succeed.
            var busy = await Assert.ThrowsAsync<SqliteException>(() => command.ExecuteNonQueryAsync(ct));
            Assert.Equal(SqliteBusy, busy.SqliteErrorCode);
        }
        finally
        {
            TestDatabaseDirectory.DeleteDatabase(file);
        }
    }
}
