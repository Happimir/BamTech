using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HotChocolate.Language;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Application.Logging;
using StargateAPI.Infrastructure.Identity;
using StargateAPI.Security;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// Sign-in, revocable sessions, CSRF, and role policies on every GraphQL field.
/// </summary>
public class AuthTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await Json.ReadAsync(response)).GetProperty("code").GetString();

    // --- Route and field protection ---

    [Fact]
    public async Task Only_the_sign_in_routes_are_anonymous()
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Anonymous);
        var endpoints = api.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        var anonymous = endpoints
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is { } action
                ? $"{action.ControllerName}.{action.ActionName}"
                : e.DisplayName)
            .Order().ToList();

        // The GraphQL endpoint falls under the signed-in-by-default policy; its fields add role policies.
        Assert.Equal(["Auth.Antiforgery", "Auth.Login", "Auth.Logout"], anonymous);
    }

    [Fact]
    public async Task Every_root_field_declares_a_policy_and_every_mutation_a_write_policy()
    {
        var schema = Utf8GraphQLParser.Parse(await SchemaTests.CurrentSchemaAsync());
        var roots = schema.Definitions.OfType<ObjectTypeDefinitionNode>()
            .Where(type => type.Name.Value is "Query" or "Mutation")
            .ToList();
        Assert.Equal(2, roots.Count);

        foreach (var root in roots)
        {
            foreach (var field in root.Fields)
            {
                var policies = field.Directives.Where(d => d.Name.Value == "authorize")
                    .SelectMany(d => d.Arguments.Where(a => a.Name.Value == "policy"))
                    .Select(a => ((StringValueNode)a.Value).Value)
                    .ToList();
                Assert.True(policies.Count > 0, $"{root.Name.Value}.{field.Name.Value} has no policy.");
                if (root.Name.Value == "Mutation")
                {
                    Assert.True(policies.Contains(Policies.PeopleWrite) || policies.Contains(Policies.DutiesWrite),
                        $"Mutation.{field.Name.Value} writes without a write policy.");
                }
            }
        }
    }

    [Fact]
    public async Task Anonymous_requests_get_401_problem_details_not_a_login_redirect()
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Anonymous);

        var graphql = await api.Client.PostAsJsonAsync("/graphql", new { query = Operations.BusinessCalendar }, Ct);
        var me = await api.Client.GetAsync("/auth/me", Ct);

        foreach (var response in new[] { graphql, me })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Null(response.Headers.Location);
            Assert.Equal("unauthenticated", await CodeAsync(response));
        }
    }

    [Fact]
    public async Task A_browser_opening_a_protected_url_signed_out_gets_401_not_a_server_error()
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Anonymous);

        async Task<HttpResponseMessage> OpenAsync(string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Accept.ParseAdd("text/html");
            return await api.Client.SendAsync(request, Ct);
        }

        // No ProblemDetails writer accepts text/html; the status alone still says what happened.
        Assert.Equal(HttpStatusCode.Unauthorized, (await OpenAsync("/graphql")).StatusCode);   // the Development IDE's URL
        Assert.Equal(HttpStatusCode.Unauthorized, (await OpenAsync("/auth/me")).StatusCode);
        var outcome = Assert.Single(await api.ProcessLogsAsync(expectedOutcomes: 1));
        Assert.Equal((ProcessLogOutcome.Rejected, "unauthenticated", "Auth.Me"), (outcome.Outcome, outcome.ErrorCode, outcome.Operation));
    }

    public static TheoryData<string, string> WriteOperations => new()
    {
        { Operations.CreatePerson, """{"input":{"name":"New Person"}}""" },
        { Operations.UpdatePerson, """{"input":{"id":"{id}","name":"Renamed","version":"{version}"}}""" },
        { Operations.UpsertPersonByName, """{"input":{"lookupName":"New Person","name":"New Person"}}""" },
        { Operations.AssignDuty, """{"input":{"person":{"id":"{id}"},"rank":"1LT","dutyTitle":"Pilot","dutyStartDate":"2020-01-01"}}""" },
        { Operations.AssignDuty, """{"input":{"person":{"name":"Grace Hopper"},"rank":"1LT","dutyTitle":"Pilot","dutyStartDate":"2020-01-01"}}""" }
    };

    [Theory]
    [MemberData(nameof(WriteOperations))]
    public async Task Reader_is_forbidden_on_every_mutation_and_nothing_changes(string query, string variables)
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Reader);
        var id = await api.SeedPersonAsync("Grace Hopper");
        var version = await api.WithContextAsync(c => c.People.Where(p => p.Id == id).Select(p => p.Version).SingleAsync());

        var response = await api.GraphQLAsync(query,
            JsonDocument.Parse(variables.Replace("{id}", id.ToString()).Replace("{version}", version.ToString())).RootElement);

        Assert.Equal("forbidden", response.RequestErrorCode);
        Assert.Equal(1, await api.WithContextAsync(c => c.People.CountAsync()));
        Assert.Equal("Grace Hopper", await api.WithContextAsync(c => c.People.Select(p => p.Name).SingleAsync()));
        Assert.Equal(0, await api.WithContextAsync(c => c.AstronautDuties.CountAsync()));
        var logs = await api.ProcessLogsAsync(expectedOutcomes: 1);
        Assert.DoesNotContain(logs, l => l.Phase == ProcessLogPhase.MutationAudit);
        Assert.Equal((ProcessLogOutcome.Rejected, "forbidden"), (logs.Single().Outcome, logs.Single().ErrorCode));
    }

    [Fact]
    public async Task Reader_can_use_every_query()
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Reader);
        var id = await api.SeedPersonAsync("Grace Hopper");

        var reads = new[]
        {
            await api.GraphQLAsync(Operations.People),
            await api.GraphQLAsync(Operations.PersonWithDuties, new { id }),
            await api.GraphQLAsync(Operations.PersonByName, new { name = "Grace Hopper" }),
            await api.GraphQLAsync(Operations.BusinessCalendar)
        };

        Assert.All(reads, read => Assert.False(read.HasRequestErrors, read.Body.GetRawText()));
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/auth/me", Ct)).StatusCode);
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
    public async Task Signed_in_mutation_with_a_bad_antiforgery_token_changes_nothing()
    {
        await using var api = await TestApi.CreateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = JsonContent.Create(new { query = Operations.CreatePerson, variables = new { input = new { name = "Forged" } } })
        };
        request.Headers.Add(SecurityServiceCollectionExtensions.XsrfHeaderName, "forged-token");

        var response = await api.Client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("csrf_failed", await CodeAsync(response));
        Assert.Equal(0, await api.WithContextAsync(c => c.People.CountAsync()));
    }

    [Fact]
    public async Task Queries_need_the_antiforgery_token_too_because_every_operation_is_a_post()
    {
        await using var api = await TestApi.CreateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = JsonContent.Create(new { query = Operations.BusinessCalendar })
        };
        request.Headers.Add(SecurityServiceCollectionExtensions.XsrfHeaderName, "forged-token");

        var response = await api.Client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("csrf_failed", await CodeAsync(response));
    }

    // --- Sessions ---

    private static async Task<string> SignInAndCaptureCookieAsync(TestApi api, HttpClient client)
    {
        var login = await TestApi.LoginAsync(client, "admin", TestApi.AdminPassword);
        login.EnsureSuccessStatusCode();
        var setCookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(SecurityServiceCollectionExtensions.AuthCookieName + "="));
        return setCookie.Split(';')[0];
    }

    /// <summary>A signed-in read with only the given cookie (no antiforgery token, so a GET).</summary>
    private static Task<HttpResponseMessage> GetWithCookieAsync(TestApi api, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
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
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GraphQLAsync(Operations.People)).Http.StatusCode);
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
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GraphQLAsync(Operations.People)).Http.StatusCode);
    }

    // --- Actor on logs ---

    [Fact]
    public async Task Audits_record_the_signed_in_account_not_the_person_changed()
    {
        await using var api = await TestApi.CreateAsync();
        var adminId = await api.WithContextAsync(c => c.Users.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync());

        var created = await api.GraphQLAsync(Operations.CreatePerson, new { input = new { name = "Grace Hopper" } });
        var personId = created["createPerson"].GetProperty("person").GetProperty("id").GetGuid();

        var logs = await api.ProcessLogsAsync(expectedOutcomes: 1);
        var audit = Assert.Single(logs, l => l.Phase == ProcessLogPhase.MutationAudit);
        Assert.Equal(adminId.ToString(), audit.ActorSubject);
        Assert.NotEqual(personId.ToString(), audit.ActorSubject);
        Assert.Equal(adminId.ToString(), Assert.Single(logs, l => l.Phase == ProcessLogPhase.RequestOutcome).ActorSubject);
    }
}
