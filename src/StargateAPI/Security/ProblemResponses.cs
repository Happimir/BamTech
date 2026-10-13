using Microsoft.AspNetCore.Mvc;
using StargateAPI.Business.Logging;

namespace StargateAPI.Security
{
    /// <summary>ProblemDetails responses produced outside MVC actions (authentication events).</summary>
    public static class ProblemResponses
    {
        public static async Task WriteAsync(HttpContext httpContext, int status, string code, string title)
        {
            httpContext.RequestServices.GetService<OperationContext>()?.ErrorCode = code;
            httpContext.Response.StatusCode = status;
            await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails { Status = status, Title = title, Extensions = { ["code"] = code } }
            });
        }
    }
}
