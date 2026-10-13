using System.Net;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// Duty appends through HTTP and the real database: status/error mapping, persistence of the
/// whole transition, no partial writes on rejection, and serialization of concurrent writers.
/// The transition rules themselves are covered by the domain tests.
/// </summary>
public class DutyAssignmentApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await Json.ReadAsync(response)).GetProperty("code").GetString();

    private static Task<int> DutyCountAsync(TestApi api) =>
        api.WithContextAsync(context => context.AstronautDuties.CountAsync());

    // --- Former starter defects (were KnownDefectRegressionTests) ---

    [Fact]
    public async Task Duty_for_a_missing_person_returns_404()
    {
        await using var api = await TestApi.CreateAsync();

        var byName = await api.PostDutyAsync("Nobody Here", "1LT", "Pilot", "2020-01-01");
        var byId = await api.PostDutyByIdAsync(Guid.CreateVersion7(), "1LT", "Pilot", "2020-01-01");

        Assert.Equal(HttpStatusCode.NotFound, byName.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, byId.StatusCode);
    }

    [Fact]
    public async Task Another_persons_duty_with_same_title_and_date_does_not_block_assignment()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");
        await api.SeedPersonAsync("Sally Ride");
        (await api.PostDutyAsync("Grace Hopper", "1LT", "Pilot", "2020-01-01")).EnsureSuccessStatusCode();

        var response = await api.PostDutyAsync("Sally Ride", "1LT", "Pilot", "2020-01-01");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task First_duty_retirement_is_409_and_writes_nothing()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Sally Ride");

        var response = await api.PostDutyAsync("Sally Ride", "1LT", "RETIRED", "2020-01-01");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("first_duty_retirement", await CodeAsync(response));
        Assert.Equal(0, await api.WithContextAsync(context => context.AstronautDetails.CountAsync()));
        Assert.Equal(0, await DutyCountAsync(api));
    }

    [Theory]
    [InlineData("2019-06-01")]   // earlier: the starter inverted history
    [InlineData("2020-01-01")]   // exact repeat: no replay recognition in this version
    public async Task Same_or_earlier_start_is_409_and_history_is_unchanged(string start)
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");
        (await api.PostDutyAsync("Grace Hopper", "1LT", "Pilot", "2020-01-01")).EnsureSuccessStatusCode();

        var response = await api.PostDutyAsync("Grace Hopper", "CPT", "Commander", start);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("chronology_conflict", await CodeAsync(response));
        var duty = Assert.Single(await api.WithContextAsync(context => context.AstronautDuties.ToListAsync()));
        Assert.Null(duty.DutyEndDate);
    }

    // --- Contract and persistence ---

    [Fact]
    public async Task Created_duty_location_resolves_and_rejects_another_persons_id()
    {
        await using var api = await TestApi.CreateAsync();
        var grace = await api.SeedPersonAsync("Grace Hopper");
        var sally = await api.SeedPersonAsync("Sally Ride");

        var created = await api.PostDutyByIdAsync(grace, "1LT", "Pilot", "2020-01-01");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await Json.ReadAsync(created);
        var dutyId = body.GetProperty("id").GetGuid();
        Assert.Equal(grace, body.GetProperty("personId").GetGuid());
        Assert.Equal($"/api/people/{grace}/duties/{dutyId}", created.Headers.Location?.OriginalString);

        var followed = await api.Client.GetAsync(created.Headers.Location, Ct);
        var mismatched = await api.Client.GetAsync($"/api/people/{sally}/duties/{dutyId}", Ct);

        Assert.Equal("Pilot", (await Json.ReadAsync(followed)).GetProperty("dutyTitle").GetString());
        Assert.Equal(HttpStatusCode.NotFound, mismatched.StatusCode);
    }

    [Fact]
    public async Task Full_career_cycle_persists_history_and_current_summary()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Sally Ride");

        foreach (var (rank, title, start) in new[]
        {
            ("1LT", "Pilot", "2020-01-01"),
            ("CPT", " retired ", "2025-01-01"),
            ("MAJ", "Commander", "2026-01-01")
        })
        {
            Assert.Equal(HttpStatusCode.Created, (await api.PostDutyByIdAsync(id, rank, title, start)).StatusCode);
        }

        var body = await Json.ReadAsync(await api.Client.GetAsync($"/api/people/{id}/duties", Ct));
        var astronaut = body.GetProperty("person").GetProperty("astronaut");
        Assert.Equal("Commander", astronaut.GetProperty("currentDutyTitle").GetString());
        Assert.Equal("2026-01-01", astronaut.GetProperty("currentDutyStartDate").GetString());
        Assert.Equal("2020-01-01", astronaut.GetProperty("careerStartDate").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, astronaut.GetProperty("careerEndDate").ValueKind);
        var history = body.GetProperty("duties").EnumerateArray()
            .Select(d => $"{d.GetProperty("dutyTitle").GetString()} {d.GetProperty("dutyStartDate").GetString()}..{d.GetProperty("dutyEndDate")}")
            .ToList();
        Assert.Equal(
        [
            "Commander 2026-01-01..",
            "RETIRED 2025-01-01..2025-12-31",
            "Pilot 2020-01-01..2024-12-31"
        ], history);
    }

    [Fact]
    public async Task Stale_expected_version_is_409_and_the_returned_version_allows_the_next_write()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Grace Hopper");
        var original = (await Json.ReadAsync(await api.Client.GetAsync($"/api/people/{id}", Ct))).GetProperty("version").GetGuid();

        var first = await api.PostDutyByIdAsync(id, "1LT", "Pilot", "2020-01-01", original);
        var newVersion = (await Json.ReadAsync(first)).GetProperty("version").GetGuid();
        var stale = await api.PostDutyByIdAsync(id, "CPT", "Commander", "2021-01-01", original);
        var current = await api.PostDutyByIdAsync(id, "CPT", "Commander", "2021-01-01", newVersion);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.NotEqual(original, newVersion);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", await CodeAsync(stale));
        Assert.Equal(HttpStatusCode.Created, current.StatusCode);
        Assert.Equal(2, await DutyCountAsync(api));
    }

    [Fact]
    public async Task Legacy_route_rejects_an_ambiguous_name_before_writing()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Alex Smith");
        await api.SeedPersonAsync("Alex Smith");

        var response = await api.PostDutyAsync("Alex Smith", "1LT", "Pilot", "2020-01-01");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ambiguous_name", await CodeAsync(response));
        Assert.Equal(0, await DutyCountAsync(api));
    }

    [Fact]
    public async Task Invalid_input_is_400_with_field_errors_and_writes_nothing()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Grace Hopper");

        var missing = await api.PostDutyByIdAsync(id, " ", "Pilot", null!);
        var future = await api.PostDutyByIdAsync(id, "1LT", "Pilot", "2999-01-01");
        var malformed = await api.PostDutyByIdAsync(id, "1LT", "Pilot", "2020-13-45");

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var errors = (await Json.ReadAsync(missing)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("rank", out _));
        Assert.True(errors.TryGetProperty("dutyStartDate", out _));
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(0, await DutyCountAsync(api));
    }

    // --- Concurrency (file database: real SQLite locking, separate connections) ---

    [Fact]
    public async Task Concurrent_identical_duties_create_exactly_one()
    {
        await using var api = await TestApi.CreateAsync(useFileDatabase: true);
        var id = await api.SeedPersonAsync("Grace Hopper");

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => api.PostDutyByIdAsync(id, "1LT", "Pilot", "2020-01-01")));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(1, await DutyCountAsync(api));
    }

    [Fact]
    public async Task Concurrent_appends_in_any_order_leave_a_contiguous_history_with_one_open_duty()
    {
        await using var api = await TestApi.CreateAsync(useFileDatabase: true);
        var id = await api.SeedPersonAsync("Grace Hopper");
        var starts = new[] { 5, 2, 8, 1, 7, 3, 6, 4 }.Select(day => $"2020-01-0{day}").ToArray();

        var responses = await Task.WhenAll(starts.Select(start => api.PostDutyByIdAsync(id, "1LT", $"Duty {start}", start)));

        // Whatever the interleaving: only successes and chronology conflicts, never a 500 or a lost update.
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }));
        var (duties, detail) = await api.WithContextAsync(async context => (
            await context.AstronautDuties.OrderBy(d => d.DutyStartDate).ToListAsync(),
            await context.AstronautDetails.SingleAsync()));
        Assert.Equal(responses.Count(r => r.StatusCode == HttpStatusCode.Created), duties.Count);
        Assert.Single(duties, d => d.DutyEndDate is null);
        Assert.Equal(duties[^1].DutyStartDate, detail.CurrentDutyStartDate);
        for (var i = 0; i < duties.Count - 1; i++)
        {
            Assert.Equal(duties[i + 1].DutyStartDate.AddDays(-1), duties[i].DutyEndDate);
        }
    }
}
