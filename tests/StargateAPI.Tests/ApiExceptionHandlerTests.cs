using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StargateAPI.Controllers;
using StargateAPI.Infrastructure.Persistence;

namespace StargateAPI.Tests;

/// <summary>
/// The last line of defence for unexpected failures: correct status and stable code, and no
/// exception text, SQL or stack trace in the response body. Uses the real SQLite error
/// classification, so the provider's codes are covered end to end.
/// </summary>
public class ApiExceptionHandlerTests
{
    private const string SecretDetail = "SELECT * FROM Person -- connection=secret";

    private static async Task<(int Status, JsonElement Body)> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        var handler = new ApiExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(),
            new SqlitePersistenceErrors(),
            NullLogger<ApiExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        Assert.True(handled);
        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("SELECT", text);
        Assert.DoesNotContain("secret", text);
        return (context.Response.StatusCode, JsonDocument.Parse(text).RootElement.Clone());
    }

    [Fact]
    public async Task Unexpected_exception_is_a_sanitized_500()
    {
        var (status, body) = await HandleAsync(new InvalidOperationException(SecretDetail));

        Assert.Equal(500, status);
        Assert.Equal("internal_error", body.GetProperty("code").GetString());
        Assert.False(body.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task Busy_database_is_a_503_so_clients_know_to_retry_later()
    {
        var busy = new DbUpdateException("save failed", new SqliteException(SecretDetail, 5));

        var (status, body) = await HandleAsync(busy);

        Assert.Equal(503, status);
        Assert.Equal("service_unavailable", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Concurrency_exception_is_a_409()
    {
        var (status, body) = await HandleAsync(new DbUpdateConcurrencyException(SecretDetail));

        Assert.Equal(409, status);
        Assert.Equal("concurrency_conflict", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Other_database_errors_are_not_mistaken_for_busy()
    {
        var constraint = new DbUpdateException("save failed", new SqliteException(SecretDetail, 19));

        var (status, _) = await HandleAsync(constraint);

        Assert.Equal(500, status);
    }
}
