using StargateAPI.Business.Domain;

namespace StargateAPI.Tests.Domain;

public class DutyAssignmentInputTests
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    [Theory]
    [InlineData(" retired ")]
    [InlineData("Retired")]
    [InlineData("RETIRED")]
    public void Retired_title_is_canonicalized_case_insensitively(string title)
    {
        var input = DutyAssignmentInput.Create("CPT", title, Today, Today);

        Assert.Equal("RETIRED", input.Value!.DutyTitle);
        Assert.True(input.Value.IsRetirement);
    }

    [Fact]
    public void Other_titles_keep_their_spelling_and_are_trimmed()
    {
        var input = DutyAssignmentInput.Create("  1LT ", "  Retired Pilot ", Today, Today);

        Assert.Equal("1LT", input.Value!.Rank);
        Assert.Equal("Retired Pilot", input.Value.DutyTitle);
        Assert.False(input.Value.IsRetirement);
    }

    [Fact]
    public void Missing_and_blank_fields_are_reported_per_field()
    {
        var input = DutyAssignmentInput.Create("  ", null, null, Today);

        Assert.False(input.IsSuccess);
        Assert.Equal(["dutyStartDate", "dutyTitle", "rank"], input.Error.FieldErrors!.Keys.Order());
    }

    [Fact]
    public void Oversized_rank_and_title_are_rejected()
    {
        Assert.True(DutyAssignmentInput.Create(new string('r', 100), new string('t', 200), Today, Today).IsSuccess);

        var input = DutyAssignmentInput.Create(new string('r', 101), new string('t', 201), Today, Today);

        Assert.Equal(["dutyTitle", "rank"], input.Error!.FieldErrors!.Keys.Order());
    }

    [Fact]
    public void Future_dates_are_rejected_and_today_is_accepted()
    {
        Assert.True(DutyAssignmentInput.Create("1LT", "Pilot", Today, Today).IsSuccess);

        var future = DutyAssignmentInput.Create("1LT", "Pilot", Today.AddDays(1), Today);

        Assert.Contains("dutyStartDate", future.Error!.FieldErrors!.Keys);
    }

    [Fact]
    public void Minimum_date_is_rejected_because_the_previous_end_could_not_be_derived()
    {
        var input = DutyAssignmentInput.Create("1LT", "Pilot", DateOnly.MinValue, Today);

        Assert.Contains("dutyStartDate", input.Error!.FieldErrors!.Keys);
    }
}
