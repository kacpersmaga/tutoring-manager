using TutoringManager.Api.Infrastructure;

namespace TutoringManager.Api.Attendance;

public static class AttendanceEndpoints
{
    /// <summary>Public endpoints behind the link in the email: no sign-in, rate limited per IP.</summary>
    public static void MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attendance").AllowAnonymous().RequireRateLimiting(RateLimiting.Attendance);

        group.MapGet("/{token}", (string token, AttendanceService attendance) => attendance.GetAsync(token));
        group.MapPost("/{token}", (string token, AttendanceRespondRequest request, AttendanceService attendance) =>
            attendance.RespondAsync(token, request));
    }
}
