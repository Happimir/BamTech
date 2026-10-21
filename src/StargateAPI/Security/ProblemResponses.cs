using Microsoft.AspNetCore.Mvc;
using StargateAPI.Application.Logging;

namespace StargateAPI.Security
{
    /// <summary>ProblemDetails responses produced outside MVC actions (authentication events).</summary>
    public static class ProblemResponses
    {
        /// <summary>
        /// Sets the status and writes the ProblemDetails body when the client accepts JSON. A client
        /// that accepts only other types (a browser opening the URL) gets the status with no body.
        /// </summary>
        public static async Task WriteAsync(HttpContext httpContext, int status, string code, string title)
        {
            httpContext.RequestServices.GetService<OperationContext>()?.ErrorCode = code;
            httpContext.Response.StatusCode = status;
            await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails { Status = status, Title = title, Extensions = { ["code"] = code } }
            });
        }
    }
}
