using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<StargateContext>(options =>
    options.UseSqlite(ResolveSqliteConnectionString(
        builder.Configuration.GetConnectionString("StarbaseApiDatabase"),
        builder.Environment.ContentRootPath)));

builder.Services.AddMediatR(cfg =>
{
    cfg.AddRequestPreProcessor<CreateAstronautDutyPreProcessor>();
    cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
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

    return csb.ToString();
}

