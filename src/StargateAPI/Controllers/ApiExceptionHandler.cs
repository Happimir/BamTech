using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Results;

namespace StargateAPI.Controllers
{
    /// <summary>
    /// Converts unhandled exceptions into safe ProblemDetails. Exception messages, SQL and stack
    /// traces are logged server-side only and never returned to clients.
    /// </summary>
    public sealed class ApiExceptionHandler : IExceptionHandler
    {
        // SQLITE_BUSY and SQLITE_LOCKED: another writer held the database beyond the timeout.
        private const int SqliteBusy = 5;
        private const int SqliteLocked = 6;

        private readonly IProblemDetailsService _problemDetails;
        private readonly ILogger<ApiExceptionHandler> _logger;

        public ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
        {
            _problemDetails = problemDetails;
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var (status, code, title) = Classify(exception);

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

        private static (int Status, string Code, string Title) Classify(Exception exception) => exception switch
        {
            BadHttpRequestException badRequest =>
                (badRequest.StatusCode, ErrorCodes.BadRequest, "The request was invalid."),
            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict, "The record was changed by another request."),
            _ when IsBusy(exception) =>
                (StatusCodes.Status503ServiceUnavailable, ErrorCodes.ServiceUnavailable, "The service is busy. Try again shortly."),
            _ => (StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, "An unexpected error occurred.")
        };

        private static bool IsBusy(Exception exception) =>
            (exception as SqliteException ?? exception.InnerException as SqliteException) is
            { SqliteErrorCode: SqliteBusy or SqliteLocked };
    }
}
