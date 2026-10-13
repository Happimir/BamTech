using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Business.Data;

namespace StargateAPI.Tests.Infrastructure;

/// <summary>
/// An isolated API host per test. Each instance gets its own named shared-cache in-memory
/// SQLite database, supplied through the real "StarbaseApiDatabase" connection string so the
/// application's own DbContext wiring is exercised. One connection is held open for the
/// lifetime of the instance because SQLite discards an in-memory database when its last
/// connection closes.
/// </summary>
public sealed class TestApi : IAsyncDisposable
{
    private readonly SqliteConnection _keepAlive;
    private readonly WebApplicationFactory<Program> _factory;

    private TestApi(SqliteConnection keepAlive, WebApplicationFactory<Program> factory)
    {
        _keepAlive = keepAlive;
        _factory = factory;
        Client = factory.CreateClient();
    }

    public HttpClient Client { get; }

    public IServiceProvider Services => _factory.Services;

    public static async Task<TestApi> CreateAsync()
    {
        var connectionString = $"Data Source=stargate-test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:StarbaseApiDatabase", connectionString);
        });

        var api = new TestApi(keepAlive, factory);
        await api.WithContextAsync(context => context.Database.MigrateAsync());
        return api;
    }

    public async Task WithContextAsync(Func<StargateContext, Task> action)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<StargateContext>());
    }

    public async Task<T> WithContextAsync<T>(Func<StargateContext, Task<T>> action)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<StargateContext>());
    }

    /// <summary>Seeds a person directly, independent of the API's create endpoint.</summary>
    public Task<Guid> SeedPersonAsync(string name) => WithContextAsync(async context =>
    {
        var person = Person.Create(name);
        context.People.Add(person);
        await context.SaveChangesAsync();
        return person.Id;
    });

    /// <summary>Posts a duty using the legacy name-based body shape, which the redesign keeps.</summary>
    public Task<HttpResponseMessage> PostDutyAsync(string name, string rank, string dutyTitle, string dutyStartDate) =>
        Client.PostAsJsonAsync("/AstronautDuty", new { name, rank, dutyTitle, dutyStartDate });

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
        await _keepAlive.DisposeAsync();
    }
}

/// <summary>Response helpers that do not depend on a particular envelope shape.</summary>
public static class Json
{
    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    /// <summary>All string values of properties named <paramref name="propertyName"/>, at any depth.</summary>
    public static IReadOnlyList<string> FindStrings(JsonElement element, string propertyName)
    {
        var found = new List<string>();
        Walk(element);
        return found;

        void Walk(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in e.EnumerateObject())
                    {
                        if (property.NameEquals(propertyName) && property.Value.ValueKind == JsonValueKind.String)
                        {
                            found.Add(property.Value.GetString()!);
                        }
                        Walk(property.Value);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in e.EnumerateArray())
                    {
                        Walk(item);
                    }
                    break;
            }
        }
    }
}
