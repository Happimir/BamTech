using System.Net;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// Each test asserts the target behaviour from the design and is expected to FAIL against the
/// supplied starter, documenting a defect from the API review. They turn green as the required
/// API work fixes them. Run the passing baseline alone with: --filter "Category!=KnownDefect".
/// </summary>
[Trait("Category", "KnownDefect")]
public class KnownDefectRegressionTests
{
    [Fact]
    public async Task Get_duties_by_name_returns_the_duty_history()
    {
        // Starter: AstronautDutyController.GetAstronautDutiesByName dispatches GetPersonByName.
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");
        (await api.PostDutyAsync("Grace Hopper", "1LT", "Pilot", "2020-01-01")).EnsureSuccessStatusCode();
        (await api.PostDutyAsync("Grace Hopper", "CPT", "Commander", "2021-03-10")).EnsureSuccessStatusCode();

        var response = await api.Client.GetAsync("/AstronautDuty/Grace%20Hopper");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var titles = Json.FindStrings(await Json.ReadAsync(response), "dutyTitle");
        Assert.Contains("Pilot", titles);
        Assert.Contains("Commander", titles);
    }

    [Fact]
    public async Task Name_containing_an_apostrophe_is_treated_as_a_literal_value()
    {
        // Starter: GetPersonByName interpolates the name into SQL, so the quote breaks the query.
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Miles O'Brien");

        var response = await api.Client.GetAsync("/Person/Miles%20O'Brien");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Miles O'Brien", Json.FindStrings(await Json.ReadAsync(response), "name"));
    }

    [Fact]
    public async Task Crafted_name_cannot_select_another_persons_record()
    {
        // Starter: "' OR 1=1 OR '" rewrites the WHERE clause and returns the first person.
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Alice Example");

        var response = await api.Client.GetAsync("/Person/" + Uri.EscapeDataString("' OR 1=1 OR '"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Alice Example", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_person_name_returns_404()
    {
        // Starter: returns 200 with a null person inside a "successful" envelope.
        await using var api = await TestApi.CreateAsync();

        var response = await api.Client.GetAsync("/Person/Nobody%20Here");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Duty_for_a_missing_person_returns_404()
    {
        // Starter: the preprocessor throws BadHttpRequestException; the handler would dereference null.
        await using var api = await TestApi.CreateAsync();

        var response = await api.PostDutyAsync("Nobody Here", "1LT", "Pilot", "2020-01-01");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Another_persons_duty_with_same_title_and_date_does_not_block_assignment()
    {
        // Starter: the duty preprocessor checks title + start date across ALL people.
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");
        await api.SeedPersonAsync("Sally Ride");
        (await api.PostDutyAsync("Grace Hopper", "1LT", "Pilot", "2020-01-01")).EnsureSuccessStatusCode();

        var response = await api.PostDutyAsync("Sally Ride", "1LT", "Pilot", "2020-01-01");

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task First_duty_cannot_be_a_retirement()
    {
        // Starter: creates the detail with CareerEndDate == start date, violating rule 7
        // (career end is the day before retirement) and implying career end before career start.
        // Documented policy: reject with 409 first_duty_retirement and write nothing.
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Sally Ride");

        var response = await api.PostDutyAsync("Sally Ride", "1LT", "RETIRED", "2020-01-01");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var (details, duties) = await api.WithContextAsync(async context =>
            (await context.AstronautDetails.CountAsync(), await context.AstronautDuties.CountAsync()));
        Assert.Equal(0, details);
        Assert.Equal(0, duties);
    }

    [Fact]
    public async Task Earlier_start_date_is_rejected_without_changing_history()
    {
        // Starter: closes the latest duty regardless of chronology, producing an end before its start.
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");
        (await api.PostDutyAsync("Grace Hopper", "1LT", "Pilot", "2020-01-01")).EnsureSuccessStatusCode();

        var response = await api.PostDutyAsync("Grace Hopper", "CPT", "Commander", "2019-06-01");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var duties = await api.WithContextAsync(context => context.AstronautDuties.ToListAsync());
        var duty = Assert.Single(duties);
        Assert.Null(duty.DutyEndDate);
    }

    [Fact]
    public async Task Repeated_start_date_is_a_chronology_conflict()
    {
        // Starter: the cross-person preprocessor throws BadHttpRequestException, surfacing as an
        // unhandled exception. Documented policy: 409 chronology_conflict, no extra duty written.
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");
        (await api.PostDutyAsync("Grace Hopper", "1LT", "Pilot", "2020-01-01")).EnsureSuccessStatusCode();

        var response = await api.PostDutyAsync("Grace Hopper", "1LT", "Pilot", "2020-01-01");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await api.WithContextAsync(context => context.AstronautDuties.CountAsync()));
    }
}
