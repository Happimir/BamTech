using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Domain;
using StargateAPI.Application.Logging;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// Success and exception logging stored in the database: audits commit atomically with the
/// change, every request gets exactly one outcome named by its GraphQL operation, failures are
/// diagnosable without leaking exception text, and a logging outage never changes the result.
/// </summary>
public class ProcessLoggingTests
{
    private const string Secret = "SECRET-connection=Data Source=prod;Password=hunter2";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Successful_mutation_writes_an_audit_and_an_outcome_for_the_same_operation()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Grace Hopper");

        var response = await api.AssignDutyAsync(id, "1LT", "Pilot", "2020-01-01");
        var dutyId = response["assignDuty"].GetProperty("duty").GetProperty("id").GetGuid();

        var logs = await api.ProcessLogsAsync(expectedOutcomes: 1);
        var audit = Assert.Single(logs, l => l.Phase == ProcessLogPhase.MutationAudit);
        var outcome = Assert.Single(logs, l => l.Phase == ProcessLogPhase.RequestOutcome);
        Assert.Equal(("AssignDuty", id, dutyId), (audit.Operation, audit.PersonId!.Value, audit.AstronautDutyId!.Value));
        Assert.Contains("\"dutyTitle\":\"Pilot\"", audit.ChangesJson);
        Assert.DoesNotContain("Grace", audit.ChangesJson);                 // no personal names in audits
        Assert.Equal((ProcessLogOutcome.Succeeded, 200, "mutation assignDuty"), (outcome.Outcome, outcome.HttpStatusCode, outcome.Operation));
        Assert.Null(outcome.ErrorCode);
        Assert.Equal(audit.OperationId, outcome.OperationId);
        Assert.Equal(audit.TraceId, outcome.TraceId);
        Assert.Equal(TimeSpan.Zero, outcome.OccurredAtUtc.Offset);
        Assert.InRange(outcome.OccurredAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Business_error_returned_as_data_is_logged_as_rejected_with_its_code_and_no_audit()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Grace Hopper");
        Assert.True((await api.AssignDutyAsync(id, "1LT", "Pilot", "2020-01-01")).Succeeded("assignDuty"));

        var response = await api.AssignDutyAsync(id, "1LT", "Pilot", "2020-01-01");

        // GraphQL answers 200: the refusal is data. The log still records it as refused.
        Assert.Equal(HttpStatusCode.OK, response.Http.StatusCode);
        var logs = await api.ProcessLogsAsync(expectedOutcomes: 2);
        var rejected = Assert.Single(logs, l => l.Outcome == ProcessLogOutcome.Rejected);
        Assert.Equal(("chronology_conflict", id), (rejected.ErrorCode, rejected.PersonId!.Value));
        // The client can quote the trace ID of a refusal that has no error body.
        Assert.Equal(rejected.TraceId, Assert.Single(response.Http.Headers.GetValues("traceresponse")));
        Assert.Single(logs, l => l.Phase == ProcessLogPhase.MutationAudit);   // only the first, successful duty
        Assert.DoesNotContain(logs, l => l.Phase == ProcessLogPhase.MutationAudit && l.OperationId == rejected.OperationId);
    }

    [Fact]
    public async Task Failure_to_save_the_audit_rolls_back_the_business_change()
    {
        await using var api = await TestApi.CreateAsync(configureServices: FailingSaveInterceptor.When(
            context => FailingSaveInterceptor.Adding<ProcessLog>(context, l => l.Phase == ProcessLogPhase.MutationAudit),
            () => new InvalidOperationException("audit store failed")));
        var id = await api.SeedPersonAsync("Grace Hopper");
        var versionBefore = await api.WithContextAsync(context => context.People.Where(p => p.Id == id).Select(p => p.Version).SingleAsync());

        var response = await api.AssignDutyAsync(id, "1LT", "Pilot", "2020-01-01");

        Assert.Equal("internal_error", response.RequestErrorCode);
        // The first save (version bump) and the second (duty, detail, audit) roll back together.
        Assert.Equal(0, await api.WithContextAsync(context => context.AstronautDuties.CountAsync()));
        Assert.Equal(0, await api.WithContextAsync(context => context.AstronautDetails.CountAsync()));
        Assert.Equal(versionBefore, await api.WithContextAsync(context => context.People.Where(p => p.Id == id).Select(p => p.Version).SingleAsync()));
        var outcome = Assert.Single(await api.ProcessLogsAsync(expectedOutcomes: 1));
        Assert.Equal((ProcessLogOutcome.Failed, "internal_error"), (outcome.Outcome, outcome.ErrorCode));
    }

