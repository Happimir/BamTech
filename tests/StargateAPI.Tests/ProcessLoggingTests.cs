using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Domain;
using StargateAPI.Application.Logging;
using StargateAPI.Infrastructure.Identity;
using StargateAPI.Infrastructure.Persistence;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// Success and exception logging stored in the database: audits commit atomically with the
/// change, every request gets exactly one outcome, failures are diagnosable without leaking
/// exception text, and a logging outage never changes the business result.
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

        var response = await api.PostDutyByIdAsync(id, "1LT", "Pilot", "2020-01-01");
        var dutyId = (await Json.ReadAsync(response)).GetProperty("id").GetGuid();

        var logs = await api.ProcessLogsAsync(expectedOutcomes: 1);
        var audit = Assert.Single(logs, l => l.Phase == ProcessLogPhase.MutationAudit);
        var outcome = Assert.Single(logs, l => l.Phase == ProcessLogPhase.RequestOutcome);
        Assert.Equal(("AssignDuty", id, dutyId), (audit.Operation, audit.PersonId!.Value, audit.AstronautDutyId!.Value));
        Assert.Contains("\"dutyTitle\":\"Pilot\"", audit.ChangesJson);
        Assert.DoesNotContain("Grace", audit.ChangesJson);                 // no personal names in audits
        Assert.Equal((ProcessLogOutcome.Succeeded, 201, "People.AssignDuty"), (outcome.Outcome, outcome.HttpStatusCode, outcome.Operation));
        Assert.Equal(audit.OperationId, outcome.OperationId);
        Assert.Equal(audit.TraceId, outcome.TraceId);
        Assert.Equal(TimeSpan.Zero, outcome.OccurredAtUtc.Offset);
        Assert.InRange(outcome.OccurredAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Rejected_mutation_writes_an_outcome_with_its_code_and_no_audit()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Grace Hopper");
        (await api.PostDutyByIdAsync(id, "1LT", "Pilot", "2020-01-01")).EnsureSuccessStatusCode();

        var response = await api.PostDutyByIdAsync(id, "1LT", "Pilot", "2020-01-01");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var logs = await api.ProcessLogsAsync(expectedOutcomes: 2);
        var rejected = Assert.Single(logs, l => l.Outcome == ProcessLogOutcome.Rejected);
        Assert.Equal(("chronology_conflict", 409, id), (rejected.ErrorCode, rejected.HttpStatusCode!.Value, rejected.PersonId!.Value));
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

        var response = await api.PostDutyByIdAsync(id, "1LT", "Pilot", "2020-01-01");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
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

        var response = await api.PostDutyByIdAsync(id, "1LT", "Pilot", "2020-01-01");

        var problem = await Json.ReadAsync(response);
        var outcome = Assert.Single(await api.ProcessLogsAsync(expectedOutcomes: 1));
        Assert.Equal(problem.GetProperty("traceId").GetString(), outcome.TraceId);
        Assert.Equal((ProcessLogOutcome.Failed, 500, "internal_error"), (outcome.Outcome, outcome.HttpStatusCode!.Value, outcome.ErrorCode));
        Assert.Equal("System.InvalidOperationException", outcome.ExceptionType);
        Assert.Contains("AssignDutyHandler", outcome.ExceptionDetails);    // the stack trace is kept
        Assert.DoesNotContain("hunter2", outcome.ExceptionDetails);
        Assert.DoesNotContain("hunter2", outcome.Message);
    }

    [Fact]
    public async Task Binding_failure_before_the_handler_is_logged_as_a_rejected_validation()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Grace Hopper");

        var response = await api.Client.PostAsync($"/api/people/{id}/duties",
            new StringContent("{ not json", Encoding.UTF8, "application/json"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var outcome = Assert.Single(await api.ProcessLogsAsync(expectedOutcomes: 1));
        Assert.Equal((ProcessLogOutcome.Rejected, "validation", "People.AssignDuty"), (outcome.Outcome, outcome.ErrorCode, outcome.Operation));
    }

    [Fact]
    public async Task Reads_are_logged_by_operation_name_without_url_names_and_metadata_is_excluded()
    {
        await using var api = await TestApi.CreateAsync();
        var id = await api.SeedPersonAsync("Grace Hopper");

        (await api.Client.GetAsync($"/api/people/{id}", Ct)).EnsureSuccessStatusCode();
        (await api.Client.GetAsync("/Person/Grace%20Hopper", Ct)).EnsureSuccessStatusCode();
        (await api.Client.GetAsync("/api/metadata", Ct)).EnsureSuccessStatusCode();
        await api.Client.GetAsync("/no/such/route", Ct);

        var logs = await api.ProcessLogsAsync(expectedOutcomes: 3);
        Assert.Equal(["People.GetPerson", "Person.GetPersonByName", "Unmatched"], logs.Select(l => l.Operation));
        Assert.All(logs.Take(2), l => Assert.Equal((ProcessLogOutcome.Succeeded, id), (l.Outcome, l.PersonId!.Value)));
        Assert.Equal(ProcessLogOutcome.Rejected, logs[2].Outcome);
        Assert.DoesNotContain(logs, l => l.Message.Contains("Grace"));
    }

    [Fact]
    public async Task Request_logging_outage_does_not_change_the_business_result()
    {
        await using var api = await TestApi.CreateAsync(configureServices: FailingSaveInterceptor.When(
            context => FailingSaveInterceptor.Adding<ProcessLog>(context, l => l.Phase == ProcessLogPhase.RequestOutcome),
            () => new InvalidOperationException("log store unavailable")));
        var id = await api.SeedPersonAsync("Grace Hopper");

        var write = await api.PostDutyByIdAsync(id, "1LT", "Pilot", "2020-01-01");
        var read = await api.Client.GetAsync($"/api/people/{id}/duties", Ct);

        Assert.Equal(HttpStatusCode.Created, write.StatusCode);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(1, await api.WithContextAsync(context => context.AstronautDuties.CountAsync()));
        // The audit is part of the business transaction and still committed.
        var logs = await api.ProcessLogsAsync(expectedOutcomes: 0);
        Assert.Equal(ProcessLogPhase.MutationAudit, Assert.Single(logs).Phase);
    }
}
