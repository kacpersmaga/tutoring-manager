using Microsoft.AspNetCore.Identity;
using TutoringManager.Api.Infrastructure;

namespace TutoringManager.Api.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", Register).RequireRateLimiting(RateLimiting.Auth);
        group.MapPost("/login", Login).RequireRateLimiting(RateLimiting.Auth);
        group.MapPost("/logout", Logout);
        group.MapGet("/me", Me).RequireAuthorization();
        group.MapGet("/antiforgery", IssueAntiforgeryToken);
    }

    private static async Task<IResult> Register(
        RegisterRequest request, UserManager<Tutor> users, SignInManager<Tutor> signIn)
    {
        var tutor = new Tutor
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
        };

        var result = await users.CreateAsync(tutor, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(e => e.Code.Contains("Password") ? "password" : "email")
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
            return Results.ValidationProblem(errors);
        }

        await signIn.SignInAsync(tutor, isPersistent: true);
        return Results.Ok(ToMe(tutor));
    }

    private static async Task<IResult> Login(
        LoginRequest request, UserManager<Tutor> users, SignInManager<Tutor> signIn)
    {
        var tutor = await users.FindByEmailAsync(request.Email.Trim());
        if (tutor is null)
        {
            return InvalidCredentials();
        }

        var result = await signIn.PasswordSignInAsync(tutor, request.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return Results.Problem(
                title: "Account temporarily locked.",
                detail: "Too many failed sign-in attempts. Try again in 15 minutes.",
                statusCode: StatusCodes.Status423Locked);
        }

        return result.Succeeded ? Results.Ok(ToMe(tutor)) : InvalidCredentials();
    }

    private static async Task<IResult> Logout(SignInManager<Tutor> signIn)
    {
        await signIn.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Me(HttpContext context, UserManager<Tutor> users)
    {
        var tutor = await users.GetUserAsync(context.User);
        return tutor is null ? Results.Unauthorized() : Results.Ok(ToMe(tutor));
    }

    private static IResult IssueAntiforgeryToken(HttpContext context)
    {
        Antiforgery.IssueToken(context);
        return Results.NoContent();
    }

    private static IResult InvalidCredentials() =>
        Results.Problem(title: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);

    private static MeResponse ToMe(Tutor tutor) => new(tutor.Email!, tutor.DisplayName, tutor.TimeZoneId);
}
