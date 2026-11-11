using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TutoringManager.Api.Demo;
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
        group.MapPost("/delete-account", DeleteAccount).RequireAuthorization().RequireRateLimiting(RateLimiting.Auth);
    }

    private static async Task<IResult> Register(
        RegisterRequest request, UserManager<Tutor> users, SignInManager<Tutor> signIn, DemoSeeder demo)
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
        return Results.Ok(ToMe(tutor, demo));
    }

    private static async Task<IResult> Login(
        LoginRequest request, UserManager<Tutor> users, SignInManager<Tutor> signIn, DemoSeeder demo)
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

        return result.Succeeded ? Results.Ok(ToMe(tutor, demo)) : InvalidCredentials();
    }

    private static async Task<IResult> Logout(SignInManager<Tutor> signIn)
    {
        await signIn.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Me(HttpContext context, UserManager<Tutor> users, DemoSeeder demo)
    {
        var tutor = await users.GetUserAsync(context.User);
        return tutor is null ? Results.Unauthorized() : Results.Ok(ToMe(tutor, demo));
    }

    /// <summary>
    /// GDPR erasure of the whole account: students (with their lessons, notes, series, tokens, emails and accounts
    /// by cascade), imported statements and the sign-in itself. Asks for the password again.
    /// </summary>
    private static async Task<IResult> DeleteAccount(
        DeleteAccountRequest request, HttpContext context, UserManager<Tutor> users, SignInManager<Tutor> signIn, AppDbContext db)
    {
        var tutor = await users.GetUserAsync(context.User);
        if (tutor is null)
        {
            return Results.Unauthorized();
        }

        if (!await users.CheckPasswordAsync(tutor, request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = ["The password is not correct."] });
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.BankTransactions.Where(t => t.TutorId == tutor.Id).ExecuteDeleteAsync();
        await db.BankImports.Where(i => i.TutorId == tutor.Id).ExecuteDeleteAsync();
        await db.Students.Where(s => s.TutorId == tutor.Id).ExecuteDeleteAsync();
        await users.DeleteAsync(tutor);
        await transaction.CommitAsync();

        await signIn.SignOutAsync();
        return Results.NoContent();
    }

    private static IResult IssueAntiforgeryToken(HttpContext context)
    {
        Antiforgery.IssueToken(context);
        return Results.NoContent();
    }

    private static IResult InvalidCredentials() =>
        Results.Problem(title: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);

    private static MeResponse ToMe(Tutor tutor, DemoSeeder demo) =>
        new(tutor.Email!, tutor.DisplayName, tutor.TimeZoneId, demo.IsDemoTutor(tutor));
}
