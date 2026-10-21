using MediatR;

namespace StargateAPI.Application.Services
{
    /// <summary>The per-request (scoped) lock that <see cref="OneRequestAtATimeBehavior{TRequest, TResponse}"/> takes.</summary>
    public sealed class RequestGate : IDisposable
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);

        public void Dispose() => Lock.Dispose();
    }

    /// <summary>
    /// Runs the use cases of one request (one DI scope) one at a time. They share the scope's
    /// DbContext, which allows a single operation at a time, while GraphQL resolves sibling
    /// fields concurrently (a payload's duty and person, a DataLoader batch beside another
    /// field). Serializing here, rather than making every resolver serial, keeps DataLoader
    /// batching: keys are collected while resolvers wait, then loaded in one query.
    /// Handlers never send other requests, so a handler holding the lock cannot wait on it.
    /// </summary>
    public sealed class OneRequestAtATimeBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly RequestGate _gate;

        public OneRequestAtATimeBehavior(RequestGate gate)
        {
            _gate = gate;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            await _gate.Lock.WaitAsync(cancellationToken);
            try
            {
                return await next();
            }
            finally
            {
                _gate.Lock.Release();
            }
        }
    }
}
