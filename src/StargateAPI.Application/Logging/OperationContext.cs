using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace StargateAPI.Application.Logging
{
    /// <summary>
    /// Per-request (scoped) facts shared by the HTTP boundary, command handlers and exception
    /// handling, so each log row is written once by its owner with the full picture:
    /// handlers own MutationAudit rows; the request-outcome middleware owns the RequestOutcome row.
    /// </summary>
    public sealed class OperationContext
    {
        /// <summary>Server-generated ID for this execution; a retried request gets a new one.</summary>
        public Guid OperationId { get; } = Guid.CreateVersion7();

        /// <summary>Distributed trace ID; the same value clients see as ProblemDetails "traceId".</summary>
        public string TraceId { get; set; } = Activity.Current?.Id ?? "untraced";

        public Guid? PersonId { get; set; }

        public Guid? AstronautDutyId { get; set; }

        /// <summary>Stable error code of the response, set by whoever produced the error.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>The unhandled exception, if any. Its message is never persisted.</summary>
        public Exception? Exception { get; set; }

        /// <summary>Set by a handler only after its transaction commits.</summary>
        public bool MutationCommitted { get; set; }

        /// <summary>Authenticated subject; populated once authentication exists. Never the selected Person.</summary>
        public string? ActorSubject { get; set; }

        public string? ActorIssuer { get; set; }
    }

    /// <summary>Helpers that keep personal data and exception messages out of log rows.</summary>
    public static class LogContent
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        /// <summary>Serializes an allowlisted change summary (callers pass only safe fields).</summary>
        public static string Changes(object changes) => JsonSerializer.Serialize(changes, JsonOptions);

        /// <summary>
        /// Exception types and stack traces for the whole inner-exception chain, without messages:
        /// messages can carry SQL, connection details or personal data.
        /// </summary>
        public static string ExceptionDiagnostics(Exception exception)
        {
            var builder = new StringBuilder();
            for (var current = exception; current is not null; current = current.InnerException)
            {
                builder.AppendLine(current.GetType().FullName);
                builder.AppendLine(current.StackTrace);
            }
            return builder.ToString();
        }
    }
}
