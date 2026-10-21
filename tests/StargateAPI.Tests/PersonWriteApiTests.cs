using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// Person writes: create, version-checked rename by ID, and the by-name upsert decision table,
/// including the retry and race cases that must never create a duplicate.
/// </summary>
public class PersonWriteApiTests
{
    private static Task<GraphQLResponse> CreateAsync(TestApi api, object input) =>
        api.GraphQLAsync(Operations.CreatePerson, new { input });

    private static Task<GraphQLResponse> UpdateAsync(TestApi api, object input) =>
        api.GraphQLAsync(Operations.UpdatePerson, new { input });

    private static Task<GraphQLResponse> UpsertAsync(TestApi api, string lookupName, string name, Guid? expectedVersion = null) =>
        api.GraphQLAsync(Operations.UpsertPersonByName, new { input = new { lookupName, name, expectedVersion } });

    private static async Task<JsonElement> GetPersonAsync(TestApi api, Guid id) =>
        (await api.GraphQLAsync(Operations.Person, new { id }))["person"];

    private static JsonElement Person(GraphQLResponse response, string field) => response[field].GetProperty("person");

    private static Task<int> CountAsync(TestApi api, string name) =>
        api.WithContextAsync(context => context.People.CountAsync(p => p.Name == name));

    // --- Create and update by ID ---

    [Fact]
    public async Task Create_keeps_explicit_name_parts_and_always_creates_a_new_identity()
    {
        await using var api = await TestApi.CreateAsync();
        var input = new { name = "Alex Smith", firstName = " Alex ", middleName = "   ", lastName = "Smith" };

        var first = await CreateAsync(api, input);
        var second = await CreateAsync(api, input);

        Assert.True(first.Succeeded("createPerson"));
        var created = Person(first, "createPerson");
        Assert.Equal("Alex", created.GetProperty("firstName").GetString());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("middleName").ValueKind);
        Assert.NotEqual(created.GetProperty("id").GetGuid(), Person(second, "createPerson").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Create_reports_invalid_fields()
    {
        await using var api = await TestApi.CreateAsync();

        var response = await CreateAsync(api, new { name = " ", lastName = new string('x', 101) });

        Assert.Equal("ValidationError", response.PayloadErrorType("createPerson"));
        Assert.Equal(["lastName", "name"], response.InvalidFields("createPerson").Order());
        Assert.Equal(0, await api.WithContextAsync(context => context.People.CountAsync()));
    }

    [Fact]
    public async Task Created_person_gets_a_uuid_v7_identity_and_no_astronaut_summary()
    {
        await using var api = await TestApi.CreateAsync();

        var created = Person(await CreateAsync(api, new { name = "Ada Lovelace" }), "createPerson");

        var id = created.GetProperty("id").GetString()!;
        Assert.Equal('7', id[14]);                  // RFC 9562 version nibble
        Assert.Contains(id[19], "89ab");            // RFC 9562 variant bits 10xx
        Assert.Equal(JsonValueKind.Null, created.GetProperty("astronaut").ValueKind);
    }

    [Fact]
    public async Task Rename_by_id_requires_the_current_version_and_keeps_identity_and_history()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Grace Hopper");
        Assert.True((await api.AssignDutyAsync(id, "1LT", "Pilot", "2020-01-01")).Succeeded("assignDuty"));
        var version = await api.VersionAsync(id);

        var missing = await UpdateAsync(api, new { id, name = "Grace B. Hopper" });
        var renamed = await UpdateAsync(api, new { id, name = "Grace B. Hopper", firstName = "Grace", lastName = "Hopper", version });
        var stale = await UpdateAsync(api, new { id, name = "Someone Else", version });

        // The version is required by the schema, so a request without it never runs.
        Assert.True(missing.HasRequestErrors);
        Assert.True(renamed.Succeeded("updatePerson"));
        var body = Person(renamed, "updatePerson");
        Assert.Equal("Grace B. Hopper", body.GetProperty("name").GetString());
        Assert.NotEqual(version, body.GetProperty("version").GetGuid());
        Assert.Equal("Pilot", body.GetProperty("astronaut").GetProperty("currentDutyTitle").GetString());
        Assert.Equal("ConcurrencyConflictError", stale.PayloadErrorType("updatePerson"));
        Assert.Equal("concurrency_conflict", stale.PayloadErrorCode("updatePerson"));
        Assert.Equal("Grace B. Hopper", (await GetPersonAsync(api, id)).GetProperty("name").GetString());
        var duties = (await api.GraphQLAsync(Operations.PersonWithDuties, new { id }))["person"].GetProperty("duties");
        Assert.Equal(1, duties.GetArrayLength());
    }

    [Fact]
    public async Task Rename_by_id_replaces_all_parts_and_an_exact_no_op_keeps_the_version()
    {
        await using var api = await TestApi.CreateAsync();
        var created = Person(await CreateAsync(api, new { name = "Ada Lovelace", firstName = "Ada", lastName = "Lovelace" }), "createPerson");
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetGuid();

        var noOp = Person(await UpdateAsync(api, new { id, name = " Ada Lovelace ", firstName = "Ada", lastName = "Lovelace", version }), "updatePerson");
        var partsOmitted = Person(await UpdateAsync(api, new { id, name = "Ada Lovelace", version }), "updatePerson");

        Assert.Equal(version, noOp.GetProperty("version").GetGuid());
        Assert.Equal(JsonValueKind.Null, partsOmitted.GetProperty("firstName").ValueKind);
        Assert.NotEqual(version, partsOmitted.GetProperty("version").GetGuid());
    }

