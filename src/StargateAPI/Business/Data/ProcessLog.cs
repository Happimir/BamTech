using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StargateAPI.Business.Logging;
using System.ComponentModel.DataAnnotations.Schema;

namespace StargateAPI.Business.Data
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
    [Table("ProcessLog")]
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

    public class ProcessLogConfiguration : IEntityTypeConfiguration<ProcessLog>
    {
        public void Configure(EntityTypeBuilder<ProcessLog> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();

            // UTC instants stored as INTEGER Unix milliseconds: reliable ordering and range
            // queries in SQLite, which cannot compare DateTimeOffset values natively.
            builder.Property(x => x.OccurredAtUtc).HasConversion(
                v => v.ToUnixTimeMilliseconds(),
                v => DateTimeOffset.FromUnixTimeMilliseconds(v));

            builder.Property(x => x.TraceId).IsRequired().HasMaxLength(ProcessLog.MaxTraceIdLength);
            builder.Property(x => x.Operation).IsRequired().HasMaxLength(ProcessLog.MaxOperationLength);
            builder.Property(x => x.Phase).HasConversion<string>().HasMaxLength(ProcessLog.MaxEnumLength);
            builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(ProcessLog.MaxEnumLength);
            builder.Property(x => x.ActorSubject).HasMaxLength(ProcessLog.MaxActorSubjectLength);
            builder.Property(x => x.ActorIssuer).HasMaxLength(ProcessLog.MaxActorIssuerLength);
            builder.Property(x => x.SourceSystem).HasMaxLength(ProcessLog.MaxSourceSystemLength);
            builder.Property(x => x.ErrorCode).HasMaxLength(ProcessLog.MaxErrorCodeLength);
            builder.Property(x => x.ExceptionType).HasMaxLength(ProcessLog.MaxExceptionTypeLength);
            builder.Property(x => x.Message).IsRequired().HasMaxLength(ProcessLog.MaxMessageLength);
            builder.Property(x => x.ExceptionDetails).HasMaxLength(ProcessLog.MaxExceptionDetailsLength);
            builder.Property(x => x.ChangesJson).HasMaxLength(ProcessLog.MaxChangesJsonLength);

            builder.HasIndex(x => new { x.OccurredAtUtc, x.Id });
            builder.HasIndex(x => new { x.TraceId, x.OccurredAtUtc });
            // At most one row per phase per execution; backs up the single-writer rule.
            builder.HasIndex(x => new { x.OperationId, x.Phase }).IsUnique();
            builder.HasIndex(x => new { x.PersonId, x.OccurredAtUtc }).HasFilter("\"PersonId\" IS NOT NULL");

            // SQLite does not enforce HasMaxLength, so lengths and enum values are CHECKed.
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ProcessLog_Phase", "\"Phase\" IN ('MutationAudit', 'RequestOutcome')");
                t.HasCheckConstraint("CK_ProcessLog_Outcome", "\"Outcome\" IN ('Succeeded', 'Rejected', 'Failed', 'Cancelled')");
                t.HasCheckConstraint("CK_ProcessLog_Audit_Succeeded",
                    "\"Phase\" <> 'MutationAudit' OR \"Outcome\" = 'Succeeded'");
                t.HasCheckConstraint("CK_ProcessLog_Duration", "\"DurationMilliseconds\" IS NULL OR \"DurationMilliseconds\" >= 0");
                t.HasCheckConstraint("CK_ProcessLog_HttpStatus", "\"HttpStatusCode\" IS NULL OR \"HttpStatusCode\" BETWEEN 100 AND 599");
                t.HasCheckConstraint("CK_ProcessLog_Lengths",
                    $"length(\"TraceId\") <= {ProcessLog.MaxTraceIdLength} " +
                    $"AND length(\"Operation\") <= {ProcessLog.MaxOperationLength} " +
                    $"AND length(\"Message\") <= {ProcessLog.MaxMessageLength} " +
                    $"AND (\"ErrorCode\" IS NULL OR length(\"ErrorCode\") <= {ProcessLog.MaxErrorCodeLength}) " +
                    $"AND (\"ExceptionType\" IS NULL OR length(\"ExceptionType\") <= {ProcessLog.MaxExceptionTypeLength}) " +
                    $"AND (\"ExceptionDetails\" IS NULL OR length(\"ExceptionDetails\") <= {ProcessLog.MaxExceptionDetailsLength}) " +
                    $"AND (\"ChangesJson\" IS NULL OR length(\"ChangesJson\") <= {ProcessLog.MaxChangesJsonLength})");
            });
        }
    }
}
