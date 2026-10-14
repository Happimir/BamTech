using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using StargateAPI.Application;
using StargateAPI.Application.Logging;
using StargateAPI.Controllers;
using StargateAPI.Domain.Results;
using StargateAPI.Hosting;
using StargateAPI.Infrastructure;
using StargateAPI.Infrastructure.Identity;
using StargateAPI.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options =>
    {
        // Command bodies are small; bound them so oversized payloads are rejected early.
        options.Filters.Add(new RequestSizeLimitAttribute(32 * 1024));
        // Every POST/PUT/PATCH/DELETE (including login and logout) must carry the antiforgery token.
        options.Filters.Add<AntiforgeryValidationFilter>();
    })
    .AddJsonOptions(options =>
    {
        // Unknown fields (including derived/read-only ones such as end dates) are a 400, not silently ignored.
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
            var problem = factory.CreateValidationProblemDetails(context.HttpContext, context.ModelState);
            problem.Extensions["code"] = ErrorCodes.Validation;
            context.HttpContext.RequestServices.GetService<OperationContext>()?.ErrorCode = ErrorCodes.Validation;
            return new BadRequestObjectResult(problem) { ContentTypes = { ControllerBaseExtensions.ProblemJson } };
        };
    });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Layers: use cases (Application) and their persistence implementation (Infrastructure).
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

// Business "today" (UTC) for date rules; replaceable in tests.
builder.Services.AddSingleton(TimeProvider.System);

// Identity, revocable cookie sessions, role policies (authentication required by default), antiforgery.
builder.Services.AddActsSecurity(builder.Configuration);

var app = builder.Build();

// Setup command: dotnet run --project src/StargateAPI -- seed-demo-users
if (args.Contains(DemoUserSeeder.Command))
{
    Environment.ExitCode = await DemoUserSeeder.RunAsync(
        app.Services, app.Configuration, app.Environment, app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Setup"));
    return;
}

// Configure the HTTP request pipeline.
// Optional: the built Angular app on the same origin (see SpaHosting); null leaves the API as is.
var spaFiles = SpaHosting.ResolveFiles(app.Configuration, app.Environment);
if (spaFiles is not null)
{
    app.UseSpaStaticFiles(spaFiles);
}

// Outermost for API requests: records one outcome per request after exception handling has produced the response.
app.UseMiddleware<RequestOutcomeLoggingMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();

// Record the authenticated account (never the selected Person) for audits and request outcomes.
app.Use(async (httpContext, next) =>
{
    if (httpContext.User.FindFirst(ClaimTypes.NameIdentifier) is { } subject)
    {
        var operation = httpContext.RequestServices.GetRequiredService<OperationContext>();
        operation.ActorSubject = subject.Value;
        operation.ActorIssuer = subject.Issuer;
    }
    await next(httpContext);
});

app.UseAuthorization();

app.MapControllers();
if (spaFiles is not null)
{
    app.MapSpaFallback(spaFiles);
}

app.Run();

// Exposes the implicit Program class to WebApplicationFactory in the test project.
public partial class Program { }
