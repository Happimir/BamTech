using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// Duty appends through GraphQL and the real database: typed errors, persistence of the whole
/// transition, no partial writes on rejection, and serialization of concurrent writers.
/// The transition rules themselves are covered by the domain tests.
/// </summary>
public class DutyAssignmentApiTests
{
    private const string Field = "assignDuty";

    private static Task<int> DutyCountAsync(TestApi api) =>
        api.WithContextAsync(context => context.AstronautDuties.CountAsync());

    // --- Former starter defects (were KnownDefectRegressionTests) ---

    [Fact]
    public async Task Duty_for_a_missing_person_is_a_not_found_error()
    {
        await using var api = await TestApi.CreateAsync();

        var byName = await api.AssignDutyByNameAsync("Nobody Here", "1LT", "Pilot", "2020-01-01");
        var byId = await api.AssignDutyAsync(Guid.CreateVersion7(), "1LT", "Pilot", "2020-01-01");

        Assert.Equal("NotFoundError", byName.PayloadErrorType(Field));
        Assert.Equal("NotFoundError", byId.PayloadErrorType(Field));
        Assert.Equal("not_found", byId.PayloadErrorCode(Field));
    }

    [Fact]
    public async Task Another_persons_duty_with_same_title_and_date_does_not_block_assignment()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");
        await api.SeedPersonAsync("Sally Ride");
        Assert.True((await api.AssignDutyByNameAsync("Grace Hopper", "1LT", "Pilot", "2020-01-01")).Succeeded(Field));

        var response = await api.AssignDutyByNameAsync("Sally Ride", "1LT", "Pilot", "2020-01-01");

