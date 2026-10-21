using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;

namespace StargateAPI.Security
{
    /// <summary>
    /// What the MVC filters give the sign-in routes, for the GraphQL endpoint: an antiforgery token
    /// on every POST (reads included, since every operation is a POST) and a 32 KiB body limit.
    /// Runs after authorization, so an anonymous request is a 401 before the token is checked.
    /// </summary>
    public sealed class GraphQLRequestGuard
    {
        public const long MaxRequestBodySize = 32 * 1024;

        private readonly RequestDelegate _next;
        private readonly IAntiforgery _antiforgery;

        public GraphQLRequestGuard(RequestDelegate next, IAntiforgery antiforgery)
        {
            _next = next;
            _antiforgery = antiforgery;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            if (httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodySize)
            {
                bodySize.MaxRequestBodySize = MaxRequestBodySize;
            }

            if (HttpMethods.IsPost(httpContext.Request.Method))
            {
                try
                {
                    await _antiforgery.ValidateRequestAsync(httpContext);
                }
                catch (AntiforgeryValidationException)
                {
                    await ProblemResponses.WriteAsync(httpContext, StatusCodes.Status400BadRequest,
                        AntiforgeryValidationFilter.CsrfFailed, "The request could not be verified. Refresh and try again.");
                    return;
                }
            }

            await _next(httpContext);
        }
    }
}
