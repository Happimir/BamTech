
namespace StargateAPI.Application.Logging
{
    /// <summary>Which writer produced a log row. Persisted as a stable string.</summary>
    public enum ProcessLogPhase
    {
        /// <summary>Written by a command handler in the same transaction as the business change.</summary>
        MutationAudit,
        /// <summary>Written once per HTTP request by the boundary middleware, after the response.</summary>
        RequestOutcome
    }

    /// <summary>Persisted as a stable string; changing a value is a data compatibility change.</summary>
    public enum ProcessLogOutcome
    {
        Succeeded,
        Rejected,
        Failed,
        Cancelled
    }

    /// <summary>
    /// Append-only process log (success and exception logging stored in the database). Operational
    /// evidence, not part of the astronaut domain: there are no CRUD endpoints, and PersonId /
    /// AstronautDutyId are historical references without foreign keys, so evidence about a
    /// missing or later-removed record is never lost. Never holds raw payloads, credentials or
    /// exception messages.
    /// </summary>
    public class ProcessLog
    {
        public const int MaxTraceIdLength = 64;
        public const int MaxOperationLength = 128;
        public const int MaxEnumLength = 32;
        public const int MaxActorSubjectLength = 256;
        public const int MaxActorIssuerLength = 512;
        public const int MaxSourceSystemLength = 128;
        public const int MaxErrorCodeLength = 128;
        public const int MaxExceptionTypeLength = 512;
        public const int MaxMessageLength = 2048;
        public const int MaxExceptionDetailsLength = 16000;
        public const int MaxChangesJsonLength = 8000;

        // For EF Core materialization.
        private ProcessLog()
        {
        }

        public Guid Id { get; private set; }
        public DateTimeOffset OccurredAtUtc { get; private set; }
        public Guid OperationId { get; private set; }
        public string TraceId { get; private set; } = string.Empty;
        public string Operation { get; private set; } = string.Empty;
        public ProcessLogPhase Phase { get; private set; }
        public ProcessLogOutcome Outcome { get; private set; }
        public long? DurationMilliseconds { get; private set; }
        public int? HttpStatusCode { get; private set; }
        public string? ActorSubject { get; private set; }
        public string? ActorIssuer { get; private set; }
        public string? SourceSystem { get; private set; }
        public Guid? PersonId { get; private set; }
        public Guid? AstronautDutyId { get; private set; }
        public string? ErrorCode { get; private set; }
        public string? ExceptionType { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public string? ExceptionDetails { get; private set; }
        public string? ChangesJson { get; private set; }

        /// <summary>A committed business change. Only ever Succeeded: a rolled-back change leaves no audit.</summary>
        public static ProcessLog MutationAudit(
            OperationContext operation, string operationName, DateTimeOffset now,
            Guid? personId, Guid? dutyId, string message, string? changesJson = null)
        {
            if (changesJson is { Length: > MaxChangesJsonLength })
            {
                throw new ArgumentException("Audit changes exceed the allowed size.", nameof(changesJson));
            }

            return new ProcessLog
            {
                Id = Guid.CreateVersion7(),
                OccurredAtUtc = now.ToUniversalTime(),
                OperationId = operation.OperationId,
                TraceId = Bound(operation.TraceId, MaxTraceIdLength)!,
                Operation = Bound(operationName, MaxOperationLength)!,
                Phase = ProcessLogPhase.MutationAudit,
                Outcome = ProcessLogOutcome.Succeeded,
                ActorSubject = Bound(operation.ActorSubject, MaxActorSubjectLength),
                ActorIssuer = Bound(operation.ActorIssuer, MaxActorIssuerLength),
                PersonId = personId,
                AstronautDutyId = dutyId,
                Message = Bound(message, MaxMessageLength)!,
                ChangesJson = changesJson
            };
        }

        /// <summary>
        /// The final outcome of one request. Exception type and details are recorded only for a
        /// Failed outcome; HTTP status may be null when it cannot be established.
        /// </summary>
        public static ProcessLog RequestOutcome(
            OperationContext operation, string operationName, DateTimeOffset now, ProcessLogOutcome outcome,
            int? httpStatusCode, long durationMilliseconds, string message, string? exceptionDetails)
        {
            var failed = outcome == ProcessLogOutcome.Failed;
            return new ProcessLog
            {
                Id = Guid.CreateVersion7(),
                OccurredAtUtc = now.ToUniversalTime(),
                OperationId = operation.OperationId,
                TraceId = Bound(operation.TraceId, MaxTraceIdLength)!,
                Operation = Bound(operationName, MaxOperationLength)!,
                Phase = ProcessLogPhase.RequestOutcome,
                Outcome = outcome,
                DurationMilliseconds = Math.Max(0, durationMilliseconds),
                HttpStatusCode = httpStatusCode is >= 100 and <= 599 ? httpStatusCode : null,
                ActorSubject = Bound(operation.ActorSubject, MaxActorSubjectLength),
                ActorIssuer = Bound(operation.ActorIssuer, MaxActorIssuerLength),
                PersonId = operation.PersonId,
                AstronautDutyId = operation.AstronautDutyId,
                ErrorCode = Bound(operation.ErrorCode, MaxErrorCodeLength),
                ExceptionType = failed ? Bound(operation.Exception?.GetType().FullName, MaxExceptionTypeLength) : null,
                Message = Bound(message, MaxMessageLength)!,
                ExceptionDetails = failed ? Bound(exceptionDetails, MaxExceptionDetailsLength) : null
            };
        }

        private static string? Bound(string? value, int maxLength) =>
            value is null || value.Length <= maxLength ? value : value[..maxLength];
    }
}
