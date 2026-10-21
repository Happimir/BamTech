using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>Person reads: by-name lookups, ID lookups, namesakes and error contracts.</summary>
public class PersonReadTests
{
    private static async Task<JsonElement> ByNameAsync(TestApi api, string name) =>
        (await api.GraphQLAsync(Operations.PersonByName, new { name }))["personByName"];

    private static string? TypeName(JsonElement result) => result.GetProperty("__typename").GetString();

    [Fact]
    public async Task Duties_by_name_are_the_duty_history_newest_first()
    {
        // Was a starter defect: the endpoint dispatched GetPersonByName.
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");
        await api.AssignDutyByNameAsync("Grace Hopper", "1LT", "Pilot", "2020-01-01");
        await api.AssignDutyByNameAsync("Grace Hopper", "CPT", "Commander", "2021-03-10");

        var person = await ByNameAsync(api, "Grace Hopper");

        Assert.Equal("Person", TypeName(person));
        var duties = person.GetProperty("duties").EnumerateArray().ToList();
        Assert.Equal(["Commander", "Pilot"], duties.Select(d => d.GetProperty("dutyTitle").GetString()));
        Assert.Equal(JsonValueKind.Null, duties[0].GetProperty("dutyEndDate").ValueKind);
        Assert.Equal("2021-03-09", duties[1].GetProperty("dutyEndDate").GetString());
        Assert.Equal("Commander", person.GetProperty("astronaut").GetProperty("currentDutyTitle").GetString());
    }

    [Fact]
    public async Task Name_containing_an_apostrophe_is_treated_as_a_literal_value()
    {
        // Was a starter defect: the name was interpolated into SQL.
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Miles O'Brien");

        var person = await ByNameAsync(api, "Miles O'Brien");

        Assert.Equal("Miles O'Brien", person.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Crafted_name_cannot_select_another_persons_record()
    {
        // Was a starter defect: "' OR 1=1 OR '" rewrote the WHERE clause.
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Alice Example");

        var response = await api.GraphQLAsync(Operations.PersonByName, new { name = "' OR 1=1 OR '" });

        Assert.Equal("NotFoundError", TypeName(response["personByName"]));
        Assert.DoesNotContain("Alice Example", response.Body.GetRawText());
    }

    [Fact]
    public async Task Unknown_person_name_is_a_not_found_error_with_its_code()
    {
        // Was a starter defect: 200 with a null person.
        await using var api = await TestApi.CreateAsync();

        var result = await ByNameAsync(api, "Nobody Here");

        Assert.Equal("NotFoundError", TypeName(result));
        Assert.Equal("not_found", result.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(result.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Name_lookup_is_case_insensitive_and_trims_but_keeps_accents()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("José Álvarez");

        var matched = await ByNameAsync(api, "  josé álvarez ");
        var unaccented = await ByNameAsync(api, "Jose Alvarez");

        Assert.Equal("José Álvarez", matched.GetProperty("name").GetString());
        Assert.Equal("NotFoundError", TypeName(unaccented));
    }

    [Fact]
    public async Task Names_with_slashes_and_wildcards_are_matched_literally()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("A/B 100% _x_");
        await api.SeedPersonAsync("A/B 100X Yx_");

        var person = await ByNameAsync(api, "A/B 100% _x_");

        Assert.Equal("A/B 100% _x_", person.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Namesakes_make_name_lookups_ambiguous_and_are_selected_by_id()
    {
        await using var api = await TestApi.CreateAsync();
        var first = await api.SeedPersonAsync("Alex Smith");
        var second = await api.SeedPersonAsync("Alex Smith");

        var byName = await ByNameAsync(api, "Alex Smith");
        var firstById = (await api.GraphQLAsync(Operations.Person, new { id = first }))["person"];
        var secondById = (await api.GraphQLAsync(Operations.PersonWithDuties, new { id = second }))["person"];

        Assert.Equal("AmbiguousNameError", TypeName(byName));
        Assert.Equal("ambiguous_name", byName.GetProperty("code").GetString());
        Assert.Equal(first, firstById.GetProperty("id").GetGuid());
        Assert.Equal(second, secondById.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Person_without_assignments_has_no_astronaut_summary_and_empty_duties()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Ada Lovelace");

        var person = (await api.GraphQLAsync(Operations.PersonWithDuties, new { id }))["person"];

        Assert.Equal(JsonValueKind.Null, person.GetProperty("astronaut").ValueKind);
        Assert.Equal(0, person.GetProperty("duties").GetArrayLength());
    }

    [Fact]
    public async Task Person_and_duties_are_read_as_one_snapshot_when_both_are_selected()
    {
        await using var api = await TestApi.CreateAsync(configureServices: CommandCounter.Register);
        var id = await api.SeedPersonAsync("Grace Hopper");
        await api.AssignDutyAsync(id, "1LT", "Pilot", "2020-01-01");
        var counter = CommandCounter.For(api);
        counter.Reset();

        var byId = (await api.GraphQLAsync(Operations.PersonWithDuties, new { id }))["person"];
        var byName = await ByNameAsync(api, "Grace Hopper");   // duties inside the result union's Person fragment

        Assert.Equal(1, byId.GetProperty("duties").GetArrayLength());
        Assert.Equal(1, byName.GetProperty("duties").GetArrayLength());
        // One statement per read for the person and history: no separate duties query.
        Assert.Equal(2, counter.Count(sql => sql.Contains("\"AstronautDuty\"")));
    }

    [Fact]
    public async Task Unknown_ids_are_null_and_malformed_ids_are_rejected()
    {
        await using var api = await TestApi.CreateAsync();

        var unknown = await api.GraphQLAsync(Operations.Person, new { id = Guid.CreateVersion7() });
        var malformed = await api.GraphQLAsync(Operations.Person, new { id = "not-a-guid" });

        Assert.False(unknown.HasRequestErrors);
        Assert.Equal(JsonValueKind.Null, unknown["person"].ValueKind);
        Assert.True(malformed.HasRequestErrors);
    }

    [Fact]
    public async Task Blank_or_oversized_lookup_names_are_validation_errors()
    {
        await using var api = await TestApi.CreateAsync();

        var blank = await ByNameAsync(api, "  ");
        var oversized = await ByNameAsync(api, new string('a', 301));

        Assert.Equal("ValidationError", TypeName(blank));
        Assert.Equal("validation", blank.GetProperty("code").GetString());
        Assert.Equal("ValidationError", TypeName(oversized));
    }

    [Fact]
    public async Task Unknown_input_fields_are_rejected_rather_than_ignored()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");

        var response = await api.GraphQLAsync(Operations.AssignDuty, new
        {
            input = new
            {
                person = new { name = "Grace Hopper" },
                rank = "1LT",
                dutyTitle = "Pilot",
                dutyStartDate = "2020-01-01",
                dutyEndDate = "2020-12-31"
            }
        });

        Assert.True(response.HasRequestErrors);
        Assert.Equal(0, await api.WithContextAsync(context => context.AstronautDuties.CountAsync()));
    }
}
