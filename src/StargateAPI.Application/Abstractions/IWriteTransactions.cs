namespace StargateAPI.Application.Abstractions
{
    /// <summary>Starts transactions for read-check-write sequences that must not interleave.</summary>
    public interface IWriteTransactions
    {
        /// <summary>
        /// Begins a transaction that holds the database write lock from the start, so concurrent
        /// writers serialize before reading the state they check. Disposing without committing
        /// rolls everything back.
        /// </summary>
        Task<IWriteTransaction> BeginAsync(CancellationToken cancellationToken);
    }

    public interface IWriteTransaction : IAsyncDisposable
    {
        Task CommitAsync(CancellationToken cancellationToken);
    }
}
