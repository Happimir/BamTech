using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Infrastructure.Persistence;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// Pins the behaviour that write serialization relies on: the use cases' write transaction holds
/// the database write lock from the start. A provider upgrade or implementation change that
/// breaks this fails here rather than surfacing later as lost updates or spurious 503s.
/// </summary>
public class SqliteTransactionBehaviorTests
{
    private const int SqliteBusy = 5;

    // A hard limit: a lock test that waits instead of failing must not hang the suite.
    [Fact(Timeout = 30_000)]
    public async Task Write_transaction_takes_the_write_lock_before_anything_is_read()
    {
        var ct = TestContext.Current.CancellationToken;
        var file = TestDatabaseDirectory.NewDatabasePath("lock");
        try
        {
            await using var context = new StargateContext(
                new DbContextOptionsBuilder<StargateContext>().UseSqlite($"Data Source={file}").Options);
            await context.Database.MigrateAsync(ct);

            // The implementation the command handlers receive, with no reads or writes yet.
            await using var transaction = await new SqliteWriteTransactions(context).BeginAsync(ct);

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
