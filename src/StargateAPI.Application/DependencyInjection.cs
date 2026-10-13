using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Application.Logging;

namespace StargateAPI.Application
{
    public static class DependencyInjection
    {
        /// <summary>The use cases (MediatR handlers) and the per-request logging context.</summary>
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

            // Per-request logging facts shared by the HTTP boundary, handlers and exception handling.
            services.AddScoped<OperationContext>();
            return services;
        }
    }
}
