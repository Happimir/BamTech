using System.Text.Json;
using StargateAPI.Application.Logging;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// Paging correctness (no gaps or duplicates, deterministic order), literal prefix search, and
/// duties for a whole page fetched in one batched query.
/// </summary>
public class PeopleListApiTests
{
    private static async Task<JsonElement> GetAsync(TestApi api, object? variables = null) =>
        (await api.GraphQLAsync(Operations.People, variables))["people"];

    private static List<string?> Names(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()).ToList();

    [Fact]
    public async Task Pages_cover_everyone_exactly_once_in_a_stable_order()
    {
        await using var api = await TestApi.CreateAsync();
        foreach (var name in new[] { "delta", "Alpha", "charlie", "Bravo", "Alex Smith", "Alex Smith", "echo" })
        {
            await api.SeedPersonAsync(name);
        }

        var pages = new List<JsonElement>();
        for (var page = 1; page <= 3; page++)
        {
            pages.Add(await GetAsync(api, new { page, pageSize = 3 }));
        }
        var ids = pages.SelectMany(p => p.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid())).ToList();

        Assert.Equal(7, pages[0].GetProperty("totalCount").GetInt32());
        Assert.Equal(3, pages[0].GetProperty("totalPages").GetInt32());
        Assert.Equal(7, ids.Distinct().Count());
        Assert.Equal(
            ["Alex Smith", "Alex Smith", "Alpha", "Bravo", "charlie", "delta", "echo"],
            pages.SelectMany(Names));
        // Namesakes are tie-broken by ID, so the order is reproducible.
        var namesakeIds = ids.Take(2).ToList();
        Assert.Equal(namesakeIds.OrderBy(id => id.ToString().ToUpperInvariant()), namesakeIds);
    }

    [Fact]
    public async Task Duties_for_a_whole_page_cost_one_query_not_one_per_person()
    {
        await using var api = await TestApi.CreateAsync(configureServices: CommandCounter.Register);
        foreach (var name in new[] { "Alpha", "Bravo", "Charlie" })
        {
            var id = await api.SeedPersonAsync(name);
            await api.AssignDutyAsync(id, "1LT", $"{name} Pilot", "2020-01-01");
        }
        await api.SeedPersonAsync("Delta");
        var counter = CommandCounter.For(api);
        counter.Reset();

        var response = await api.GraphQLAsync("""
            query { people { items { name duties { dutyTitle } } } }
            """);

        var titles = response["people"].GetProperty("items").EnumerateArray().ToDictionary(
            p => p.GetProperty("name").GetString()!,
            p => p.GetProperty("duties").EnumerateArray().Select(d => d.GetProperty("dutyTitle").GetString()).ToList());
        Assert.Equal(["Alpha Pilot"], titles["Alpha"]);
        Assert.Equal(["Charlie Pilot"], titles["Charlie"]);
        Assert.Empty(titles["Delta"]);
        // The DataLoader collects every person's ID and reads all their duties at once.
        Assert.Equal(1, counter.Count(sql => sql.Contains("FROM \"AstronautDuty\"")));
    }

    [Fact]
    public async Task Defaults_and_a_page_past_the_end()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");

        var first = await GetAsync(api);
        var beyond = await GetAsync(api, new { page = 5 });

        Assert.Equal((1, 25), (first.GetProperty("page").GetInt32(), first.GetProperty("pageSize").GetInt32()));
        Assert.Equal(0, beyond.GetProperty("items").GetArrayLength());
        Assert.Equal(1, beyond.GetProperty("totalCount").GetInt32());
    }

    public static TheoryData<string, object> InvalidPaging => new()
    {
        { "page", new { page = 0 } },
        { "pageSize", new { pageSize = 0 } },
        { "pageSize", new { pageSize = 101 } },
        { "page", new { page = int.MaxValue, pageSize = 100 } },   // offset overflow
    };

    [Theory]
    [MemberData(nameof(InvalidPaging))]
    public async Task Invalid_paging_is_rejected_with_its_field_rather_than_clamped(string field, object variables)
    {
        await using var api = await TestApi.CreateAsync();

        var response = await api.GraphQLAsync(Operations.People, variables);

        Assert.Equal("validation", response.RequestErrorCode);
        Assert.True(response.RequestError.GetProperty("extensions").GetProperty("fields").TryGetProperty(field, out _));
        var outcome = Assert.Single(await api.ProcessLogsAsync(expectedOutcomes: 1));
        Assert.Equal((ProcessLogOutcome.Rejected, "validation", "query people"), (outcome.Outcome, outcome.ErrorCode, outcome.Operation));
    }

    [Fact]
    public async Task A_paging_argument_of_the_wrong_type_is_rejected_before_it_runs()
    {
        await using var api = await TestApi.CreateAsync();

        var response = await api.GraphQLAsync(Operations.People, new { page = "abc" });

        Assert.True(response.HasRequestErrors);
    }

    [Fact]
    public async Task Name_filter_is_a_case_insensitive_prefix_with_wildcards_matched_literally()
    {
        await using var api = await TestApi.CreateAsync();
        foreach (var name in new[] { "100% Effort", "100x Effort", "A_B Jones", "AXB Jones", "Grace Hopper", "😀 Smile", "Zoë" })
        {
            await api.SeedPersonAsync(name);
        }

        Assert.Equal(["Grace Hopper"], Names(await GetAsync(api, new { name = "grace" })));
        Assert.Equal(["100% Effort"], Names(await GetAsync(api, new { name = "100%" })));
        Assert.Equal(["A_B Jones"], Names(await GetAsync(api, new { name = "A_B" })));
        Assert.Equal(["😀 Smile"], Names(await GetAsync(api, new { name = "😀" })));
        Assert.Equal(["Zoë"], Names(await GetAsync(api, new { name = "zoë" })));
        Assert.Equal(0, (await GetAsync(api, new { name = "nobody" })).GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Business_calendar_reports_the_utc_business_date()
    {
        await using var api = await TestApi.CreateAsync();

        var calendar = (await api.GraphQLAsync(Operations.BusinessCalendar))["businessCalendar"];

        Assert.Equal("UTC", calendar.GetProperty("businessTimeZone").GetString());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Contains(DateOnly.Parse(calendar.GetProperty("businessDate").GetString()!), new[] { today, today.AddDays(-1) });
    }
}
