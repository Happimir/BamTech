using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Application.Abstractions;
using StargateAPI.Application.Logging;

namespace StargateAPI.Infrastructure.Persistence
{
    internal sealed class RequestOutcomeLog(IServiceScopeFactory scopeFactory) : IRequestOutcomeLog
    {
        public async Task WriteAsync(ProcessLog entry, TimeSpan timeout, CancellationToken cancellationToken)
        {
            // A fresh scope and context: no pending business changes from the request can leak
            // into this save, and the wait is bounded both client-side and at the database.
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            bounded.CancelAfter(timeout);
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
            context.Database.SetCommandTimeout(timeout);
            context.ProcessLogs.Add(entry);
            await context.SaveChangesAsync(bounded.Token);
        }
    }
}
