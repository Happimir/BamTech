using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Domain;
using StargateAPI.Application.Logging;
using StargateAPI.Hosting;
using StargateAPI.Infrastructure.Identity;
using StargateAPI.Infrastructure.Persistence;
using StargateAPI.Security;

namespace StargateAPI.Tests.Infrastructure;

public enum TestUser
{
    Anonymous,
    Admin,
    Reader
}

/// <summary>Copies the XSRF-TOKEN cookie into the X-XSRF-TOKEN header on unsafe methods, as Angular does.</summary>
public sealed class XsrfHeaderHandler(System.Net.CookieContainer cookies) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head && request.Method != HttpMethod.Options
            && cookies.GetCookies(request.RequestUri!)["XSRF-TOKEN"] is { } token
            && !request.Headers.Contains(SecurityServiceCollectionExtensions.XsrfHeaderName))
        {
            request.Headers.Add(SecurityServiceCollectionExtensions.XsrfHeaderName, token.Value);
        }
        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// An isolated API host per test, using the real "StarbaseApiDatabase" connection string so the
/// application's own DbContext wiring is exercised.
/// By default each instance gets a named shared-cache in-memory SQLite database; one connection
/// is held open because SQLite discards an in-memory database when its last connection closes.
/// Concurrency tests use a temporary file database instead: shared-cache memory databases use
/// table-level locking, which is not the locking the application runs with.
/// </summary>
public sealed class TestApi : IAsyncDisposable
{
    public const string AdminPassword = "Admin-Test-Pass-1!";
    public const string ReaderPassword = "Reader-Test-Pass-1!";

    private readonly SqliteConnection? _keepAlive;
    private readonly string? _databaseFile;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly List<HttpClient> _clients = [];

    private TestApi(SqliteConnection? keepAlive, string? databaseFile, WebApplicationFactory<Program> factory)
    {
        _keepAlive = keepAlive;
        _databaseFile = databaseFile;
        _factory = factory;
    }

    /// <summary>The default client, signed in as <see cref="TestUser"/> given to CreateAsync (Admin unless stated).</summary>
    public HttpClient Client { get; private set; } = null!;

    public IServiceProvider Services => _factory.Services;

    /// <summary>
    /// Starts an isolated API with migrated storage and the seeded admin/reader accounts, and a
    /// default client signed in through the real antiforgery + login flow. Process logs written
    /// by that sign-in are cleared so each test sees only its own.
    /// </summary>
    public static async Task<TestApi> CreateAsync(
        bool useFileDatabase = false, Action<IServiceCollection>? configureServices = null, TestUser user = TestUser.Admin,
        string spaRootPath = "")
    {
        SqliteConnection? keepAlive = null;
        string? databaseFile = null;
        string connectionString;
        if (useFileDatabase)
        {
            databaseFile = TestDatabaseDirectory.NewDatabasePath("api");
            connectionString = $"Data Source={databaseFile}";
        }
        else
        {
            connectionString = $"Data Source=stargate-test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            keepAlive = new SqliteConnection(connectionString);
            await keepAlive.OpenAsync();
        }

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:StarbaseApiDatabase", connectionString);
            // UI hosting is off unless a test opts in, so a local web/dist build changes nothing.
            builder.UseSetting(SpaHosting.RootPathSetting, spaRootPath);
            builder.ConfigureTestServices(services =>
            {
                // Tests only: far fewer PBKDF2 iterations keep sign-in fast. Production keeps the default.
                services.Configure<PasswordHasherOptions>(options => options.IterationCount = 1_000);
                configureServices?.Invoke(services);
            });
        });

        var api = new TestApi(keepAlive, databaseFile, factory);
        await api.WithContextAsync(context => context.Database.MigrateAsync());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var errors = await DemoUserSeeder.SeedAsync(scope.ServiceProvider, AdminPassword, ReaderPassword);
            Assert.Empty(errors);
        }

        api.Client = await api.CreateClientAsync(user);
        if (user != TestUser.Anonymous)
        {
            // Sign-in issued two requests (antiforgery, login); start each test with an empty log.
            await api.ProcessLogsAsync(expectedOutcomes: 2);
            await api.WithContextAsync(context => context.ProcessLogs.ExecuteDeleteAsync());
        }
        return api;
    }

    /// <summary>
    /// A new client with its own cookie jar, over https (the auth cookies are Secure). Like the
    /// Angular app, it echoes the XSRF-TOKEN cookie as the X-XSRF-TOKEN header on writes.
    /// </summary>
    public async Task<HttpClient> CreateClientAsync(TestUser user)
    {
        var cookies = new CookieContainerHandler();
        var client = _factory.CreateDefaultClient(new Uri("https://localhost"), new XsrfHeaderHandler(cookies.Container), cookies);
        _clients.Add(client);
        if (user != TestUser.Anonymous)
        {
            var (userName, password) = user == TestUser.Admin
                ? (DemoUserSeeder.AdminUserName, AdminPassword)
                : (DemoUserSeeder.ReaderUserName, ReaderPassword);
            var login = await LoginAsync(client, userName, password);
            login.EnsureSuccessStatusCode();
        }
        return client;
    }

    /// <summary>A client with no cookie jar and no XSRF handling, for sending hand-built requests.</summary>
    public HttpClient CreatePlainClient()
    {
        var client = _factory.CreateDefaultClient(new Uri("https://localhost"));
        _clients.Add(client);
        return client;
    }

    /// <summary>The browser flow: fetch the antiforgery token, then post credentials.</summary>
    public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string userName, string password)
    {
        (await client.GetAsync("/auth/antiforgery")).EnsureSuccessStatusCode();
        return await client.PostAsJsonAsync("/auth/login", new { userName, password });
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

    /// <summary>
    /// All process log rows, oldest first. Waits briefly for the request-outcome row, which the
    /// boundary middleware writes after the response has been produced.
    /// </summary>
    public async Task<List<ProcessLog>> ProcessLogsAsync(int expectedOutcomes)
    {
        for (var attempt = 0; ; attempt++)
        {
            var logs = await WithContextAsync(context => context.ProcessLogs.AsNoTracking()
                .OrderBy(l => l.OccurredAtUtc).ThenBy(l => l.Id).ToListAsync());
            if (logs.Count(l => l.Phase == ProcessLogPhase.RequestOutcome) >= expectedOutcomes || attempt >= 40)
            {
                return logs;
            }
            await Task.Delay(50);
        }
    }

    /// <summary>Posts a duty using the legacy name-based body shape, which the redesign keeps.</summary>
    public Task<HttpResponseMessage> PostDutyAsync(string name, string rank, string dutyTitle, string dutyStartDate) =>
        Client.PostAsJsonAsync("/AstronautDuty", new { name, rank, dutyTitle, dutyStartDate });

    /// <summary>Posts a duty to the stable-ID route.</summary>
    public Task<HttpResponseMessage> PostDutyByIdAsync(Guid personId, string rank, string dutyTitle, string dutyStartDate, Guid? expectedVersion = null) =>
        Client.PostAsJsonAsync($"/api/people/{personId}/duties", new { rank, dutyTitle, dutyStartDate, expectedVersion });

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }
        await _factory.DisposeAsync();
        if (_keepAlive is not null)
        {
            await _keepAlive.DisposeAsync();
        }
        if (_databaseFile is not null)
        {
            TestDatabaseDirectory.DeleteDatabase(_databaseFile);
        }
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
