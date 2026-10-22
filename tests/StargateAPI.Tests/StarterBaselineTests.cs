using Microsoft.EntityFrameworkCore;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// Behaviour the starter already gets right and that must keep holding through the redesign.
/// Assertions avoid the starter's response envelope so they survive the planned DTO changes.
/// </summary>
public class StarterBaselineTests
{
    [Fact]
    public async Task Migrations_apply_to_an_empty_database_with_no_pending_model_changes()
    {
        await using var api = await TestApi.CreateAsync();

        var (pending, modelChanged) = await api.WithContextAsync(async context =>
            ((await context.Database.GetPendingMigrationsAsync()).ToList(), context.Database.HasPendingModelChanges()));

        Assert.Empty(pending);
        Assert.False(modelChanged);
    }

    [Fact]
    public async Task Get_all_people_succeeds_on_an_empty_database()
    {
        await using var api = await TestApi.CreateAsync();

        var response = await api.GraphQLAsync(Operations.People);

        Assert.False(response.HasRequestErrors);
        Assert.Equal(0, response["people"].GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Rule2_creating_a_person_creates_no_astronaut_records()
    {
        await using var api = await TestApi.CreateAsync();

        var response = await api.GraphQLAsync(Operations.CreatePerson, new { input = new { name = "Ada Lovelace" } });

        Assert.True(response.Succeeded("createPerson"));
        var (people, details, duties) = await api.WithContextAsync(async context => (
            await context.People.CountAsync(p => p.Name == "Ada Lovelace"),
            await context.AstronautDetails.CountAsync(),
            await context.AstronautDuties.CountAsync()));
        Assert.Equal(1, people);
        Assert.Equal(0, details);
        Assert.Equal(0, duties);
    }

    [Fact]
    public async Task Rules4_and_5_new_duty_closes_previous_duty_the_day_before_and_stays_open()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");

        Assert.True((await api.AssignDutyByNameAsync("Grace Hopper", "1LT", "Pilot", "2020-01-01")).Succeeded("assignDuty"));
        Assert.True((await api.AssignDutyByNameAsync("Grace Hopper", "CPT", "Commander", "2021-03-10")).Succeeded("assignDuty"));

        var duties = await api.WithContextAsync(context =>
            context.AstronautDuties.OrderBy(d => d.DutyStartDate).ToListAsync());
        Assert.Equal(2, duties.Count);
        Assert.Equal(new DateOnly(2021, 3, 9), duties[0].DutyEndDate);
        Assert.Null(duties[1].DutyEndDate);
    }

    [Fact]
    public async Task Rule3_detail_tracks_the_single_current_title_and_rank()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");

        Assert.True((await api.AssignDutyByNameAsync("Grace Hopper", "1LT", "Pilot", "2020-01-01")).Succeeded("assignDuty"));
        Assert.True((await api.AssignDutyByNameAsync("Grace Hopper", "CPT", "Commander", "2021-03-10")).Succeeded("assignDuty"));

        var details = await api.WithContextAsync(context => context.AstronautDetails.ToListAsync());
        var detail = Assert.Single(details);
        Assert.Equal("Commander", detail.CurrentDutyTitle);
        Assert.Equal("CPT", detail.CurrentRank);
        Assert.Equal(new DateOnly(2020, 1, 1), detail.CareerStartDate);
    }

    [Fact]
    public async Task Rule7_retirement_after_active_duty_sets_career_end_the_day_before()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Sally Ride");

        Assert.True((await api.AssignDutyByNameAsync("Sally Ride", "1LT", "Pilot", "2020-01-01")).Succeeded("assignDuty"));
        Assert.True((await api.AssignDutyByNameAsync("Sally Ride", "CPT", "RETIRED", "2025-01-01")).Succeeded("assignDuty"));

        var detail = await api.WithContextAsync(context => context.AstronautDetails.SingleAsync());
        Assert.Equal("RETIRED", detail.CurrentDutyTitle);
        Assert.Equal(new DateOnly(2024, 12, 31), detail.CareerEndDate);
    }

    [Fact]
    public async Task Duplicate_display_names_can_be_created()
    {
        // Matches the settled identity design (stable IDs, namesakes allowed). In the starter this
        // already held, because its CreatePersonPreProcessor was never registered with MediatR.
        await using var api = await TestApi.CreateAsync();

        Assert.True((await api.GraphQLAsync(Operations.CreatePerson, new { input = new { name = "Alex Smith" } })).Succeeded("createPerson"));
        Assert.True((await api.GraphQLAsync(Operations.CreatePerson, new { input = new { name = "Alex Smith" } })).Succeeded("createPerson"));

        var count = await api.WithContextAsync(context => context.People.CountAsync(p => p.Name == "Alex Smith"));
        Assert.Equal(2, count);
    }
}
