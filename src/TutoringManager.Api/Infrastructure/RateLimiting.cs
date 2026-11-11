using System.Threading.RateLimiting;

namespace TutoringManager.Api.Infrastructure;

public static class RateLimiting
{
    public const string Auth = "auth";
    public const string Attendance = "attendance";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var authPerMinute = configuration.GetValue("RateLimits:AuthPerMinute", 10);
        var attendancePerMinute = configuration.GetValue("RateLimits:AttendancePerMinute", 30);

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Auth, context => PerClientIp(context, authPerMinute));
            options.AddPolicy(Attendance, context => PerClientIp(context, attendancePerMinute));
        });
    }

    private static RateLimitPartition<string> PerClientIp(HttpContext context, int permitsPerMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
}
