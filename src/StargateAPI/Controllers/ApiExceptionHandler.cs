using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StargateAPI.Application.Abstractions;
using StargateAPI.Application.Logging;
using StargateAPI.Domain.Results;

namespace StargateAPI.Controllers
{
    /// <summary>
    /// Converts unhandled exceptions into safe ProblemDetails. Exception messages, SQL and stack
    /// traces are logged server-side only and never returned to clients.
    /// </summary>
    public sealed class ApiExceptionHandler : IExceptionHandler
    {
        private readonly IProblemDetailsService _problemDetails;
        private readonly IPersistenceErrors _persistenceErrors;
        private readonly ILogger<ApiExceptionHandler> _logger;

        public ApiExceptionHandler(
            IProblemDetailsService problemDetails, IPersistenceErrors persistenceErrors, ILogger<ApiExceptionHandler> logger)
        {
            _problemDetails = problemDetails;
            _persistenceErrors = persistenceErrors;
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var (status, code, title) = Classify(exception);

            // Preserve the failure for the request-outcome log even though the client gets a safe body.
            if (httpContext.RequestServices.GetService<OperationContext>() is { } operation)
            {
                operation.ErrorCode = code;
                operation.Exception = exception;
            }

            if (status >= StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(exception, "Request failed with {Code}", code);
            }
            else
            {
                _logger.LogInformation(exception, "Request rejected with {Code}", code);
            }

            httpContext.Response.StatusCode = status;
            return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = status,
                    Title = title,
                    Extensions = { ["code"] = code }
                }
            });
        }

        private (int Status, string Code, string Title) Classify(Exception exception) => exception switch
        {
            BadHttpRequestException badRequest =>
                (badRequest.StatusCode, ErrorCodes.BadRequest, "The request was invalid."),
            _ when _persistenceErrors.IsConcurrencyConflict(exception) =>
                (StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict, "The record was changed by another request."),
            // Another writer held the database beyond the lock-wait limit.
            _ when _persistenceErrors.IsBusy(exception) =>
                (StatusCodes.Status503ServiceUnavailable, ErrorCodes.ServiceUnavailable, "The service is busy. Try again shortly."),
            _ => (StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, "An unexpected error occurred.")
        };
    }
}
