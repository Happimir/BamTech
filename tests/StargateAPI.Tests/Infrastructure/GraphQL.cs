using System.Net.Http.Json;
using System.Text.Json;

namespace StargateAPI.Tests.Infrastructure;

/// <summary>
/// A GraphQL response: the HTTP response and its parsed body. Business errors are data (a
/// payload's <c>errors</c> list, or a result union member); request errors (invalid documents
/// or variables, denied fields, faults) are in the top-level <c>errors</c> list.
/// </summary>
public sealed record GraphQLResponse(HttpResponseMessage Http, JsonElement Body)
{
    public JsonElement Data => Body.GetProperty("data");

    /// <summary>A root field's value, such as the createPerson payload.</summary>
    public JsonElement this[string field] => Data.GetProperty(field);

    public bool HasRequestErrors => Body.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0;

    /// <summary>The first request error's <c>extensions.code</c>.</summary>
    public string? RequestErrorCode =>
        HasRequestErrors && Body.GetProperty("errors")[0].TryGetProperty("extensions", out var extensions)
            && extensions.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;

    public JsonElement RequestError => Body.GetProperty("errors")[0];

    /// <summary>The first business error of a mutation payload, or null when it succeeded.</summary>
    public JsonElement? PayloadError(string field) =>
        this[field].TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0
            ? errors[0]
            : null;

    /// <summary>The first payload error's GraphQL type, such as ChronologyConflictError.</summary>
    public string? PayloadErrorType(string field) => PayloadError(field)?.GetProperty("__typename").GetString();

    public string? PayloadErrorCode(string field) => PayloadError(field)?.GetProperty("code").GetString();

    /// <summary>The invalid fields of a ValidationError payload error, by name.</summary>
    public IReadOnlyList<string> InvalidFields(string field) =>
        PayloadError(field)?.GetProperty("fields").EnumerateArray().Select(f => f.GetProperty("field").GetString()!).ToList()
        ?? [];

    /// <summary>True when the mutation committed: no request errors and no payload errors.</summary>
    public bool Succeeded(string field) =>
        !HasRequestErrors && this[field].ValueKind == JsonValueKind.Object && PayloadError(field) is null;
}

/// <summary>The operation documents the tests send, with the fields a client would select.</summary>
public static class Operations
{
    public const string PersonFields = """
        id name version firstName middleName lastName
        astronaut { currentRank currentDutyTitle currentDutyStartDate careerStartDate careerEndDate status }
        """;

    public const string DutyFields = "id rank dutyTitle dutyStartDate dutyEndDate isRetirement";

    public const string ErrorFields = """
        errors { __typename ... on Error { code message } ... on ValidationError { fields { field messages } } }
        """;

    public const string CreatePerson = $$"""
        mutation CreatePerson($input: CreatePersonInput!) {
          createPerson(input: $input) { person { {{PersonFields}} } {{ErrorFields}} }
        }
        """;

    public const string UpdatePerson = $$"""
        mutation UpdatePerson($input: UpdatePersonInput!) {
          updatePerson(input: $input) { person { {{PersonFields}} } {{ErrorFields}} }
        }
        """;

    public const string UpsertPersonByName = $$"""
        mutation Upsert($input: UpsertPersonByNameInput!) {
          upsertPersonByName(input: $input) { created person { {{PersonFields}} } {{ErrorFields}} }
        }
        """;

    public const string AssignDuty = $$"""
        mutation AssignDuty($input: AssignDutyInput!) {
          assignDuty(input: $input) { duty { {{DutyFields}} } person { {{PersonFields}} } {{ErrorFields}} }
        }
        """;

    public const string Person = $$"""
        query Person($id: UUID!) { person(id: $id) { {{PersonFields}} } }
        """;

    public const string PersonWithDuties = $$"""
        query PersonWithDuties($id: UUID!) { person(id: $id) { {{PersonFields}} duties { {{DutyFields}} } } }
        """;

    public const string PersonByName = $$"""
        query PersonByName($name: String!) {
          personByName(name: $name) {
            __typename
            ... on Person { {{PersonFields}} duties { {{DutyFields}} } }
            ... on Error { code message }
            ... on ValidationError { fields { field messages } }
          }
        }
        """;

    public const string People = $$"""
        query People($page: Int, $pageSize: Int, $name: String) {
          people(page: $page, pageSize: $pageSize, name: $name) {
            page pageSize totalCount totalPages items { {{PersonFields}} }
          }
        }
        """;

    public const string BusinessCalendar = "query { businessCalendar { businessDate businessTimeZone } }";
}

public static class GraphQLClientExtensions
{
    /// <summary>Posts one operation to /graphql, as the UI does.</summary>
    public static async Task<GraphQLResponse> GraphQLAsync(this HttpClient client, string query, object? variables = null)
    {
        var response = await client.PostAsJsonAsync("/graphql", new { query, variables }, TestContext.Current.CancellationToken);
        return new GraphQLResponse(response, await Json.ReadAsync(response));
    }
}
