using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Infrastructure.Persistence;

namespace StargateAPI.Tests.Infrastructure;

/// <summary>Records the SQL of every query the application runs, to prove how many statements a request costs.</summary>
public sealed class CommandCounter : DbCommandInterceptor
{
    private readonly ConcurrentQueue<string> _commands = new();

    public static void Register(IServiceCollection services)
    {
        var counter = new CommandCounter();
        services.AddSingleton(counter);
        services.ConfigureDbContext<StargateContext>(options => options.AddInterceptors(counter));
    }

    public static CommandCounter For(TestApi api) => api.Services.GetRequiredService<CommandCounter>();

    public void Reset() => _commands.Clear();

    public int Count(Func<string, bool> predicate) => _commands.Count(predicate);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        _commands.Enqueue(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
