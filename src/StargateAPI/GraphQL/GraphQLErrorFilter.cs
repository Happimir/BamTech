using HotChocolate;
using HotChocolate.Execution;
using StargateAPI.Application.Abstractions;
using StargateAPI.Application.Logging;
using StargateAPI.Domain.Results;
using ErrorCodes = StargateAPI.Domain.Results.ErrorCodes;

namespace StargateAPI.GraphQL
{
    /// <summary>
    /// Every GraphQL error passes through here: unexpected exceptions, authorization failures and
    /// document errors (syntax, unknown fields, wrong variable types). Business errors are not
    /// GraphQL errors; they are returned as data (see <see cref="UserErrors"/>).
    /// <list type="bullet">
    /// <item>Exceptions get the same treatment as <c>ApiExceptionHandler</c>: a stable code, a safe
    /// message, and the exception kept for the process log only (never its message).</item>
    /// <item>Authorization codes become the API's own (<c>forbidden</c>).</item>
    /// <item>Every error carries the request's <c>traceId</c>, which finds its process log row.</item>
    /// </list>
    /// </summary>
    public sealed class GraphQLErrorFilter : IErrorFilter
    {
        /// <summary>Hot Chocolate's code when a policy denies the field (role lacks permission).</summary>
        private const string NotAuthorized = "AUTH_NOT_AUTHORIZED";
        private const string NotAuthenticated = "AUTH_NOT_AUTHENTICATED";

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPersistenceErrors _persistenceErrors;
        private readonly ILogger<GraphQLErrorFilter> _logger;

        public GraphQLErrorFilter(
            IHttpContextAccessor httpContextAccessor, IPersistenceErrors persistenceErrors, ILogger<GraphQLErrorFilter> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _persistenceErrors = persistenceErrors;
            _logger = logger;
        }

        public IError OnError(IError error)
        {
            var operation = _httpContextAccessor.HttpContext?.RequestServices.GetService<OperationContext>();
            var builder = ErrorBuilder.FromError(error);

            if (error.Exception is { } exception)
            {
                var (code, message, failed) = Classify(exception);
                if (failed)
                {
                    _logger.LogError(exception, "GraphQL request failed with {Code}", code);
                }
                else
                {
                    _logger.LogInformation(exception, "GraphQL request rejected with {Code}", code);
                }
                if (operation is not null)
                {
                    operation.Exception ??= exception;
                    operation.ErrorCode = code;
                    operation.Failed |= failed;
                }
                builder.ClearExtensions().SetMessage(message).SetCode(code).SetException(null);
            }
            else
            {
                var code = error.Code switch
                {
                    NotAuthorized => "forbidden",
                    NotAuthenticated => "unauthenticated",
                    null => "graphql_error",
                    _ => error.Code
                };
                builder.SetCode(code);
                if (operation is not null)
                {
                    operation.ErrorCode ??= code;
                }
            }

            if (operation is not null)
            {
                builder.SetExtension("traceId", operation.TraceId);
            }
            return builder.Build();
        }

        private (string Code, string Message, bool Failed) Classify(Exception exception) => exception switch
        {
            _ when _persistenceErrors.IsConcurrencyConflict(exception) =>
                (ErrorCodes.ConcurrencyConflict, "The record was changed by another request.", false),
            // Another writer held the database beyond the lock-wait limit. Nothing was written.
            _ when _persistenceErrors.IsBusy(exception) =>
                (ErrorCodes.ServiceUnavailable, "The service is busy. Try again shortly.", true),
            _ => (ErrorCodes.InternalError, "An unexpected error occurred.", true)
        };
    }
}