    [Fact]
    public async Task Unhandled_exception_is_diagnosable_by_trace_id_without_its_message()
    {
        await using var api = await TestApi.CreateAsync(configureServices: FailingSaveInterceptor.When(
            context => FailingSaveInterceptor.Adding<AstronautDuty>(context),
            () => new InvalidOperationException(Secret)));
        var id = await api.SeedPersonAsync("Grace Hopper");

        var response = await api.AssignDutyAsync(id, "1LT", "Pilot", "2020-01-01");

        var error = response.RequestError;
        var outcome = Assert.Single(await api.ProcessLogsAsync(expectedOutcomes: 1));
        Assert.Equal(error.GetProperty("extensions").GetProperty("traceId").GetString(), outcome.TraceId);
        Assert.Equal("An unexpected error occurred.", error.GetProperty("message").GetString());
        Assert.DoesNotContain("hunter2", response.Body.GetRawText());
        Assert.Equal((ProcessLogOutcome.Failed, "internal_error"), (outcome.Outcome, outcome.ErrorCode));
        Assert.Equal("System.InvalidOperationException", outcome.ExceptionType);
        Assert.Contains("AssignDutyHandler", outcome.ExceptionDetails);    // the stack trace is kept
        Assert.DoesNotContain("hunter2", outcome.ExceptionDetails);
        Assert.DoesNotContain("hunter2", outcome.Message);
    }

    [Fact]
    public async Task Malformed_request_before_any_resolver_is_logged_as_rejected()
    {
        await using var api = await TestApi.CreateAsync();

        var notJson = await api.Client.PostAsync("/graphql", new StringContent("{ not json", Encoding.UTF8, "application/json"), Ct);
        var unknownField = await api.GraphQLAsync("query { people { items { salary } } }");

        Assert.Equal(HttpStatusCode.BadRequest, notJson.StatusCode);
        Assert.True(unknownField.HasRequestErrors);
        var logs = await api.ProcessLogsAsync(expectedOutcomes: 2);
        Assert.All(logs, l => Assert.Equal((ProcessLogOutcome.Rejected, "graphql"), (l.Outcome, l.Operation)));
    }

    [Fact]
    public async Task Reads_are_logged_by_root_field_never_by_alias_operation_name_or_argument()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Grace Hopper");

        await api.GraphQLAsync(Operations.Person, new { id });
        await api.GraphQLAsync("""
            query FindGraceHopper { grace: personByName(name: "Grace Hopper") { __typename } businessCalendar { businessDate } }
            """);
        await api.Client.GetAsync("/no/such/route", Ct);

        var logs = await api.ProcessLogsAsync(expectedOutcomes: 3);
        Assert.Equal(["query person", "query personByName+businessCalendar", "Unmatched"], logs.Select(l => l.Operation));
        Assert.Equal([(ProcessLogOutcome.Succeeded, (Guid?)id, (string?)null), (ProcessLogOutcome.Succeeded, id, null)],
            logs.Take(2).Select(l => (l.Outcome, l.PersonId, l.ErrorCode)));
        Assert.Equal(ProcessLogOutcome.Rejected, logs[2].Outcome);
        Assert.DoesNotContain(logs, l => l.Message.Contains("Grace") || l.Operation.Contains("Grace"));
    }

    [Fact]
    public async Task Request_logging_outage_does_not_change_the_business_result()
    {
        await using var api = await TestApi.CreateAsync(configureServices: FailingSaveInterceptor.When(
            context => FailingSaveInterceptor.Adding<ProcessLog>(context, l => l.Phase == ProcessLogPhase.RequestOutcome),
            () => new InvalidOperationException("log store unavailable")));
        var id = await api.SeedPersonAsync("Grace Hopper");

        var write = await api.AssignDutyAsync(id, "1LT", "Pilot", "2020-01-01");
        var read = await api.GraphQLAsync(Operations.PersonWithDuties, new { id });

        Assert.True(write.Succeeded("assignDuty"));
        Assert.Equal(1, read["person"].GetProperty("duties").GetArrayLength());
        Assert.Equal(1, await api.WithContextAsync(context => context.AstronautDuties.CountAsync()));
        // The audit is part of the business transaction and still committed.
        var logs = await api.ProcessLogsAsync(expectedOutcomes: 0);
        Assert.Equal(ProcessLogPhase.MutationAudit, Assert.Single(logs).Phase);
    }
}
