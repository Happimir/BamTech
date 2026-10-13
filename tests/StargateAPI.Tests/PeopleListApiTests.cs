using System.Net;
using System.Text.Json;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>Paging correctness (no gaps or duplicates, deterministic order) and literal prefix search.</summary>
public class PeopleListApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> GetAsync(TestApi api, string query) =>
        await Json.ReadAsync(await api.Client.GetAsync("/api/people" + query, Ct));

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
            pages.Add(await GetAsync(api, $"?page={page}&pageSize=3"));
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
    public async Task Legacy_list_route_returns_the_same_paged_contract()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");

        var legacy = await (await api.Client.GetAsync("/Person?page=1&pageSize=10", Ct)).Content.ReadAsStringAsync(Ct);
        var current = await (await api.Client.GetAsync("/api/people?page=1&pageSize=10", Ct)).Content.ReadAsStringAsync(Ct);

        Assert.Equal(current, legacy);
    }

    [Fact]
    public async Task Defaults_and_a_page_past_the_end()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Grace Hopper");

        var first = await GetAsync(api, "");
        var beyond = await GetAsync(api, "?page=5");

        Assert.Equal((1, 25), (first.GetProperty("page").GetInt32(), first.GetProperty("pageSize").GetInt32()));
        Assert.Equal(0, beyond.GetProperty("items").GetArrayLength());
        Assert.Equal(1, beyond.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?page=abc")]
    [InlineData("?page=2147483647&pageSize=100")]   // offset overflow
    public async Task Invalid_paging_is_rejected_rather_than_clamped(string query)
    {
        await using var api = await TestApi.CreateAsync();

        var response = await api.Client.GetAsync("/api/people" + query, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Name_filter_is_a_case_insensitive_prefix_with_wildcards_matched_literally()
    {
        await using var api = await TestApi.CreateAsync();
        foreach (var name in new[] { "100% Effort", "100x Effort", "A_B Jones", "AXB Jones", "Grace Hopper", "😀 Smile", "Zoë" })
        {
            await api.SeedPersonAsync(name);
        }

        Assert.Equal(["Grace Hopper"], Names(await GetAsync(api, "?name=grace")));
        Assert.Equal(["100% Effort"], Names(await GetAsync(api, "?name=" + Uri.EscapeDataString("100%"))));
        Assert.Equal(["A_B Jones"], Names(await GetAsync(api, "?name=A_B")));
        Assert.Equal(["😀 Smile"], Names(await GetAsync(api, "?name=" + Uri.EscapeDataString("😀"))));
        Assert.Equal(["Zoë"], Names(await GetAsync(api, "?name=" + Uri.EscapeDataString("zoë"))));
        Assert.Equal(0, (await GetAsync(api, "?name=nobody")).GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Metadata_reports_the_utc_business_date()
    {
        await using var api = await TestApi.CreateAsync();

        var body = await Json.ReadAsync(await api.Client.GetAsync("/api/metadata", Ct));

        Assert.Equal("UTC", body.GetProperty("businessTimeZone").GetString());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Contains(DateOnly.Parse(body.GetProperty("businessDate").GetString()!), new[] { today, today.AddDays(-1) });
    }
}
