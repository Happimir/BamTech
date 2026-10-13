using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// Person writes: create by ID contract, version-checked rename, and the legacy name upsert
/// decision table, including the retry and race cases that must never create a duplicate.
/// </summary>
public class PersonWriteApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await Json.ReadAsync(response)).GetProperty("code").GetString();

    private static async Task<JsonElement> GetPersonAsync(TestApi api, Guid id) =>
        await Json.ReadAsync(await api.Client.GetAsync($"/api/people/{id}", Ct));

    private static Task<HttpResponseMessage> UpsertAsync(TestApi api, string lookupName, string name, Guid? expectedVersion = null) =>
        api.Client.PutAsJsonAsync($"/Person/by-name?name={Uri.EscapeDataString(lookupName)}", new { name, expectedVersion }, Ct);

    private static Task<int> CountAsync(TestApi api, string name) =>
        api.WithContextAsync(context => context.People.CountAsync(p => p.Name == name));

    // --- Create and update by ID ---

    [Fact]
    public async Task Create_keeps_explicit_name_parts_and_always_creates_a_new_identity()
    {
        await using var api = await TestApi.CreateAsync();
        var body = new { name = "Alex Smith", firstName = " Alex ", middleName = "   ", lastName = "Smith" };

        var first = await api.Client.PostAsJsonAsync("/api/people", body, Ct);
        var second = await api.Client.PostAsJsonAsync("/api/people", body, Ct);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var created = await Json.ReadAsync(first);
        Assert.Equal("Alex", created.GetProperty("firstName").GetString());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("middleName").ValueKind);
        Assert.NotEqual(created.GetProperty("id").GetGuid(), (await Json.ReadAsync(second)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Create_reports_invalid_fields()
    {
        await using var api = await TestApi.CreateAsync();

        var response = await api.Client.PostAsJsonAsync("/api/people", new { name = " ", lastName = new string('x', 101) }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await Json.ReadAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("lastName", out _));
    }

    [Fact]
    public async Task Rename_by_id_requires_the_current_version_and_keeps_identity_and_history()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Grace Hopper");
        (await api.PostDutyByIdAsync(id, "1LT", "Pilot", "2020-01-01")).EnsureSuccessStatusCode();
        var version = (await GetPersonAsync(api, id)).GetProperty("version").GetGuid();

        var missing = await api.Client.PutAsJsonAsync($"/api/people/{id}", new { name = "Grace B. Hopper" }, Ct);
        var renamed = await api.Client.PutAsJsonAsync($"/api/people/{id}",
            new { name = "Grace B. Hopper", firstName = "Grace", lastName = "Hopper", version }, Ct);
        var stale = await api.Client.PutAsJsonAsync($"/api/people/{id}", new { name = "Someone Else", version }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.True((await Json.ReadAsync(missing)).GetProperty("errors").TryGetProperty("version", out _));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var body = await Json.ReadAsync(renamed);
        Assert.Equal("Grace B. Hopper", body.GetProperty("name").GetString());
        Assert.NotEqual(version, body.GetProperty("version").GetGuid());
        Assert.Equal("Pilot", body.GetProperty("astronaut").GetProperty("currentDutyTitle").GetString());
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", await CodeAsync(stale));
        Assert.Equal("Grace B. Hopper", (await GetPersonAsync(api, id)).GetProperty("name").GetString());
        var duties = await Json.ReadAsync(await api.Client.GetAsync($"/api/people/{id}/duties", Ct));
        Assert.Equal(1, duties.GetProperty("duties").GetArrayLength());
    }

    [Fact]
    public async Task Rename_by_id_replaces_all_parts_and_an_exact_no_op_keeps_the_version()
    {
        await using var api = await TestApi.CreateAsync();
        var created = await Json.ReadAsync(await api.Client.PostAsJsonAsync("/api/people",
            new { name = "Ada Lovelace", firstName = "Ada", lastName = "Lovelace" }, Ct));
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetGuid();

        var noOp = await Json.ReadAsync(await api.Client.PutAsJsonAsync($"/api/people/{id}",
            new { name = " Ada Lovelace ", firstName = "Ada", lastName = "Lovelace", version }, Ct));
        var partsOmitted = await Json.ReadAsync(await api.Client.PutAsJsonAsync($"/api/people/{id}",
            new { name = "Ada Lovelace", version }, Ct));

        Assert.Equal(version, noOp.GetProperty("version").GetGuid());
        Assert.Equal(JsonValueKind.Null, partsOmitted.GetProperty("firstName").ValueKind);
        Assert.NotEqual(version, partsOmitted.GetProperty("version").GetGuid());
    }

    [Fact]
    public async Task Rename_of_an_unknown_id_is_404()
    {
        await using var api = await TestApi.CreateAsync();

        var response = await api.Client.PutAsJsonAsync($"/api/people/{Guid.CreateVersion7()}",
            new { name = "Nobody", version = Guid.NewGuid() }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Legacy add/update by name ---

    [Fact]
    public async Task Upsert_creates_when_no_one_has_the_name()
    {
        await using var api = await TestApi.CreateAsync();

        var response = await UpsertAsync(api, "neo", "Neo");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Json.ReadAsync(response);
        Assert.Equal("Neo", body.GetProperty("name").GetString());
        Assert.Equal($"/api/people/{body.GetProperty("id").GetGuid()}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Upsert_never_creates_from_a_rename_whose_source_is_missing()
    {
        // A retried Alice -> Alicia after the first attempt succeeded must not create a second Alicia.
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Alice");
        var version = (await GetPersonAsync(api, id)).GetProperty("version").GetGuid();

        var first = await UpsertAsync(api, "Alice", "Alicia", version);
        var retry = await UpsertAsync(api, "Alice", "Alicia", version);
        var retryWithoutVersion = await UpsertAsync(api, "Alice", "Alicia");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(id, (await Json.ReadAsync(first)).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, retry.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, retryWithoutVersion.StatusCode);
        Assert.Equal(1, await CountAsync(api, "Alicia"));
        Assert.Equal(1, await api.WithContextAsync(context => context.People.CountAsync()));
    }

    [Fact]
    public async Task Upsert_rename_needs_a_current_version()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Alice");

        var withoutVersion = await UpsertAsync(api, "Alice", "Alicia");
        var stale = await UpsertAsync(api, "Alice", "Alicia", Guid.NewGuid());

        Assert.Equal("version_required", await CodeAsync(withoutVersion));
        Assert.Equal("concurrency_conflict", await CodeAsync(stale));
        Assert.Equal(1, await CountAsync(api, "Alice"));
    }

    [Fact]
    public async Task Upsert_with_an_unchanged_name_is_a_no_op_that_keeps_name_parts()
    {
        await using var api = await TestApi.CreateAsync();
        var created = await Json.ReadAsync(await api.Client.PostAsJsonAsync("/api/people",
            new { name = "Ada Lovelace", firstName = "Ada", lastName = "Lovelace" }, Ct));

        var response = await UpsertAsync(api, "ada lovelace", "Ada Lovelace");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json.ReadAsync(response);
        Assert.Equal(created.GetProperty("version").GetGuid(), body.GetProperty("version").GetGuid());
        Assert.Equal("Ada", body.GetProperty("firstName").GetString());
    }

    [Fact]
    public async Task Upsert_rename_clears_name_parts_that_no_longer_describe_the_name()
    {
        await using var api = await TestApi.CreateAsync();
        var created = await Json.ReadAsync(await api.Client.PostAsJsonAsync("/api/people",
            new { name = "Ada Lovelace", firstName = "Ada", lastName = "Lovelace" }, Ct));

        var response = await UpsertAsync(api, "Ada Lovelace", "Ada King", created.GetProperty("version").GetGuid());

        var body = await Json.ReadAsync(response);
        Assert.Equal("Ada King", body.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("lastName").ValueKind);
    }

    [Fact]
    public async Task Upsert_of_an_ambiguous_name_is_409_and_changes_nothing()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Alex Smith");
        await api.SeedPersonAsync("Alex Smith");

        var response = await UpsertAsync(api, "Alex Smith", "Alex Smith");

        Assert.Equal("ambiguous_name", await CodeAsync(response));
        Assert.Equal(2, await CountAsync(api, "Alex Smith"));
    }

    [Fact]
    public async Task Concurrent_upserts_of_a_missing_name_create_exactly_one_person()
    {
        await using var api = await TestApi.CreateAsync(useFileDatabase: true);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => UpsertAsync(api, "Neo", "Neo")));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, await CountAsync(api, "Neo"));
    }
}
