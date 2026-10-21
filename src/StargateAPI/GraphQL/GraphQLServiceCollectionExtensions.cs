using HotChocolate;
using HotChocolate.AspNetCore;
using HotChocolate.Execution;
using HotChocolate.Execution.Options;
using HotChocolate.Types;

// Names the source-generated registration method: AddActs().
[assembly: Module("Acts")]

namespace StargateAPI.GraphQL
{
    public static class GraphQLServiceCollectionExtensions
    {
        public const string Path = "/graphql";

        /// <summary>
        /// The GraphQL server: the source-generated types, mutation and query conventions (errors
        /// as data), field authorization with the API's policies, and limits suited to a public
        /// endpoint. Introspection and the Nitro IDE are available in Development only.
        /// </summary>
        public static IServiceCollection AddActsGraphQL(this IServiceCollection services, IWebHostEnvironment environment)
        {
            services.AddHttpContextAccessor();

            services.AddGraphQLServer()
                .AddActs()
                .AddMutationConventions(applyToAllMutations: true)
                .AddQueryConventions()
                .AddErrorInterfaceType<IUserError>()
                .AddAuthorization()
                // Schema services live in their own container; the filter's come from the application's.
                .AddErrorFilter(schemaServices => ActivatorUtilities.CreateInstance<GraphQLErrorFilter>(
                    schemaServices.GetRootServiceProvider()))
                .AddDiagnosticEventListener<OperationNameListener>()
                .AddDiagnosticEventListener<GraphQLRequestNameListener>()
                .ModifyOptions(options =>
                {
                    // Hot Chocolate gives each query resolver its own DI scope by default, so that
                    // concurrent resolvers never share a DbContext. Here every resolver uses the
                    // request's scope: the per-request OperationContext must see what each use case
                    // recorded for the process log. Sibling fields still resolve concurrently; the
                    // use cases take turns on the shared DbContext (OneRequestAtATimeBehavior),
                    // which keeps DataLoader batching.
                    options.DefaultQueryDependencyInjectionScope = DependencyInjectionScope.Request;
                })
                .ModifyRequestOptions(options =>
                {
                    // Exception messages can carry SQL or personal data; they go to the server log only.
                    options.IncludeExceptionDetails = false;
                })
                .AddMaxExecutionDepthRule(8)
                .DisableIntrospection(!environment.IsDevelopment())
                .ModifyServerOptions(options =>
                {
                    // One operation per POST. GET would put names from variables into URLs and logs.
                    options.EnableGetRequests = false;
                    options.Batching = AllowedBatching.None;
                    options.EnableMultipartRequests = false;
                    options.EnableSchemaRequests = environment.IsDevelopment();
                    options.Tool.Enable = environment.IsDevelopment();
                });

            return services;
        }
    }
}
