using Microsoft.AspNetCore.Antiforgery;

namespace TutoringManager.Api.Infrastructure;

/// <summary>
/// Double-submit antiforgery for the JSON API: the SPA reads the XSRF-TOKEN cookie and sends it back
/// in the X-XSRF-TOKEN header on every request that changes data.
/// </summary>
public static class Antiforgery
{
    public const string HeaderName = "X-XSRF-TOKEN";
    public const string CookieName = "XSRF-TOKEN";

    public static void IssueToken(HttpContext context)
    {
        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append(CookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false, // the SPA must be able to read it
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            Path = "/",
        });
    }

    public static IApplicationBuilder UseApiAntiforgery(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (RequiresValidation(context.Request))
            {
                var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                if (!await antiforgery.IsRequestValidAsync(context))
                {
                    throw new ApiException(StatusCodes.Status400BadRequest, "Invalid or missing antiforgery token.");
                }
            }

            await next();
        });

    // The attendance endpoints are public and authorised by the token in the URL, not by a cookie,
    // so there is no session a forged request could ride on.
    private static bool RequiresValidation(HttpRequest request) =>
        !HttpMethods.IsGet(request.Method)
        && !HttpMethods.IsHead(request.Method)
        && !HttpMethods.IsOptions(request.Method)
        && request.Path.StartsWithSegments("/api")
        && !request.Path.StartsWithSegments("/api/attendance");
}
