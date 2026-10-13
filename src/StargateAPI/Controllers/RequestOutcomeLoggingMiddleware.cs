using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Business.Logging;

namespace StargateAPI.Controllers
{
    /// <summary>
    /// The single writer of RequestOutcome rows: one per request, after the response has been
    /// produced, so it sees validation, not-found, conflict and unhandled-exception outcomes alike.
    /// Registered outside exception handling. Logging is best effort: a failure to persist is
    /// reported through ILogger and never changes the response or masks the original outcome.
    /// </summary>
    public sealed class RequestOutcomeLoggingMiddleware
    {
        // Documentation and business-calendar metadata are excluded to avoid log noise.
        private static readonly PathString[] ExcludedPaths = ["/swagger", "/api/metadata"];
        private static readonly TimeSpan LogWriteTimeout = TimeSpan.FromSeconds(2);

        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RequestOutcomeLoggingMiddleware> _logger;

        public RequestOutcomeLoggingMiddleware(
            RequestDelegate next, IServiceScopeFactory scopeFactory, TimeProvider timeProvider,
            ILogger<RequestOutcomeLoggingMiddleware> logger)
        {
            _next = next;
            _scopeFactory = scopeFactory;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext httpContext, OperationContext operation)
        {
            if (ExcludedPaths.Any(path => httpContext.Request.Path.StartsWithSegments(path)))
            {
                await _next(httpContext);
                return;
            }

            operation.TraceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
            // Captured now: exception handling clears the endpoint before re-executing.
            var operationName = OperationName(httpContext);
            var started = _timeProvider.GetTimestamp();
            var escaped = false;
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                escaped = true;
                operation.Exception ??= ex;
                throw;
            }
            finally
            {
                await WriteOutcomeAsync(httpContext, operation, operationName, _timeProvider.GetElapsedTime(started), escaped);
            }
        }

        private async Task WriteOutcomeAsync(
            HttpContext httpContext, OperationContext operation, string operationName, TimeSpan elapsed, bool escaped)
        {
            try
            {
                var status = httpContext.Response.StatusCode;
                ProcessLogOutcome outcome;
                if (httpContext.RequestAborted.IsCancellationRequested)
                {
                    outcome = ProcessLogOutcome.Cancelled;
                    operation.ErrorCode ??= operation.MutationCommitted ? "response_interrupted" : "cancelled";
                }
                else if (escaped)
                {
                    outcome = ProcessLogOutcome.Failed;
                    operation.ErrorCode = operation.MutationCommitted ? "response_failed" : "internal_error";
                    if (!httpContext.Response.HasStarted)
                    {
                        status = StatusCodes.Status500InternalServerError;
                    }
                }
                else
                {
                    outcome = status switch
                    {
                        >= 500 => ProcessLogOutcome.Failed,
                        >= 400 => ProcessLogOutcome.Rejected,
                        _ => ProcessLogOutcome.Succeeded
                    };
                    if (outcome != ProcessLogOutcome.Succeeded)
                    {
                        operation.ErrorCode ??= $"http_{status}";
                    }
                }

                var message = $"{httpContext.Request.Method} {operationName} {outcome.ToString().ToLowerInvariant()} with status {status}."
                    + (operation.MutationCommitted && outcome != ProcessLogOutcome.Succeeded
                        ? " The mutation was committed before the response failed."
                        : string.Empty);
                var log = ProcessLog.RequestOutcome(
                    operation, operationName, _timeProvider.GetUtcNow(), outcome, status,
                    (long)elapsed.TotalMilliseconds, message,
                    operation.Exception is { } exception ? LogContent.ExceptionDiagnostics(exception) : null);

                // A fresh scope and context: no pending business changes can leak into this save,
                // and the request's cancellation token is deliberately not used.
                using var timeout = new CancellationTokenSource(LogWriteTimeout);
                await using var scope = _scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
                context.Database.SetCommandTimeout(LogWriteTimeout);
                context.ProcessLogs.Add(log);
                await context.SaveChangesAsync(timeout.Token);
            }
            catch (Exception ex)
            {
                // The database may be the thing that failed; fall back to the application log.
                _logger.LogWarning(ex,
                    "Could not persist request outcome for operation {OperationId} ({Operation}, trace {TraceId}).",
                    operation.OperationId, operationName, operation.TraceId);
            }
        }

        /// <summary>An allowlisted name from endpoint metadata, never the raw URL (which may contain names).</summary>
        private static string OperationName(HttpContext httpContext) =>
            httpContext.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>() is { } action
                ? $"{action.ControllerName}.{action.ActionName}"
                : "Unmatched";
    }
}
