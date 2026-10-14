using Microsoft.EntityFrameworkCore;
using StargateAPI.Application.Services;
using StargateAPI.Domain;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// The persistence contract around the career rules. Staging must save the previous duty's
/// closure before the new open duty exists, so the one-open-duty unique index can never see two
/// open rows, whatever order EF would give the commands.
/// </summary>
public class AstronautCareersTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Staging_saves_the_closure_first_and_leaves_the_new_duty_for_the_callers_save()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Grace Hopper");
        (await api.PostDutyByIdAsync(id, "1LT", "Pilot", "2020-01-01")).EnsureSuccessStatusCode();

        await api.WithContextAsync(async context =>
        {
            var careers = new AstronautCareers(context);
            var person = await context.People.SingleAsync(p => p.Id == id, Ct);
            var state = await careers.LoadStateAsync(id, Ct);
            Assert.Equal(new DateOnly(2020, 1, 1), state.LatestDutyStart);
            var input = DutyAssignmentInput.Create("CPT", "Commander", new DateOnly(2021, 1, 1), new DateOnly(2026, 1, 1));
            var change = CareerTransitions.AssignDuty(person, state, input.Value!).Value!;

            await careers.StageAsync(change, Ct);

            Assert.Equal(EntityState.Unchanged, context.Entry(change.ClosedDuty!).State);
            Assert.Equal(EntityState.Added, context.Entry(change.NewDuty).State);
            var openInDatabase = await context.AstronautDuties.AsNoTracking()
                .CountAsync(d => d.PersonId == id && d.DutyEndDate == null, Ct);
            Assert.Equal(0, openInDatabase);
            return 0;
        });
    }
}
