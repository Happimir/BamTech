using HotChocolate.Execution;
using StargateAPI.Tests.Infrastructure;

namespace StargateAPI.Tests;

/// <summary>
/// The committed schema.graphql is the API's contract and the input to the UI's code
/// generator, so it must match what the server actually serves. When this fails after an
/// intended change, re-export it (README, "Schema") and review the diff like any contract change.
/// </summary>
public class SchemaTests
{
    public static readonly string CommittedSchemaPath = Path.Combine(RepositoryRoot(), "src", "StargateAPI", "schema.graphql");

    /// <summary>The SDL the server builds, as `schema export` writes it.</summary>
    public static async Task<string> CurrentSchemaAsync()
    {
        await using var api = await TestApi.CreateAsync(user: TestUser.Anonymous);
        var schema = await api.Services.GetSchemaAsync(cancellationToken: TestContext.Current.CancellationToken);
        return schema.ToString();
    }

    [Fact]
    public async Task Committed_schema_matches_the_served_schema()
    {
        var committed = await File.ReadAllTextAsync(CommittedSchemaPath, TestContext.Current.CancellationToken);

        Assert.Equal(Normalize(committed), Normalize(await CurrentSchemaAsync()));
    }

    private static string Normalize(string sdl) => sdl.ReplaceLineEndings("\n").Trim();

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StargateAPI.sln")))
            {
                return directory.FullName;
            }
        }
        throw new InvalidOperationException("The repository root (StargateAPI.sln) was not found.");
    }
}
