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
    public void Business_controllers_depend_only_on_use_cases_never_on_infrastructure()
    {
        // AuthController is the ASP.NET sign-in plumbing and legitimately uses Identity types.
        var businessControllers = Api.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract && type != typeof(AuthController))
            .ToArray();
        Assert.NotEmpty(businessControllers);

        var offenders =
            from controller in businessControllers
            from dependency in controller.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
                .Concat(controller.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Select(f => f.FieldType))
            where dependency.Assembly == Infrastructure
                || dependency.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true
            select $"{controller.Name} depends on {dependency.FullName}";

        Assert.Empty(offenders);
    }
}
