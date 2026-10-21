using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using StargateAPI.Application.Abstractions;
using StargateAPI.Controllers;
using StargateAPI.Domain;
using StargateAPI.Infrastructure.Persistence;

namespace StargateAPI.Tests;

/// <summary>
/// The onion dependency rule, checked against the compiled assemblies: references point inward
/// only (Api → Application → Domain; Infrastructure → Application → Domain), the Domain uses no
/// frameworks, and the use cases never see the SQLite provider. Project references alone would
/// not stop a package or a type from creeping into the wrong layer.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Person).Assembly;
    private static readonly Assembly Application = typeof(IStargateDbContext).Assembly;
    private static readonly Assembly Infrastructure = typeof(StargateContext).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private static string[] References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name!).Order().ToArray();

    [Fact]
    public void Domain_references_no_other_layer_and_no_framework()
    {
        var forbidden = References(Domain).Where(name =>
            name.StartsWith("StargateAPI", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.Data.Sqlite", StringComparison.Ordinal)
            || name == "MediatR");

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Application_references_only_the_domain_and_never_the_database_provider()
    {
        var references = References(Application);

        Assert.Equal(["StargateAPI.Domain"], references.Where(name => name.StartsWith("StargateAPI", StringComparison.Ordinal)));
        Assert.DoesNotContain(references, name =>
            name.StartsWith("Microsoft.Data.Sqlite", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_never_references_the_api()
    {
        Assert.DoesNotContain(Api.GetName().Name, References(Infrastructure));
    }

    [Fact]
    public void The_only_controller_is_the_sign_in_plumbing()
    {
        // The business API is GraphQL; AuthController issues and revokes the session cookie.
        var controllers = Api.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract);

        Assert.Equal([typeof(AuthController)], controllers);
    }

    [Fact]
    public void GraphQL_resolvers_depend_only_on_use_cases_never_on_infrastructure()
    {
        // Resolvers take their services as method parameters; DataLoaders and types as constructor
        // parameters or fields. None may reach the database other than through MediatR.
        var graphQLTypes = Api.GetTypes()
            .Where(type => type.Namespace == typeof(StargateAPI.GraphQL.PeopleQueries).Namespace)
            .ToArray();
        Assert.NotEmpty(graphQLTypes);

        const BindingFlags members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var offenders =
            from type in graphQLTypes
            from dependency in type.GetMethods(members).SelectMany(m => m.GetParameters())
                .Concat(type.GetConstructors(members).SelectMany(c => c.GetParameters()))
                .Select(p => p.ParameterType)
                .Concat(type.GetFields(members).Select(f => f.FieldType))
            where dependency.Assembly == Infrastructure
                || dependency.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true
            select $"{type.Name} depends on {dependency.FullName}";

        Assert.Empty(offenders);
    }
}
