using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Application.Abstractions;

namespace StargateAPI.Infrastructure.Persistence
{
    /// <summary>SQLite's error codes behind the provider-neutral classification the app uses.</summary>
    internal sealed class SqlitePersistenceErrors : IPersistenceErrors
    {
        private const int SqliteBusy = 5;
        private const int SqliteLocked = 6;
        private const int SqliteConstraintUnique = 2067;

        public bool IsUniqueViolation(Exception exception) =>
            FindSqliteException(exception) is { SqliteExtendedErrorCode: SqliteConstraintUnique };

        public bool IsBusy(Exception exception) =>
            FindSqliteException(exception) is { SqliteErrorCode: SqliteBusy or SqliteLocked };

        public bool IsConcurrencyConflict(Exception exception) => exception is DbUpdateConcurrencyException;

        private static SqliteException? FindSqliteException(Exception exception) =>
            exception as SqliteException ?? exception.InnerException as SqliteException;
    }
}