    [Fact]
    public async Task Rename_of_an_unknown_id_is_a_not_found_error()
    {
        await using var api = await TestApi.CreateAsync();

        var response = await UpdateAsync(api, new { id = Guid.CreateVersion7(), name = "Nobody", version = Guid.NewGuid() });

        Assert.Equal("NotFoundError", response.PayloadErrorType("updatePerson"));
    }

    // --- Add/update by name ---

    [Fact]
    public async Task Upsert_creates_when_no_one_has_the_name()
    {
        await using var api = await TestApi.CreateAsync();

        var response = await UpsertAsync(api, "neo", "Neo");

        Assert.True(response.Succeeded("upsertPersonByName"));
        Assert.True(response["upsertPersonByName"].GetProperty("created").GetBoolean());
        Assert.Equal("Neo", Person(response, "upsertPersonByName").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Upsert_never_creates_from_a_rename_whose_source_is_missing()
    {
        // A retried Alice -> Alicia after the first attempt succeeded must not create a second Alicia.
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Alice");
        var version = await api.VersionAsync(id);

        var first = await UpsertAsync(api, "Alice", "Alicia", version);
        var retry = await UpsertAsync(api, "Alice", "Alicia", version);
        var retryWithoutVersion = await UpsertAsync(api, "Alice", "Alicia");

        Assert.True(first.Succeeded("upsertPersonByName"));
        Assert.False(first["upsertPersonByName"].GetProperty("created").GetBoolean());
        Assert.Equal(id, Person(first, "upsertPersonByName").GetProperty("id").GetGuid());
        Assert.Equal("NotFoundError", retry.PayloadErrorType("upsertPersonByName"));
        Assert.Equal("NotFoundError", retryWithoutVersion.PayloadErrorType("upsertPersonByName"));
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

        Assert.Equal("VersionRequiredError", withoutVersion.PayloadErrorType("upsertPersonByName"));
        Assert.Equal("version_required", withoutVersion.PayloadErrorCode("upsertPersonByName"));
        Assert.Equal("concurrency_conflict", stale.PayloadErrorCode("upsertPersonByName"));
        Assert.Equal(1, await CountAsync(api, "Alice"));
    }

    [Fact]
    public async Task Resending_a_name_in_another_unicode_form_is_not_a_rename()
    {
        // "é" precomposed, or "e" plus a combining accent: the same name to every reader.
        const string composed = "José Ortega";
        const string decomposed = "José Ortega";
        await using var api = await TestApi.CreateAsync();
        var created = Person(await CreateAsync(api, new { name = decomposed, firstName = "José" }), "createPerson");
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetGuid();

        // Re-sent exactly as first sent (decomposed), which now differs from the stored spelling.
        var byName = await UpsertAsync(api, decomposed, decomposed);
        var byId = await UpdateAsync(api, new { id, name = decomposed, firstName = "José", version });

        // Stored in the canonical (NFC) form.
        Assert.Equal(composed, created.GetProperty("name").GetString());
        // Neither operation treats it as a change: no version needed, none consumed, parts kept.
        Assert.True(byName.Succeeded("upsertPersonByName"));
        Assert.True(byId.Succeeded("updatePerson"));
        var after = await GetPersonAsync(api, id);
        Assert.Equal(version, after.GetProperty("version").GetGuid());
        Assert.Equal("José", after.GetProperty("firstName").GetString());
    }

    [Fact]
    public async Task Upsert_with_an_unchanged_name_is_a_no_op_that_keeps_name_parts()
    {
        await using var api = await TestApi.CreateAsync();
        var created = Person(await CreateAsync(api, new { name = "Ada Lovelace", firstName = "Ada", lastName = "Lovelace" }), "createPerson");

        var response = await UpsertAsync(api, "ada lovelace", "Ada Lovelace");

        var body = Person(response, "upsertPersonByName");
        Assert.Equal(created.GetProperty("version").GetGuid(), body.GetProperty("version").GetGuid());
        Assert.Equal("Ada", body.GetProperty("firstName").GetString());
    }

    [Fact]
    public async Task Upsert_rename_clears_name_parts_that_no_longer_describe_the_name()
    {
        await using var api = await TestApi.CreateAsync();
        var created = Person(await CreateAsync(api, new { name = "Ada Lovelace", firstName = "Ada", lastName = "Lovelace" }), "createPerson");

        var response = await UpsertAsync(api, "Ada Lovelace", "Ada King", created.GetProperty("version").GetGuid());

        var body = Person(response, "upsertPersonByName");
        Assert.Equal("Ada King", body.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("lastName").ValueKind);
    }

    [Fact]
    public async Task Upsert_of_an_ambiguous_name_is_refused_and_changes_nothing()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Alex Smith");
        await api.SeedPersonAsync("Alex Smith");

        var response = await UpsertAsync(api, "Alex Smith", "Alex Smith");

        Assert.Equal("ambiguous_name", response.PayloadErrorCode("upsertPersonByName"));
        Assert.Equal(2, await CountAsync(api, "Alex Smith"));
    }

    [Fact]
    public async Task Concurrent_upserts_of_a_missing_name_create_exactly_one_person()
    {
        await using var api = await TestApi.CreateAsync(useFileDatabase: true);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => UpsertAsync(api, "Neo", "Neo")));

        Assert.All(responses, r => Assert.True(r.Succeeded("upsertPersonByName")));
        Assert.Single(responses, r => r["upsertPersonByName"].GetProperty("created").GetBoolean());
        Assert.Equal(1, await CountAsync(api, "Neo"));
    }
}
