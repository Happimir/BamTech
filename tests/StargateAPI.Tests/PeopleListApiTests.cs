using System.Net;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Application.Queries;
using StargateAPI.Domain;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// Paging correctness (no gaps or duplicates, deterministic order), literal prefix search, and the
/// legacy full list, which streams everyone in the same order.
/// </summary>
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
    public async Task Legacy_list_route_returns_everyone_beyond_the_page_limit_as_the_directory_rows()
    {
        await using var api = await TestApi.CreateAsync();
        await api.WithContextAsync(async context =>
        {
            // More people than the largest page, including namesakes (ordered by ID).
            context.People.AddRange(Enumerable.Range(0, GetPeopleHandler.MaxPageSize + 20)
                .Select(i => Person.Create($"Person {i:D3}"))
                .Append(Person.Create("Alex Smith"))
                .Append(Person.Create("Alex Smith")));
            await context.SaveChangesAsync();
        });

        var all = await Json.ReadAsync(await api.Client.GetAsync("/Person", Ct));
        var paged = new List<JsonElement>();
        for (var page = 1; page <= 2; page++)
        {
            paged.AddRange((await GetAsync(api, $"?page={page}&pageSize={GetPeopleHandler.MaxPageSize}"))
                .GetProperty("items").EnumerateArray());
        }

        Assert.Equal(JsonValueKind.Array, all.ValueKind);
        Assert.Equal(GetPeopleHandler.MaxPageSize + 22, all.GetArrayLength());
        // Same rows, same order as walking the directory's pages.
        Assert.Equal(paged.Select(row => row.GetRawText()), all.EnumerateArray().Select(row => row.GetRawText()));
    }

    [Fact]
    public async Task Legacy_list_route_filters_by_name_prefix_and_rejects_an_invalid_filter_before_streaming()
    {
        await using var api = await TestApi.CreateAsync();
        foreach (var name in new[] { "Bravo", "Alpha", "Alex Smith" })
        {
            await api.SeedPersonAsync(name);
        }

        var filtered = await Json.ReadAsync(await api.Client.GetAsync("/Person?name=al", Ct));
        var invalid = await api.Client.GetAsync("/Person?name=" + new string('x', 301), Ct);

        Assert.Equal(["Alex Smith", "Alpha"], filtered.EnumerateArray().Select(p => p.GetProperty("name").GetString()));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("validation", (await Json.ReadAsync(invalid)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Full_list_is_read_while_it_is_enumerated_not_loaded_up_front()
    {
        await using var api = await TestApi.CreateAsync();
        await api.SeedPersonAsync("Alpha");
        await using var scope = api.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new GetAllPeople(), Ct);
        // Added after the handler returned: only a sequence that queries as it is read can see it.
        await api.SeedPersonAsync("Bravo");
        var names = new List<string>();
        await foreach (var person in result.Value!.WithCancellation(Ct))
        {
            names.Add(person.Name);
        }

        Assert.Equal(["Alpha", "Bravo"], names);
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
