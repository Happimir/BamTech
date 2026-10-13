using StargateAPI.Application.Logging;

namespace StargateAPI.Application.Abstractions
{
    /// <summary>Stores request-outcome log rows.</summary>
    public interface IRequestOutcomeLog
    {
        /// <summary>
        /// Saves one row in its own unit of work, independent of the request's (possibly failed)
        /// database context, within <paramref name="timeout"/>. Throws on failure; callers decide
        /// whether that matters.
        /// </summary>
        Task WriteAsync(ProcessLog entry, TimeSpan timeout, CancellationToken cancellationToken);
    }
}
