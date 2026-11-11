using Microsoft.EntityFrameworkCore;
using TutoringManager.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CurrentTutor>();
builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Default"))
    .UseSnakeCaseNamingConvention());

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapGet("/health", async (AppDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok(new { status = "Healthy" })
        : Results.Problem("Database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable));

await app.MigrateDatabaseAsync();
app.Run();

public partial class Program;
