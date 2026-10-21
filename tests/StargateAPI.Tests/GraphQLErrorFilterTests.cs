using HotChocolate;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StargateAPI.Application.Logging;
using StargateAPI.GraphQL;
using StargateAPI.Infrastructure.Persistence;

namespace StargateAPI.Tests;

/// <summary>
/// The GraphQL counterpart of the exception handler: a stable code, no exception text, SQL or
/// stack trace in the error, and the facts the process log needs (code, exception, failed).
/// Uses the real SQLite error classification.
/// </summary>
public class GraphQLErrorFilterTests
{
    private const string SecretDetail = "SELECT * FROM Person -- connection=secret";

    private static (IError Error, OperationContext Operation) Filter(IError error)
    {
        var operation = new OperationContext { TraceId = "00-trace" };
        var services = new ServiceCollection().AddSingleton(operation).BuildServiceProvider();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = services } };
        var filter = new GraphQLErrorFilter(accessor, new SqlitePersistenceErrors(), NullLogger<GraphQLErrorFilter>.Instance);
        return (filter.OnError(error), operation);
    }

    private static IError FromException(Exception exception) =>
        ErrorBuilder.New().SetMessage(exception.Message).SetException(exception).Build();

    [Fact]
    public void Unexpected_exception_is_a_sanitized_internal_error_logged_as_failed()
    {
        var exception = new InvalidOperationException(SecretDetail);

        var (error, operation) = Filter(FromException(exception));

        Assert.Equal("internal_error", error.Code);
        Assert.Equal("An unexpected error occurred.", error.Message);
        Assert.Null(error.Exception);
        Assert.Equal("00-trace", error.Extensions!["traceId"]);
        Assert.DoesNotContain(error.Extensions.Values, v => v?.ToString()?.Contains("secret") == true);
        Assert.Equal(("internal_error", true), (operation.ErrorCode, operation.Failed));
        Assert.Same(exception, operation.Exception);
    }

    [Fact]
    public void Busy_database_is_service_unavailable_so_clients_know_to_retry_later()
    {
        var (error, operation) = Filter(FromException(new DbUpdateException("save failed", new SqliteException(SecretDetail, 5))));

        Assert.Equal("service_unavailable", error.Code);
        Assert.True(operation.Failed);
    }

    [Fact]
    public void Concurrency_exception_is_a_conflict_not_a_failure()
    {
        var (error, operation) = Filter(FromException(new DbUpdateConcurrencyException(SecretDetail)));

        Assert.Equal("concurrency_conflict", error.Code);
        Assert.False(operation.Failed);
    }

    [Fact]
    public void Other_database_errors_are_not_mistaken_for_busy()
    {
        var (error, _) = Filter(FromException(new DbUpdateException("save failed", new SqliteException(SecretDetail, 19))));

        Assert.Equal("internal_error", error.Code);
    }

    [Fact]
    public void A_denied_field_uses_the_apis_forbidden_code()
    {
        var (error, operation) = Filter(ErrorBuilder.New().SetMessage("The current user is not authorized.").SetCode("AUTH_NOT_AUTHORIZED").Build());

        Assert.Equal("forbidden", error.Code);
        Assert.Equal(("forbidden", false), (operation.ErrorCode, operation.Failed));
    }
}
