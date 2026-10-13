using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Domain;
using StargateAPI.Application.Logging;
using StargateAPI.Infrastructure.Identity;
using StargateAPI.Infrastructure.Persistence;
using StargateAPI.Security;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// Sign-in, revocable sessions, CSRF and role policies on every route family.
/// </summary>
public class AuthTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await Json.ReadAsync(response)).GetProperty("code").GetString();

    // --- Route protection ---

    [Fact]
    public async Task Every_route_declares_its_access_and_only_sign_in_routes_are_anonymous()
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Anonymous);
        var actions = api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Select(e => (Endpoint: e, Action: e.Metadata.GetMetadata<ControllerActionDescriptor>()))
            .Where(x => x.Action is not null)
            .ToList();

        var anonymous = actions.Where(x => x.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(x => $"{x.Action!.ControllerName}.{x.Action.ActionName}").Order().ToList();
        Assert.Equal(["Auth.Antiforgery", "Auth.Login", "Auth.Logout"], anonymous);

        foreach (var (endpoint, action) in actions.Where(x => x.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null))
        {
            var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).ToList();
            Assert.True(policies.Count > 0, $"{action!.DisplayName} has no [Authorize].");
            var isWrite = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Any(m => m != "GET");
            if (isWrite)
            {
                Assert.True(policies.Contains(Policies.PeopleWrite) || policies.Contains(Policies.DutiesWrite),
                    $"{action!.DisplayName} writes without a write policy.");
            }
        }
    }

    [Fact]
    public async Task Anonymous_requests_get_401_problem_details_not_a_login_redirect()
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Anonymous);

        var read = await api.Client.GetAsync("/api/people", Ct);
        var legacyRead = await api.Client.GetAsync("/AstronautDuty/Anyone", Ct);
        var me = await api.Client.GetAsync("/auth/me", Ct);

        foreach (var response in new[] { read, legacyRead, me })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Null(response.Headers.Location);
            Assert.Equal("unauthenticated", await CodeAsync(response));
        }
    }

    public static TheoryData<string, string, string> WriteRoutes => new()
    {
        { "POST", "/Person", "\"New Person\"" },
        { "PUT", "/Person/by-name?name=New%20Person", "{\"name\":\"New Person\"}" },
        { "POST", "/api/people", "{\"name\":\"New Person\"}" },
        { "PUT", "/api/people/{id}", "{\"name\":\"Renamed\",\"version\":\"{version}\"}" },
        { "POST", "/api/people/{id}/duties", "{\"rank\":\"1LT\",\"dutyTitle\":\"Pilot\",\"dutyStartDate\":\"2020-01-01\"}" },
        { "POST", "/AstronautDuty", "{\"name\":\"Grace Hopper\",\"rank\":\"1LT\",\"dutyTitle\":\"Pilot\",\"dutyStartDate\":\"2020-01-01\"}" }
    };

    [Theory]
    [MemberData(nameof(WriteRoutes))]
    public async Task Reader_is_forbidden_on_every_write_route_and_nothing_changes(string method, string path, string body)
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Reader);
        var id = await api.SeedPersonAsync("Grace Hopper");
        var version = await api.WithContextAsync(c => c.People.Where(p => p.Id == id).Select(p => p.Version).SingleAsync());

        var request = new HttpRequestMessage(new HttpMethod(method), path.Replace("{id}", id.ToString()))
        {
            Content = new StringContent(body.Replace("{version}", version.ToString()), System.Text.Encoding.UTF8, "application/json")
        };
        var response = await api.Client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("forbidden", await CodeAsync(response));
        Assert.Equal(1, await api.WithContextAsync(c => c.People.CountAsync()));
        Assert.Equal("Grace Hopper", await api.WithContextAsync(c => c.People.Select(p => p.Name).SingleAsync()));
        Assert.Equal(0, await api.WithContextAsync(c => c.AstronautDuties.CountAsync()));
        Assert.DoesNotContain(await api.ProcessLogsAsync(expectedOutcomes: 1), l => l.Phase == ProcessLogPhase.MutationAudit);
    }

    [Fact]
    public async Task Reader_can_use_every_read_route()
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Reader);
        var id = await api.SeedPersonAsync("Grace Hopper");

        foreach (var path in new[] { "/api/people", "/Person", $"/api/people/{id}", $"/api/people/{id}/duties",
                     "/Person/Grace%20Hopper", "/AstronautDuty/Grace%20Hopper", "/api/metadata", "/auth/me" })
        {
            Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync(path, Ct)).StatusCode);
        }
    }

    [Fact]
    public async Task Me_describes_roles_and_capabilities_without_secrets()
    {
        await using var api = await TestApi.CreateAsync();
        var reader = await api.CreateClientAsync(TestUser.Reader);

        var adminMe = await api.Client.GetAsync("/auth/me", Ct);
        var readerMe = await Json.ReadAsync(await reader.GetAsync("/auth/me", Ct));

        var admin = await Json.ReadAsync(adminMe);
        Assert.Equal("admin", admin.GetProperty("userName").GetString());
        Assert.Equal(["People.Read", "People.Write", "Duties.Write"],
            admin.GetProperty("capabilities").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal(["People.Read"], readerMe.GetProperty("capabilities").EnumerateArray().Select(c => c.GetString()));
        var raw = await adminMe.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", raw, StringComparison.OrdinalIgnoreCase);
    }

    // --- Sign-in ---

    [Fact]
    public async Task Unknown_user_and_wrong_password_get_the_same_generic_401_and_passwords_are_never_logged()
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Anonymous);
        var client = await api.CreateClientAsync(TestUser.Anonymous);

        var unknown = await TestApi.LoginAsync(client, "nobody", "Wrong-Pass-1!");
        var wrong = await TestApi.LoginAsync(client, "admin", "Wrong-Pass-1!");

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var (a, b) = (await Json.ReadAsync(unknown), await Json.ReadAsync(wrong));
        Assert.Equal((a.GetProperty("title").GetString(), a.GetProperty("code").GetString()),
            (b.GetProperty("title").GetString(), b.GetProperty("code").GetString()));
        var logs = await api.ProcessLogsAsync(expectedOutcomes: 4);
        Assert.Equal(2, logs.Count(l => l.ErrorCode == "invalid_credentials"));
        Assert.DoesNotContain(logs, l => $"{l.Message}{l.ExceptionDetails}{l.ChangesJson}".Contains("Wrong-Pass"));
    }

    [Fact]
    public async Task Repeated_failures_lock_the_account_even_for_the_right_password()
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Anonymous);
        var client = await api.CreateClientAsync(TestUser.Anonymous);

        for (var i = 0; i < 5; i++)
        {
            await TestApi.LoginAsync(client, "admin", "Wrong-Pass-1!");
        }
        var correct = await TestApi.LoginAsync(client, "admin", TestApi.AdminPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, correct.StatusCode);
        Assert.Equal("invalid_credentials", await CodeAsync(correct));
    }

    [Fact]
    public async Task Disabled_account_cannot_sign_in()
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Anonymous);
        await api.WithContextAsync(c => c.Users.Where(u => u.UserName == "admin").ExecuteUpdateAsync(s => s.SetProperty(u => u.IsEnabled, false)));

        var response = await TestApi.LoginAsync(await api.CreateClientAsync(TestUser.Anonymous), "admin", TestApi.AdminPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- CSRF ---

    [Fact]
    public async Task Login_without_an_antiforgery_token_is_rejected()
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Anonymous);

        var response = await api.CreatePlainClient().PostAsJsonAsync("/auth/login",
            new { userName = "admin", password = TestApi.AdminPassword }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("csrf_failed", await CodeAsync(response));
        Assert.Equal(0, await api.WithContextAsync(c => c.AuthSessions.CountAsync()));
    }

    [Fact]
    public async Task Signed_in_write_with_a_bad_antiforgery_token_changes_nothing()
    {
        await using var api = await TestApi.CreateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/people") { Content = JsonContent.Create(new { name = "Forged" }) };
        request.Headers.Add(SecurityServiceCollectionExtensions.XsrfHeaderName, "forged-token");

        var response = await api.Client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("csrf_failed", await CodeAsync(response));
        Assert.Equal(0, await api.WithContextAsync(c => c.People.CountAsync()));
    }

    // --- Sessions ---

    private static async Task<string> SignInAndCaptureCookieAsync(TestApi api, HttpClient client)
    {
        var login = await TestApi.LoginAsync(client, "admin", TestApi.AdminPassword);
        login.EnsureSuccessStatusCode();
        var setCookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(SecurityServiceCollectionExtensions.AuthCookieName + "="));
        return setCookie.Split(';')[0];
    }

    private static Task<HttpResponseMessage> GetWithCookieAsync(TestApi api, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/people");
        request.Headers.Add("Cookie", cookie);
        return api.CreatePlainClient().SendAsync(request, Ct);
    }

    [Fact]
    public async Task Logout_revokes_the_session_so_a_copied_cookie_stops_working()
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Anonymous);
        var client = await api.CreateClientAsync(TestUser.Anonymous);
        var copiedCookie = await SignInAndCaptureCookieAsync(api, client);
        Assert.Equal(HttpStatusCode.OK, (await GetWithCookieAsync(api, copiedCookie)).StatusCode);   // the copy works before logout

        var logout = await client.PostAsync("/auth/logout", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWithCookieAsync(api, copiedCookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/people", Ct)).StatusCode);
        Assert.NotNull(await api.WithContextAsync(c => c.AuthSessions.Select(s => s.RevokedAtUtc).SingleAsync()));
    }

    [Fact]
    public async Task Logout_without_a_session_is_a_harmless_no_op()
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Anonymous);
        var client = await api.CreateClientAsync(TestUser.Anonymous);
        (await client.GetAsync("/auth/antiforgery", Ct)).EnsureSuccessStatusCode();

        var response = await client.PostAsync("/auth/logout", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    public static TheoryData<string> SessionInvalidations => ["expired", "disabled", "security-stamp"];

    [Theory]
    [MemberData(nameof(SessionInvalidations))]
    public async Task Existing_sessions_stop_working_when_expired_disabled_or_the_security_stamp_changes(string change)
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Anonymous);
        var client = await api.CreateClientAsync(TestUser.Anonymous);
        var cookie = await SignInAndCaptureCookieAsync(api, client);

        switch (change)
        {
            case "expired":
                await api.WithContextAsync(c => c.AuthSessions.ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.ExpiresAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1))));
                break;
            case "disabled":
                await api.WithContextAsync(c => c.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.IsEnabled, false)));
                break;
            case "security-stamp":
                await using (var scope = api.Services.CreateAsyncScope())
                {
                    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    await users.UpdateSecurityStampAsync((await users.FindByNameAsync("admin"))!);
                }
                break;
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWithCookieAsync(api, cookie)).StatusCode);
    }

    // --- Actor on logs ---

    [Fact]
    public async Task Audits_record_the_signed_in_account_not_the_person_changed()
    {
        await using var api = await TestApi.CreateAsync();
        var adminId = await api.WithContextAsync(c => c.Users.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync());

        var created = await api.Client.PostAsJsonAsync("/api/people", new { name = "Grace Hopper" }, Ct);
        var personId = (await Json.ReadAsync(created)).GetProperty("id").GetGuid();

        var logs = await api.ProcessLogsAsync(expectedOutcomes: 1);
        var audit = Assert.Single(logs, l => l.Phase == ProcessLogPhase.MutationAudit);
        Assert.Equal(adminId.ToString(), audit.ActorSubject);
        Assert.NotEqual(personId.ToString(), audit.ActorSubject);
        Assert.Equal(adminId.ToString(), Assert.Single(logs, l => l.Phase == ProcessLogPhase.RequestOutcome).ActorSubject);
    }
}
