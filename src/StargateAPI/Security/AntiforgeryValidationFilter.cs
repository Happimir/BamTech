using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using StargateAPI.Application.Logging;
using StargateAPI.Controllers;

namespace StargateAPI.Security
{
    /// <summary>
    /// Requires a valid antiforgery token on every state-changing request (POST, PUT, PATCH,
    /// DELETE), including login and logout. Runs after endpoint authorization, before model
    /// binding and the action. A failure is a 400 ProblemDetails with code csrf_failed: the client
    /// refreshes the token and the user resubmits; nothing is retried automatically.
    /// </summary>
    public sealed class AntiforgeryValidationFilter : IAsyncAuthorizationFilter
    {
        public const string CsrfFailed = "csrf_failed";

        private readonly IAntiforgery _antiforgery;

        public AntiforgeryValidationFilter(IAntiforgery antiforgery)
        {
            _antiforgery = antiforgery;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var method = context.HttpContext.Request.Method;
            if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
            {
                return;
            }

            try
            {
                await _antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
                var problem = factory.CreateProblemDetails(context.HttpContext, StatusCodes.Status400BadRequest,
                    title: "The request could not be verified. Refresh and try again.");
                problem.Extensions["code"] = CsrfFailed;
                context.HttpContext.RequestServices.GetService<OperationContext>()?.ErrorCode = CsrfFailed;
                context.Result = new ObjectResult(problem)
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    ContentTypes = { ControllerBaseExtensions.ProblemJson }
                };
            }
        }
    }
}
