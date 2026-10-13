using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using StargateAPI.Security;

namespace StargateAPI.Tests;

public class DemoUserSeederTests
{
    [Fact]
    public void Development_without_configuration_uses_the_documented_demo_passwords()
    {
        var passwords = DemoUserSeeder.ResolvePasswords(Configuration(), Environment(Environments.Development));

        Assert.Equal((DemoUserSeeder.DevelopmentAdminPassword, DemoUserSeeder.DevelopmentReaderPassword), passwords);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public void Demo_passwords_never_apply_outside_development(string environmentName)
    {
        Assert.Null(DemoUserSeeder.ResolvePasswords(Configuration(), Environment(environmentName)));
    }

    [Fact]
    public void Configured_passwords_win_in_development()
    {
        var configuration = Configuration(("DemoUsers:AdminPassword", "Own#Admin1"), ("DemoUsers:ReaderPassword", "Own#Reader1"));

        Assert.Equal(("Own#Admin1", "Own#Reader1"), DemoUserSeeder.ResolvePasswords(configuration, Environment(Environments.Development)));
    }

    [Fact]
    public void Configured_passwords_are_used_outside_development()
    {
        var configuration = Configuration(("DemoUsers:AdminPassword", "Own#Admin1"), ("DemoUsers:ReaderPassword", "Own#Reader1"));

        Assert.Equal(("Own#Admin1", "Own#Reader1"), DemoUserSeeder.ResolvePasswords(configuration, Environment(Environments.Production)));
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static IHostEnvironment Environment(string name) => new StubHostEnvironment { EnvironmentName = name };

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "StargateAPI";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
