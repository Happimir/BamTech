using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>Read routes: name compatibility adapters, ID routes, namesakes and error contracts.</summary>
public class PersonReadTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_duties_by_name_returns_the_duty_history_newest_first()
    {
        // Was a starter defect: the endpoint dispatched GetPersonByName.
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");
        (await api.PostDutyAsync("Grace Hopper", "1LT", "Pilot", "2020-01-01")).EnsureSuccessStatusCode();
        (await api.PostDutyAsync("Grace Hopper", "CPT", "Commander", "2021-03-10")).EnsureSuccessStatusCode();

        var response = await api.Client.GetAsync("/AstronautDuty/Grace%20Hopper", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json.ReadAsync(response);
        var duties = body.GetProperty("duties").EnumerateArray().ToList();
        Assert.Equal(["Commander", "Pilot"], duties.Select(d => d.GetProperty("dutyTitle").GetString()));
        Assert.Equal(JsonValueKind.Null, duties[0].GetProperty("dutyEndDate").ValueKind);
        Assert.Equal("2021-03-09", duties[1].GetProperty("dutyEndDate").GetString());
        Assert.Equal("Commander", body.GetProperty("person").GetProperty("astronaut").GetProperty("currentDutyTitle").GetString());
    }

    [Fact]
    public async Task Name_containing_an_apostrophe_is_treated_as_a_literal_value()
    {
        // Was a starter defect: the name was interpolated into SQL.
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Miles O'Brien");

        var response = await api.Client.GetAsync("/Person/Miles%20O'Brien", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Miles O'Brien", (await Json.ReadAsync(response)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Crafted_name_cannot_select_another_persons_record()
    {
        // Was a starter defect: "' OR 1=1 OR '" rewrote the WHERE clause.
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Alice Example");

        var response = await api.Client.GetAsync("/Person/" + Uri.EscapeDataString("' OR 1=1 OR '"), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Alice Example", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Unknown_person_name_returns_404_problem_details_with_code_and_trace_id()
    {
        // Was a starter defect: 200 with a null person.
        await using var api = await TestApi.CreateAsync();

        var response = await api.Client.GetAsync("/Person/Nobody%20Here", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await Json.ReadAsync(response);
        Assert.Equal("not_found", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Name_lookup_is_case_insensitive_and_trims_but_keeps_accents()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("José Álvarez");

        var matched = await api.Client.GetAsync("/Person/by-name?name=" + Uri.EscapeDataString("  josé álvarez "), Ct);
        var unaccented = await api.Client.GetAsync("/Person/by-name?name=" + Uri.EscapeDataString("Jose Alvarez"), Ct);

        Assert.Equal(HttpStatusCode.OK, matched.StatusCode);
        Assert.Equal("José Álvarez", (await Json.ReadAsync(matched)).GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NotFound, unaccented.StatusCode);
    }

    [Fact]
    public async Task Query_name_route_handles_names_that_cannot_be_path_segments()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("by-name");
        await api.SeedPersonAsync("A/B 100% _x_");

        var reserved = await api.Client.GetAsync("/Person/by-name?name=by-name", Ct);
        var slashed = await api.Client.GetAsync("/Person/by-name?name=" + Uri.EscapeDataString("A/B 100% _x_"), Ct);

        Assert.Equal("by-name", (await Json.ReadAsync(reserved)).GetProperty("name").GetString());
        Assert.Equal("A/B 100% _x_", (await Json.ReadAsync(slashed)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Namesakes_make_name_lookups_ambiguous_and_are_selected_by_id()
    {
        await using var api = await TestApi.CreateAsync();
        var first = await api.SeedPersonAsync("Alex Smith");
        var second = await api.SeedPersonAsync("Alex Smith");

        var byName = await api.Client.GetAsync("/Person/Alex%20Smith", Ct);
        var dutiesByName = await api.Client.GetAsync("/AstronautDuty/Alex%20Smith", Ct);
        var firstById = await api.Client.GetAsync($"/api/people/{first}", Ct);
        var secondDutiesById = await api.Client.GetAsync($"/api/people/{second}/duties", Ct);

        Assert.Equal(HttpStatusCode.Conflict, byName.StatusCode);
        Assert.Equal("ambiguous_name", (await Json.ReadAsync(byName)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, dutiesByName.StatusCode);
        Assert.Equal(first, (await Json.ReadAsync(firstById)).GetProperty("id").GetGuid());
        Assert.Equal(second, (await Json.ReadAsync(secondDutiesById)).GetProperty("person").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Person_without_assignments_has_no_astronaut_summary_and_empty_duties()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Ada Lovelace");

        var response = await api.Client.GetAsync($"/api/people/{id}/duties", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json.ReadAsync(response);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("person").GetProperty("astronaut").ValueKind);
        Assert.Equal(0, body.GetProperty("duties").GetArrayLength());
    }

    [Fact]
    public async Task Unknown_or_malformed_ids_return_404()
    {
        await using var api = await TestApi.CreateAsync();

        var unknown = await api.Client.GetAsync($"/api/people/{Guid.CreateVersion7()}", Ct);
        var unknownDuties = await api.Client.GetAsync($"/api/people/{Guid.CreateVersion7()}/duties", Ct);
        var malformed = await api.Client.GetAsync("/api/people/not-a-guid", Ct);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownDuties.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
    }

    [Fact]
    public async Task Blank_or_oversized_lookup_names_are_rejected_with_400()
    {
        await using var api = await TestApi.CreateAsync();

        var blank = await api.Client.GetAsync("/Person/by-name?name=%20%20", Ct);
        var oversized = await api.Client.GetAsync("/Person/by-name?name=" + new string('a', 301), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal("validation", (await Json.ReadAsync(blank)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
    }

    [Fact]
    public async Task Created_person_gets_a_uuid_v7_identity_and_location()
    {
        await using var api = await TestApi.CreateAsync();

        var response = await api.Client.PostAsJsonAsync("/Person", "Ada Lovelace", Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await Json.ReadAsync(response)).GetProperty("id").GetString()!;
        Assert.Equal('7', id[14]);                  // RFC 9562 version nibble
        Assert.Contains(id[19], "89ab");            // RFC 9562 variant bits 10xx
        Assert.Equal($"/api/people/{id}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Unknown_json_fields_are_rejected_rather_than_ignored()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");

        var response = await api.Client.PostAsJsonAsync("/AstronautDuty", new
        {
            name = "Grace Hopper",
            rank = "1LT",
            dutyTitle = "Pilot",
            dutyStartDate = "2020-01-01",
            dutyEndDate = "2020-12-31"
        }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation", (await Json.ReadAsync(response)).GetProperty("code").GetString());
    }
}
