using Microsoft.Data.Sqlite;

[assembly: AssemblyFixture(typeof(StargateAPI.Tests.Infrastructure.TestDatabaseDirectory))]

namespace StargateAPI.Tests.Infrastructure;

/// <summary>
/// Owns every file-based test database. Each test run gets its own folder under
/// %TEMP%/stargate-tests, deleted when the run ends. On start-up, folders left by runs that
/// crashed or were killed are swept once they are older than an hour, so a concurrent run on
/// the same machine is never touched. In-memory test databases need none of this.
/// </summary>
public sealed class TestDatabaseDirectory : IDisposable
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "stargate-tests");
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(1);

    public static string RunDirectory { get; } =
        Path.Combine(Root, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");

    public TestDatabaseDirectory()
    {
        SweepStaleRuns();
        Directory.CreateDirectory(RunDirectory);
    }

    /// <summary>A new, unique database file path inside this run's folder.</summary>
    public static string NewDatabasePath(string prefix)
    {
        Directory.CreateDirectory(RunDirectory);
        return Path.Combine(RunDirectory, $"{prefix}-{Guid.NewGuid():N}.db");
    }

    /// <summary>
    /// Deletes a test database and its WAL/SHM/journal side files. EF creates SQLite databases in
    /// WAL mode, so the side files are normal. A failure is reported as a test warning, not
    /// swallowed; the end-of-run folder delete and the stale sweep remain as backstops.
    /// </summary>
    public static void DeleteDatabase(string path)
    {
        // Pooled connections keep the file open on Windows.
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { path, path + "-wal", path + "-shm", path + "-journal" })
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TestContext.Current.AddWarning($"Could not delete test database file '{file}': {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        TryDeleteDirectory(RunDirectory);
    }

    private static void SweepStaleRuns()
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(Root))
        {
            if (DateTime.UtcNow - Directory.GetLastWriteTimeUtc(directory) > StaleAfter)
            {
                TryDeleteDirectory(directory);
            }
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Still locked (e.g. by a concurrent run); the next run's sweep retries it.
        }
    }
}
