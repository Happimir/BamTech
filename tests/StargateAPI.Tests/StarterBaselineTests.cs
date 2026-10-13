using System.Net;
using System.Net.Http.Json;
using MediatR;
using MediatR.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Business.Commands;
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

        var response = await api.Client.GetAsync("/Person");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Rule2_creating_a_person_creates_no_astronaut_records()
    {
        await using var api = await TestApi.CreateAsync();

        var response = await api.Client.PostAsJsonAsync("/Person", "Ada Lovelace");

        Assert.True(response.IsSuccessStatusCode, $"Create returned {(int)response.StatusCode}");
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

        (await api.PostDutyAsync("Grace Hopper", "1LT", "Pilot", "2020-01-01")).EnsureSuccessStatusCode();
        (await api.PostDutyAsync("Grace Hopper", "CPT", "Commander", "2021-03-10")).EnsureSuccessStatusCode();

        var duties = await api.WithContextAsync(context =>
            context.AstronautDuties.OrderBy(d => d.DutyStartDate).ToListAsync());
        Assert.Equal(2, duties.Count);
        Assert.Equal(new DateTime(2021, 3, 9), duties[0].DutyEndDate);
        Assert.Null(duties[1].DutyEndDate);
    }

    [Fact]
    public async Task Rule3_detail_tracks_the_single_current_title_and_rank()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");

        (await api.PostDutyAsync("Grace Hopper", "1LT", "Pilot", "2020-01-01")).EnsureSuccessStatusCode();
        (await api.PostDutyAsync("Grace Hopper", "CPT", "Commander", "2021-03-10")).EnsureSuccessStatusCode();

        var details = await api.WithContextAsync(context => context.AstronautDetails.ToListAsync());
        var detail = Assert.Single(details);
        Assert.Equal("Commander", detail.CurrentDutyTitle);
        Assert.Equal("CPT", detail.CurrentRank);
        Assert.Equal(new DateTime(2020, 1, 1), detail.CareerStartDate);
    }

    [Fact]
    public async Task Rule7_retirement_after_active_duty_sets_career_end_the_day_before()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Sally Ride");

        (await api.PostDutyAsync("Sally Ride", "1LT", "Pilot", "2020-01-01")).EnsureSuccessStatusCode();
        (await api.PostDutyAsync("Sally Ride", "CPT", "RETIRED", "2025-01-01")).EnsureSuccessStatusCode();

        var detail = await api.WithContextAsync(context => context.AstronautDetails.SingleAsync());
        Assert.Equal("RETIRED", detail.CurrentDutyTitle);
        Assert.Equal(new DateTime(2024, 12, 31), detail.CareerEndDate);
    }

    [Fact]
    public async Task Duplicate_display_names_can_be_created()
    {
        // Matches the settled identity design (stable IDs, namesakes allowed). In the starter it
        // holds only because CreatePersonPreProcessor never runs; see the registration test below.
        await using var api = await TestApi.CreateAsync();

        (await api.Client.PostAsJsonAsync("/Person", "Alex Smith")).EnsureSuccessStatusCode();
        (await api.Client.PostAsJsonAsync("/Person", "Alex Smith")).EnsureSuccessStatusCode();

        var count = await api.WithContextAsync(context => context.People.CountAsync(p => p.Name == "Alex Smith"));
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Mediator_registers_preprocessors_as_observed_in_the_starter()
    {
        await using var api = await TestApi.CreateAsync();
        await using var scope = api.Services.CreateAsyncScope();

        var dutyPreProcessors = scope.ServiceProvider.GetServices<IRequestPreProcessor<CreateAstronautDuty>>().ToList();
        var personPreProcessors = scope.ServiceProvider.GetServices<IRequestPreProcessor<CreatePerson>>().ToList();
        var dutyBehaviors = scope.ServiceProvider
            .GetServices<IPipelineBehavior<CreateAstronautDuty, CreateAstronautDutyResult>>().ToList();

        Assert.Single(dutyPreProcessors);
        Assert.Empty(personPreProcessors);
        Assert.Single(dutyBehaviors.OfType<RequestPreProcessorBehavior<CreateAstronautDuty, CreateAstronautDutyResult>>());
    }
}
