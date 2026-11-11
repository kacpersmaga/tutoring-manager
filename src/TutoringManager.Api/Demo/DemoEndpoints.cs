using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TutoringManager.Api.Auth;

namespace TutoringManager.Api.Demo;

public static class DemoEndpoints
{
    public static void MapDemoEndpoints(this IEndpointRouteBuilder app)
    {
        // Demo mode runs only locally, so the sign-in page may fill in the demo credentials.
        app.MapGet("/api/demo", (IOptions<DemoOptions> options) => options.Value.Enabled
            ? Results.Ok(new { options.Value.Email, options.Value.Password })
            : Results.NotFound());

        // Only the demo account can reset itself, and only when demo mode is on; everyone else gets 404.
        app.MapPost("/api/demo/reset", async (HttpContext context, UserManager<Tutor> users, DemoSeeder seeder) =>
        {
            var tutor = await users.GetUserAsync(context.User);
            if (tutor is null || !seeder.IsDemoTutor(tutor))
            {
                return Results.NotFound();
            }

            await seeder.ResetAsync(tutor);
            return Results.NoContent();
        }).RequireAuthorization();
    }
}
