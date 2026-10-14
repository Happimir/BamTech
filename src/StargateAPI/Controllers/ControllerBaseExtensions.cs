using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using StargateAPI.Application.Logging;
using StargateAPI.Domain.Results;

namespace StargateAPI.Controllers
{
    /// <summary>Maps application results to HTTP: success bodies, or ProblemDetails with a stable code.</summary>
    public static class ControllerBaseExtensions
    {
        public const string ProblemJson = "application/problem+json";

        public static IActionResult ToActionResult<T>(this ControllerBase controller, Result<T> result, Func<T, IActionResult> onSuccess) =>
            result.IsSuccess ? onSuccess(result.Value) : controller.ToProblem(result.Error);

        public static IActionResult ToProblem(this ControllerBase controller, AppError error)
        {
            var status = error.Kind switch
            {
                ErrorKind.Validation => StatusCodes.Status400BadRequest,
                ErrorKind.NotFound => StatusCodes.Status404NotFound,
                ErrorKind.Conflict => StatusCodes.Status409Conflict,
                ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status500InternalServerError
            };

            ProblemDetails problem;
            if (error.FieldErrors is { Count: > 0 })
            {
                var modelState = new ModelStateDictionary();
                foreach (var (field, messages) in error.FieldErrors)
                {
                    foreach (var message in messages)
                    {
                        modelState.AddModelError(field, message);
                    }
                }
                problem = controller.ProblemDetailsFactory.CreateValidationProblemDetails(
                    controller.HttpContext, modelState, status, detail: error.Message);
            }
            else
            {
                problem = controller.ProblemDetailsFactory.CreateProblemDetails(
                    controller.HttpContext, status, detail: error.Message);
            }

            problem.Extensions["code"] = error.Code;
            controller.HttpContext.RequestServices.GetService<OperationContext>()?.ErrorCode = error.Code;
            return new ObjectResult(problem) { StatusCode = status, ContentTypes = { ProblemJson } };
        }
    }
}
