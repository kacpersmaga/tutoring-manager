using Microsoft.EntityFrameworkCore;
using TutoringManager.Api.Infrastructure;
using TutoringManager.Api.Lessons;

namespace TutoringManager.Api.Attendance;

public record AttendancePageResponse(
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    string TutorName,
    AttendanceResponse Response);

public record AttendanceRespondRequest(AttendanceResponse Response);

/// <summary>
/// The public side of attendance confirmation. There is no signed-in user here, so query filters are
/// bypassed explicitly and the token alone decides which lesson is visible.
/// </summary>
public class AttendanceService(AppDbContext db, TimeProvider time)
{
    /// <summary>Creates a token that expires when the lesson starts. Returns the raw value for the email link.</summary>
    public async Task<string> CreateTokenAsync(Lesson lesson)
    {
        var (rawToken, hash) = AttendanceToken.Generate();
        db.AttendanceTokens.Add(new AttendanceToken
        {
            LessonId = lesson.Id,
            TokenHash = hash,
            ExpiresAtUtc = lesson.StartUtc,
            CreatedAt = time.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync();
        return rawToken;
    }

    /// <summary>Called when a lesson is moved or cancelled: links sent for the old time stop working.</summary>
    public async Task RevokeTokensAsync(Guid lessonId)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await db.AttendanceTokens
            .Where(t => t.LessonId == lessonId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, now));
    }

    public async Task<AttendancePageResponse> GetAsync(string rawToken)
    {
        var lesson = await FindLessonAsync(rawToken);
        var tutorName = await db.Users.Where(u => u.Id == lesson.TutorId).Select(u => u.DisplayName).SingleAsync();
        return new AttendancePageResponse(lesson.LocalDate, lesson.LocalStartTime, lesson.DurationMinutes, tutorName, lesson.Attendance);
    }

    /// <summary>The answer can be changed any number of times until the lesson starts.</summary>
    public async Task<AttendancePageResponse> RespondAsync(string rawToken, AttendanceRespondRequest request)
    {
        if (request.Response == AttendanceResponse.Unknown)
        {
            throw ApiException.Validation("response", "Choose Confirmed or Declined.");
        }

        var lesson = await FindLessonAsync(rawToken);
        lesson.Attendance = request.Response;
        lesson.AttendanceRespondedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        return await GetAsync(rawToken);
    }

    private async Task<Lesson> FindLessonAsync(string rawToken)
    {
        var hash = AttendanceToken.HashOf(rawToken) ?? throw UnknownLink();
        var token = await db.AttendanceTokens.SingleOrDefaultAsync(t => t.TokenHash == hash);
        if (token is null || token.RevokedAt is not null)
        {
            throw UnknownLink();
        }

        var lesson = await db.Lessons.IgnoreQueryFilters().SingleAsync(l => l.Id == token.LessonId);
        if (token.ExpiresAtUtc <= time.GetUtcNow().UtcDateTime || lesson.Status != LessonStatus.Scheduled)
        {
            throw new ApiException(StatusCodes.Status410Gone, "This link has expired.",
                "Answers can be given until the lesson starts.");
        }

        return lesson;
    }

    // Wrong, revoked and made-up tokens all get the same answer, so the response reveals nothing.
    private static ApiException UnknownLink() => ApiException.NotFound("Link");
}
