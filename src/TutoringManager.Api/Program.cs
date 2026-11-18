using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TutoringManager.Api.Attendance;
using TutoringManager.Api.Auth;
using TutoringManager.Api.Demo;
using TutoringManager.Api.Emails;
using TutoringManager.Api.Infrastructure;
using TutoringManager.Api.Lessons;
using TutoringManager.Api.Payments;
using TutoringManager.Api.Students;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CurrentTutor>();
builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Default"))
    .UseSnakeCaseNamingConvention());

builder.Services.AddDataProtection().PersistKeysToDbContext<AppDbContext>().SetApplicationName("TutoringManager");
builder.Services.AddAppAuth(builder.Configuration);
builder.Services.AddAppRateLimiting(builder.Configuration);

builder.Services.AddScoped<StudentService>();
builder.Services.AddScoped<LessonService>();
builder.Services.AddScoped<SeriesService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<EmailScheduler>();
builder.Services.AddScoped<EmailDispatcher>();
builder.Services.AddSingleton<MBankParser>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<ReportService>();
builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection("Demo"));
builder.Services.AddScoped<DemoSeeder>();

if (builder.Configuration.GetValue("Workers:Enabled", true))
{
    builder.Services.AddHostedService<SeriesExtensionWorker>();
    builder.Services.AddHostedService<EmailWorker>();
}

var app = builder.Build();

if (app.Configuration.GetValue("Security:HttpsOnly", false))
{
    // Production behind TLS: redirect plain HTTP and tell browsers to stay on HTTPS.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseApiAntiforgery();

app.MapGet("/health", async (AppDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok(new { status = "Healthy" })
        : Results.Problem("Database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable));

app.MapAuthEndpoints();
app.MapStudentEndpoints();
app.MapLessonEndpoints();
app.MapAttendanceEndpoints();
app.MapPaymentEndpoints();
app.MapDemoEndpoints();

// The React app is served from wwwroot; unknown /api paths stay 404 instead of returning the SPA.
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

await app.MigrateDatabaseAsync();
await app.SeedDemoAsync();
app.Run();

public partial class Program;
