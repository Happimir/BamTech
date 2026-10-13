using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Business.Logging;
using StargateAPI.Business.Results;
using StargateAPI.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options =>
    {
        // Command bodies are small; bound them so oversized payloads are rejected early.
        options.Filters.Add(new RequestSizeLimitAttribute(32 * 1024));
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
builder.Services.AddDbContext<StargateContext>(options =>
    options.UseSqlite(ResolveSqliteConnectionString(
        builder.Configuration.GetConnectionString("StarbaseApiDatabase"),
        builder.Environment.ContentRootPath)));

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly);
});

// Business "today" (UTC) for date rules; replaceable in tests.
builder.Services.AddSingleton(TimeProvider.System);

// Per-request logging facts shared by the HTTP boundary, handlers and exception handling.
builder.Services.AddScoped<OperationContext>();

var app = builder.Build();

// Configure the HTTP request pipeline.
// Outermost: records one outcome per request after exception handling has produced the response.
app.UseMiddleware<RequestOutcomeLoggingMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// A relative SQLite path would resolve against the process working directory, so starting
// the API from another folder would silently create a second, empty database. Anchor it to
// the content root instead; absolute paths and in-memory databases are left unchanged.
static string ResolveSqliteConnectionString(string? connectionString, string contentRootPath)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Connection string 'StarbaseApiDatabase' is not configured.");
    }

    var csb = new SqliteConnectionStringBuilder(connectionString);
    if (csb.Mode != SqliteOpenMode.Memory
        && !string.IsNullOrEmpty(csb.DataSource)
        && csb.DataSource != ":memory:"
        && !Path.IsPathRooted(csb.DataSource))
    {
        csb.DataSource = Path.GetFullPath(Path.Combine(contentRootPath, csb.DataSource));
    }

    // Bound how long a command waits on another writer's lock (SQLITE_BUSY becomes a 503)
    // unless the connection string sets its own value; the provider default is 30 seconds.
    if (csb.DefaultTimeout == 30)
    {
        csb.DefaultTimeout = 5;
    }

    return csb.ToString();
}

// Exposes the implicit Program class to WebApplicationFactory in the test project.
public partial class Program { }
