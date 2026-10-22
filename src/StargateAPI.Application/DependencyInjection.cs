using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Application.Logging;
using StargateAPI.Application.Services;

namespace StargateAPI.Application
{
    public static class DependencyInjection
    {
        /// <summary>The use cases (MediatR handlers), the services they share, and the per-request logging context.</summary>
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
                cfg.AddOpenBehavior(typeof(OneRequestAtATimeBehavior<,>));
            });
            services.AddScoped<RequestGate>();

            // Per-request logging facts shared by the HTTP boundary, handlers and exception handling.
            services.AddScoped<OperationContext>();

            // Shared by the write handlers; scoped to share the request's DbContext and operation.
            services.AddScoped<IAuditTrail, AuditTrail>();
            services.AddScoped<IAstronautCareers, AstronautCareers>();
            return services;
        }
    }
}
