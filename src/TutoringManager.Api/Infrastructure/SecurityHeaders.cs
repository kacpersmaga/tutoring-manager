namespace TutoringManager.Api.Infrastructure;

public static class SecurityHeaders
{
    // Styles allow 'unsafe-inline' because the calendar library injects its stylesheet at runtime;
    // scripts stay 'self' only. Fonts come from Fontshare and Google Fonts; the calendar's icon font is a data: URI.
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline' https://api.fontshare.com https://fonts.googleapis.com; " +
        "font-src 'self' data: https://cdn.fontshare.com https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            // The attendance link carries a token in its path: never send it on in a Referer header.
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            await next();
        });
}
