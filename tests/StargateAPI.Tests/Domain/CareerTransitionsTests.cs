using StargateAPI.Domain;
using StargateAPI.Application.Logging;
using StargateAPI.Infrastructure.Identity;
using StargateAPI.Infrastructure.Persistence;
using StargateAPI.Domain.Results;

namespace StargateAPI.Tests.Domain;

/// <summary>Rules 2-7 and the retirement/reactivation policy, tested without HTTP or a database.</summary>
public class CareerTransitionsTests
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    /// <summary>Tracks the state a handler would load, applying each successful assignment.</summary>
    private sealed class Career
    {
        private readonly List<AstronautDuty> _duties = [];

        public Person Person { get; } = Person.Create("Test Person");
        public AstronautDetail? Detail { get; private set; }
        public AstronautDuty? Open { get; private set; }
        public IReadOnlyList<AstronautDuty> Duties => _duties;

        public CareerState State => new(Detail, Open, _duties.Count == 0 ? null : _duties.Max(d => d.DutyStartDate));

        public Result<DutyAssignment> Assign(string rank, string title, string start)
        {
            var input = DutyAssignmentInput.Create(rank, title, DateOnly.Parse(start), Today);
            Assert.True(input.IsSuccess, "Test input should be valid.");
            var result = CareerTransitions.AssignDuty(Person, State, input.Value);
            if (result.IsSuccess)
            {
                Detail ??= result.Value.NewDetail;
                Open = result.Value.NewDuty;
                _duties.Add(result.Value.NewDuty);
            }
            return result;
        }
    }

    private static DateOnly D(string value) => DateOnly.Parse(value);

    [Fact]
    public void First_assignment_creates_the_summary_and_an_open_duty()
    {
        var career = new Career();
        var versionBefore = career.Person.Version;

        var result = career.Assign("1LT", "Pilot", "2020-01-01");

        Assert.True(result.IsSuccess);
        var detail = Assert.IsType<AstronautDetail>(result.Value.NewDetail);
        Assert.Equal(("1LT", "Pilot", D("2020-01-01"), D("2020-01-01"), (DateOnly?)null),
            (detail.CurrentRank, detail.CurrentDutyTitle, detail.CurrentDutyStartDate, detail.CareerStartDate, detail.CareerEndDate));
        Assert.Null(result.Value.NewDuty.DutyEndDate);
        Assert.Null(result.Value.ClosedDuty);
        Assert.NotEqual(versionBefore, career.Person.Version);
    }

    [Fact]
    public void New_duty_closes_the_previous_one_the_day_before_and_updates_the_summary()
    {
        var career = new Career();
        career.Assign("1LT", "Pilot", "2020-01-01");
        var first = career.Open!;

        var result = career.Assign("CPT", "Commander", "2021-03-10");

        Assert.True(result.IsSuccess);
        Assert.Same(first, result.Value.ClosedDuty);
        Assert.Equal(D("2021-03-09"), first.DutyEndDate);
        Assert.Null(result.Value.NewDetail);
        Assert.Equal(("CPT", "Commander", D("2021-03-10"), D("2020-01-01")),
            (career.Detail!.CurrentRank, career.Detail.CurrentDutyTitle, career.Detail.CurrentDutyStartDate, career.Detail.CareerStartDate));
    }

    [Fact]
    public void Retirement_sets_career_end_the_day_before_and_stays_the_open_duty()
    {
        var career = new Career();
        career.Assign("1LT", "Pilot", "2020-01-01");

        var result = career.Assign("CPT", "RETIRED", "2025-01-01");

        Assert.True(result.IsSuccess);
        Assert.Equal(D("2024-12-31"), career.Detail!.CareerEndDate);
        Assert.Equal("RETIRED", career.Detail.CurrentDutyTitle);
        Assert.Null(result.Value.NewDuty.DutyEndDate);
    }

    [Fact]
    public void First_duty_retirement_is_rejected_without_changes()
    {
        var career = new Career();
        var versionBefore = career.Person.Version;

        var result = career.Assign("1LT", "RETIRED", "2020-01-01");

        Assert.Equal(ErrorCodes.FirstDutyRetirement, result.Error?.Code);
        Assert.Null(career.Detail);
        Assert.Equal(versionBefore, career.Person.Version);
    }

    [Fact]
    public void Retiring_while_already_retired_is_rejected_without_moving_career_end()
    {
        var career = new Career();
        career.Assign("1LT", "Pilot", "2020-01-01");
        career.Assign("CPT", "RETIRED", "2025-01-01");
        var retiredDuty = career.Open!;
        var versionBefore = career.Person.Version;

        var result = career.Assign("CPT", "RETIRED", "2026-01-01");

        Assert.Equal(ErrorCodes.AlreadyRetired, result.Error?.Code);
        Assert.Equal(D("2024-12-31"), career.Detail!.CareerEndDate);
        Assert.Null(retiredDuty.DutyEndDate);
        Assert.Equal(versionBefore, career.Person.Version);
    }

    [Fact]
    public void Reactivation_clears_career_end_keeps_first_service_and_a_later_retirement_sets_it_again()
    {
        var career = new Career();
        career.Assign("1LT", "Pilot", "2020-01-01");
        career.Assign("CPT", "RETIRED", "2025-01-01");
        var retiredDuty = career.Open!;

        var reactivated = career.Assign("MAJ", "Commander", "2026-01-01");

        Assert.True(reactivated.IsSuccess);
        Assert.Equal(D("2025-12-31"), retiredDuty.DutyEndDate);      // retired status ended
        Assert.Null(career.Detail!.CareerEndDate);
        Assert.Equal(D("2020-01-01"), career.Detail.CareerStartDate);

        var retiredAgain = career.Assign("MAJ", "RETIRED", "2026-06-01");

        Assert.True(retiredAgain.IsSuccess);
        Assert.Equal(D("2026-05-31"), career.Detail.CareerEndDate);
        Assert.Equal(4, career.Duties.Count);
        Assert.Single(career.Duties, d => d.DutyEndDate is null);
    }

    [Theory]
    [InlineData("2020-01-01")]   // exact repeat
    [InlineData("2019-12-31")]   // earlier
    public void Same_or_earlier_start_is_a_chronology_conflict_without_changes(string start)
    {
        var career = new Career();
        career.Assign("1LT", "Pilot", "2020-01-01");
        var open = career.Open!;
        var versionBefore = career.Person.Version;

        var result = career.Assign("1LT", "Pilot", start);

        Assert.Equal(ErrorCodes.ChronologyConflict, result.Error?.Code);
        Assert.Null(open.DutyEndDate);
        Assert.Equal("Pilot", career.Detail!.CurrentDutyTitle);
        Assert.Equal(versionBefore, career.Person.Version);
    }

    [Theory]
    [InlineData("2024-01-01", "2024-03-01", "2024-02-29")]   // leap day
    [InlineData("2023-01-01", "2023-03-01", "2023-02-28")]   // non-leap year
    [InlineData("2024-06-01", "2025-01-01", "2024-12-31")]   // year boundary
    [InlineData("2020-01-01", "2020-01-02", "2020-01-01")]   // one-day duty
    public void Previous_end_is_calendar_correct(string first, string second, string expectedEnd)
    {
        var career = new Career();
        career.Assign("1LT", "Pilot", first);
        var previous = career.Open!;

        career.Assign("CPT", "Commander", second);

        Assert.Equal(D(expectedEnd), previous.DutyEndDate);
    }

    [Fact]
    public void Inconsistent_loaded_state_is_treated_as_corrupt_data()
    {
        var career = new Career();
        career.Assign("1LT", "Pilot", "2020-01-01");
        var input = DutyAssignmentInput.Create("CPT", "Commander", D("2021-01-01"), Today).Value!;

        // A summary without its open duty violates rules 3/4; never guess how to continue.
        Assert.Throws<InvalidOperationException>(() =>
            CareerTransitions.AssignDuty(career.Person, new CareerState(career.Detail, null, D("2020-01-01")), input));
    }
}