        Assert.True(response.Succeeded(Field));
    }

    [Fact]
    public async Task First_duty_retirement_is_refused_and_writes_nothing()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Sally Ride");

        var response = await api.AssignDutyByNameAsync("Sally Ride", "1LT", "RETIRED", "2020-01-01");

        Assert.Equal("FirstDutyRetirementError", response.PayloadErrorType(Field));
        Assert.Equal("first_duty_retirement", response.PayloadErrorCode(Field));
        Assert.Equal(JsonValueKind.Null, response[Field].GetProperty("duty").ValueKind);
        Assert.Equal(0, await api.WithContextAsync(context => context.AstronautDetails.CountAsync()));
        Assert.Equal(0, await DutyCountAsync(api));
    }

    [Fact]
    public async Task Retiring_a_retired_person_is_refused()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Sally Ride");
        await api.AssignDutyAsync(id, "1LT", "Pilot", "2020-01-01");
        Assert.True((await api.AssignDutyAsync(id, "1LT", "RETIRED", "2024-01-01")).Succeeded(Field));

        var response = await api.AssignDutyAsync(id, "1LT", "retired", "2025-01-01");

        Assert.Equal("AlreadyRetiredError", response.PayloadErrorType(Field));
        Assert.Equal("already_retired", response.PayloadErrorCode(Field));
        Assert.Equal(2, await DutyCountAsync(api));
    }

    [Theory]
    [InlineData("2019-06-01")]   // earlier: the starter inverted history
    [InlineData("2020-01-01")]   // exact repeat: no replay recognition in this version
    public async Task Same_or_earlier_start_is_a_chronology_conflict_and_history_is_unchanged(string start)
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");
        Assert.True((await api.AssignDutyByNameAsync("Grace Hopper", "1LT", "Pilot", "2020-01-01")).Succeeded(Field));

        var response = await api.AssignDutyByNameAsync("Grace Hopper", "CPT", "Commander", start);

        Assert.Equal("ChronologyConflictError", response.PayloadErrorType(Field));
        Assert.Equal("chronology_conflict", response.PayloadErrorCode(Field));
        var duty = Assert.Single(await api.WithContextAsync(context => context.AstronautDuties.ToListAsync()));
        Assert.Null(duty.DutyEndDate);
    }

    // --- Contract and persistence ---

    [Fact]
    public async Task Payload_returns_the_new_duty_and_the_person_with_their_new_version()
    {
        await using var api = await TestApi.CreateAsync();
        var grace = await api.SeedPersonAsync("Grace Hopper");
        var before = await api.VersionAsync(grace);

        var response = await api.AssignDutyAsync(grace, "1LT", "Pilot", "2020-01-01");

        Assert.True(response.Succeeded(Field));
        var duty = response[Field].GetProperty("duty");
        var person = response[Field].GetProperty("person");
        Assert.Equal("Pilot", duty.GetProperty("dutyTitle").GetString());
        Assert.Equal(JsonValueKind.Null, duty.GetProperty("dutyEndDate").ValueKind);
        Assert.Equal(grace, person.GetProperty("id").GetGuid());
        Assert.Equal("Pilot", person.GetProperty("astronaut").GetProperty("currentDutyTitle").GetString());
        Assert.NotEqual(before, person.GetProperty("version").GetGuid());
        var stored = await api.WithContextAsync(context => context.AstronautDuties.SingleAsync());
        Assert.Equal((stored.Id, grace), (duty.GetProperty("id").GetGuid(), stored.PersonId));
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
            Assert.True((await api.AssignDutyAsync(id, rank, title, start)).Succeeded(Field));
        }

        var person = (await api.GraphQLAsync(Operations.PersonWithDuties, new { id }))["person"];
        var astronaut = person.GetProperty("astronaut");
        Assert.Equal("Commander", astronaut.GetProperty("currentDutyTitle").GetString());
        Assert.Equal("2026-01-01", astronaut.GetProperty("currentDutyStartDate").GetString());
        Assert.Equal("2020-01-01", astronaut.GetProperty("careerStartDate").GetString());
        Assert.Equal(JsonValueKind.Null, astronaut.GetProperty("careerEndDate").ValueKind);
        var history = person.GetProperty("duties").EnumerateArray()
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
    public async Task Rule6_status_and_retirement_flags_are_reported_by_the_server_through_a_whole_career()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Sally Ride");

        async Task<string?> StatusAsync()
        {
            // Every read that carries the summary reports the same status.
            var person = (await api.GraphQLAsync(Operations.Person, new { id }))["person"].GetProperty("astronaut");
            var row = (await api.GraphQLAsync(Operations.People, new { name = "sally" }))["people"]
                .GetProperty("items")[0].GetProperty("astronaut");
            var byName = (await api.GraphQLAsync(Operations.PersonByName, new { name = "sally ride" }))["personByName"]
                .GetProperty("astronaut");
            if (person.ValueKind == JsonValueKind.Null)
            {
                Assert.Equal(JsonValueKind.Null, row.ValueKind);
                Assert.Equal(JsonValueKind.Null, byName.ValueKind);
                return null;
            }
            var status = person.GetProperty("status").GetString();
            Assert.Equal(status, row.GetProperty("status").GetString());
            Assert.Equal(status, byName.GetProperty("status").GetString());
            return status;
        }

        var before = await StatusAsync();
        await api.AssignDutyAsync(id, "1LT", "Pilot", "2020-01-01");
        var serving = await StatusAsync();
        await api.AssignDutyAsync(id, "CPT", " retired ", "2025-01-01");
        var retired = await StatusAsync();
        await api.AssignDutyAsync(id, "MAJ", "Commander", "2026-01-01");
        var reactivated = await StatusAsync();

        Assert.Equal([null, "ACTIVE", "RETIRED", "ACTIVE"], new[] { before, serving, retired, reactivated });
        var duties = (await api.GraphQLAsync(Operations.PersonWithDuties, new { id }))["person"].GetProperty("duties");
        Assert.Equal([false, true, false], duties.EnumerateArray().Select(d => d.GetProperty("isRetirement").GetBoolean()));
    }

    [Fact]
    public async Task Stale_expected_version_is_a_conflict_and_the_returned_version_allows_the_next_write()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Grace Hopper");
        var original = await api.VersionAsync(id);

        var first = await api.AssignDutyAsync(id, "1LT", "Pilot", "2020-01-01", original);
        var newVersion = first[Field].GetProperty("person").GetProperty("version").GetGuid();
        var stale = await api.AssignDutyAsync(id, "CPT", "Commander", "2021-01-01", original);
        var current = await api.AssignDutyAsync(id, "CPT", "Commander", "2021-01-01", newVersion);

        Assert.True(first.Succeeded(Field));
        Assert.NotEqual(original, newVersion);
        Assert.Equal("ConcurrencyConflictError", stale.PayloadErrorType(Field));
        Assert.Equal("concurrency_conflict", stale.PayloadErrorCode(Field));
        Assert.True(current.Succeeded(Field));
        Assert.Equal(2, await DutyCountAsync(api));
    }

    [Fact]
    public async Task By_name_assignment_refuses_an_ambiguous_name_before_writing()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Alex Smith");
        await api.SeedPersonAsync("Alex Smith");

        var response = await api.AssignDutyByNameAsync("Alex Smith", "1LT", "Pilot", "2020-01-01");

        Assert.Equal("AmbiguousNameError", response.PayloadErrorType(Field));
        Assert.Equal("ambiguous_name", response.PayloadErrorCode(Field));
        Assert.Equal(0, await DutyCountAsync(api));
    }

    [Fact]
    public async Task Invalid_input_is_a_validation_error_with_field_names_and_writes_nothing()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Grace Hopper");

        var blank = await api.AssignDutyAsync(id, " ", " ", "2020-01-01");
        var future = await api.AssignDutyAsync(id, "1LT", "Pilot", "2999-01-01");

        Assert.Equal("ValidationError", blank.PayloadErrorType(Field));
        Assert.Equal(["dutyTitle", "rank"], blank.InvalidFields(Field).Order());
        Assert.Equal(["dutyStartDate"], future.InvalidFields(Field));
        Assert.Equal(0, await DutyCountAsync(api));
    }

    [Theory]
    [InlineData(null)]                         // required by the schema
    [InlineData("2020-13-45")]                 // not a date
    [InlineData("2024-03-10T00:00:00")]        // the starter's date-time form: dates are calendar dates
    [InlineData("03/10/2024")]
    public async Task Start_date_must_be_a_calendar_date_or_the_request_is_rejected_before_it_runs(string? sent)
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Grace Hopper");

        var response = await api.AssignDutyAsync(id, "1LT", "Pilot", sent);

        Assert.True(response.HasRequestErrors);
        Assert.False(response.Body.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty(Field, out var payload) && payload.ValueKind == JsonValueKind.Object);
        Assert.Equal(0, await DutyCountAsync(api));
    }

    [Fact]
    public async Task Person_selector_takes_exactly_one_of_id_or_name()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Grace Hopper");

        var both = await api.GraphQLAsync(Operations.AssignDuty, new
        {
            input = new { person = new { id, name = "Grace Hopper" }, rank = "1LT", dutyTitle = "Pilot", dutyStartDate = "2020-01-01" }
        });

        Assert.True(both.HasRequestErrors);
        Assert.Equal(0, await DutyCountAsync(api));
    }

    // --- Concurrency (file database: real SQLite locking, separate connections) ---

    [Fact]
    public async Task Concurrent_identical_duties_create_exactly_one()
    {
        await using var api = await TestApi.CreateAsync(useFileDatabase: true);
        var id = await api.SeedPersonAsync("Grace Hopper");

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => api.AssignDutyAsync(id, "1LT", "Pilot", "2020-01-01")));

        Assert.Single(responses, r => r.Succeeded(Field));
        Assert.All(responses.Where(r => !r.Succeeded(Field)),
            r => Assert.Equal("ChronologyConflictError", r.PayloadErrorType(Field)));
        Assert.Equal(1, await DutyCountAsync(api));
    }

    [Fact]
    public async Task Concurrent_appends_in_any_order_leave_a_contiguous_history_with_one_open_duty()
    {
        await using var api = await TestApi.CreateAsync(useFileDatabase: true);
        var id = await api.SeedPersonAsync("Grace Hopper");
        var starts = new[] { 5, 2, 8, 1, 7, 3, 6, 4 }.Select(day => $"2020-01-0{day}").ToArray();

        var responses = await Task.WhenAll(starts.Select(start => api.AssignDutyAsync(id, "1LT", $"Duty {start}", start)));

        // Whatever the interleaving: only successes and chronology conflicts, never a fault or a lost update.
        Assert.All(responses, r => Assert.False(r.HasRequestErrors));
        Assert.All(responses.Where(r => !r.Succeeded(Field)),
            r => Assert.Equal("ChronologyConflictError", r.PayloadErrorType(Field)));
        var (duties, detail) = await api.WithContextAsync(async context => (
            await context.AstronautDuties.OrderBy(d => d.DutyStartDate).ToListAsync(),
            await context.AstronautDetails.SingleAsync()));
        Assert.Equal(responses.Count(r => r.Succeeded(Field)), duties.Count);
        Assert.Single(duties, d => d.DutyEndDate is null);
        Assert.Equal(duties[^1].DutyStartDate, detail.CurrentDutyStartDate);
        for (var i = 0; i < duties.Count - 1; i++)
        {
            Assert.Equal(duties[i + 1].DutyStartDate.AddDays(-1), duties[i].DutyEndDate);
        }
    }
}
