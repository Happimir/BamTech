using System.Net;
using StargateAPI.Application.Logging;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// The built UI served from the API: deep links reach the Angular shell without a session, the
/// API keeps its own 404s, and page loads never reach the operation log.
/// </summary>
public class SpaHostingTests
{
    private const string Shell = "<!doctype html><title>ACTS test shell</title>";
    private const string Bundle = "console.log('acts');";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Deep_links_and_assets_are_served_without_a_session()
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Anonymous, spaRootPath: CreateUiBuild());

        foreach (var path in new[] { "/", $"/people/{Guid.NewGuid()}", "/login" })
        {
            var page = await api.Client.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Equal(Shell, await page.Content.ReadAsStringAsync(Ct));
            Assert.True(page.Headers.CacheControl?.NoCache, $"{path} should not be cached");
        }

        var asset = await api.Client.GetAsync("/main-TEST.js", Ct);
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Equal(Bundle, await asset.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData("/auth/nope")]
    [InlineData("/graphql/nope")]
    [InlineData("/GraphQL")]
    public async Task Api_paths_keep_their_own_errors_instead_of_the_shell(string path)
    {
        await using var api = await TestApi.CreateAsync(spaRootPath: CreateUiBuild());

        var response = await api.Client.GetAsync(path, Ct);

        // A GET of /graphql outside Development is refused by the GraphQL server (no IDE, no GET operations).
        Assert.False(response.IsSuccessStatusCode, $"{path} returned {(int)response.StatusCode}");
        Assert.NotEqual(Shell, await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Page_loads_are_not_logged_while_api_calls_still_are()
    {
        await using var api = await TestApi.CreateAsync(spaRootPath: CreateUiBuild());
        var id = await api.SeedPersonAsync("Grace Hopper");

        (await api.Client.GetAsync("/", Ct)).EnsureSuccessStatusCode();
        (await api.Client.GetAsync($"/people/{id}", Ct)).EnsureSuccessStatusCode();
        (await api.Client.GetAsync("/main-TEST.js", Ct)).EnsureSuccessStatusCode();
        Assert.False((await api.GraphQLAsync(Operations.Person, new { id })).HasRequestErrors);

        var outcome = Assert.Single(await api.ProcessLogsAsync(expectedOutcomes: 1),
            l => l.Phase == ProcessLogPhase.RequestOutcome);
        Assert.Equal("query person", outcome.Operation);
    }

    [Fact]
    public async Task Hosting_stays_off_without_a_built_index_html()
    {
        var emptyBuild = Directory.CreateDirectory(Path.Combine(TestDatabaseDirectory.RunDirectory, $"ui-{Guid.NewGuid():N}"));
        await using var api = await TestApi.CreateAsync(user: TestUser.Anonymous, spaRootPath: emptyBuild.FullName);

        var response = await api.Client.GetAsync("/people", Ct);

        // As before hosting existed: an unmatched path falls under the signed-in-by-default policy.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A minimal stand-in for `npm run build` output, in this run's temporary folder.</summary>
    private static string CreateUiBuild()
    {
        var root = Directory.CreateDirectory(Path.Combine(TestDatabaseDirectory.RunDirectory, $"ui-{Guid.NewGuid():N}"));
        File.WriteAllText(Path.Combine(root.FullName, "index.html"), Shell);
        File.WriteAllText(Path.Combine(root.FullName, "main-TEST.js"), Bundle);
        return root.FullName;
    }
}
