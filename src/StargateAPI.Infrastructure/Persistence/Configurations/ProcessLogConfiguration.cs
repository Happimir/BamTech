using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StargateAPI.Domain;
using StargateAPI.Application.Logging;

namespace StargateAPI.Infrastructure.Persistence.Configurations
{
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
            builder.ToTable("ProcessLog", t =>
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
