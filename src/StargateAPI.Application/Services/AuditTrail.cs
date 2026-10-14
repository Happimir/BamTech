using StargateAPI.Application.Abstractions;
using StargateAPI.Application.Logging;

namespace StargateAPI.Application.Services
{
    /// <summary>The success audit for a write: one MutationAudit row per committed change.</summary>
    public interface IAuditTrail
    {
        /// <summary>
        /// Stages the audit row for the current operation. It is saved by the caller's next
        /// SaveChanges, so it commits with the change or not at all. <paramref name="changes"/> is
        /// serialized as JSON and must hold field names or derived facts, never personal values.
        /// </summary>
        void Record(string operationName, Guid personId, Guid? dutyId, string message, object? changes = null);
    }

    public sealed class AuditTrail : IAuditTrail
    {
        private readonly IStargateDbContext _context;
        private readonly TimeProvider _timeProvider;
        private readonly OperationContext _operation;

        public AuditTrail(IStargateDbContext context, TimeProvider timeProvider, OperationContext operation)
        {
            _context = context;
            _timeProvider = timeProvider;
            _operation = operation;
        }

        public void Record(string operationName, Guid personId, Guid? dutyId, string message, object? changes = null)
        {
            _context.ProcessLogs.Add(ProcessLog.MutationAudit(
                _operation, operationName, _timeProvider.GetUtcNow(), personId, dutyId, message,
                changes is null ? null : LogContent.Changes(changes)));
        }
    }
}
