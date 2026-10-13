using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Business.Data;

namespace StargateAPI.Tests.Infrastructure;

/// <summary>Injects a failure into SaveChanges when the pending changes match a condition.</summary>
public sealed class FailingSaveInterceptor : SaveChangesInterceptor
{
    private readonly Func<DbContext, bool> _shouldFail;
    private readonly Func<Exception> _exception;

    private FailingSaveInterceptor(Func<DbContext, bool> shouldFail, Func<Exception> exception)
    {
        _shouldFail = shouldFail;
        _exception = exception;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && _shouldFail(context))
        {
            throw _exception();
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>Registers the interceptor on every StargateContext the application creates.</summary>
    public static Action<IServiceCollection> When(Func<DbContext, bool> shouldFail, Func<Exception> exception) =>
        services => services.ConfigureDbContext<StargateContext>(options =>
            options.AddInterceptors(new FailingSaveInterceptor(shouldFail, exception)));

    public static bool Adding<T>(DbContext context, Func<T, bool>? predicate = null) where T : class =>
        context.ChangeTracker.Entries<T>().Any(e => e.State == EntityState.Added && (predicate?.Invoke(e.Entity) ?? true));
}
