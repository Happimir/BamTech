using Microsoft.EntityFrameworkCore.Storage;
using StargateAPI.Application.Abstractions;

namespace StargateAPI.Infrastructure.Persistence
{
    /// <summary>
    /// Microsoft.Data.Sqlite begins an IMMEDIATE transaction by default, taking the write lock
    /// before anything is read (pinned by SqliteTransactionBehaviorTests). Competing writers
    /// therefore serialize at BeginAsync instead of both acting on the same stale state.
    /// </summary>
    internal sealed class SqliteWriteTransactions(StargateContext context) : IWriteTransactions
    {
        public async Task<IWriteTransaction> BeginAsync(CancellationToken cancellationToken) =>
            new Transaction(await context.Database.BeginTransactionAsync(cancellationToken));

        private sealed class Transaction(IDbContextTransaction inner) : IWriteTransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken) => inner.CommitAsync(cancellationToken);

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
