namespace StargateAPI.Application.Abstractions
{
    /// <summary>
    /// Classifies database exceptions without the caller knowing the provider's error codes.
    /// </summary>
    public interface IPersistenceErrors
    {
        /// <summary>A write violated a unique constraint.</summary>
        bool IsUniqueViolation(Exception exception);

        /// <summary>The database stayed locked by another writer past the wait limit; retry later.</summary>
        bool IsBusy(Exception exception);

        /// <summary>An optimistic concurrency check failed: the row changed since it was read.</summary>
        bool IsConcurrencyConflict(Exception exception);
    }
}
