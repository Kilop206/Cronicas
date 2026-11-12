namespace Chronicles.Api;

public static class HttpResponseProtection
{
    public static void UseHttpResponseProtection(this WebApplication app, IReadOnlyCollection<string>? allowedOrigins = null) => app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
            context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
            if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        // Complements Origin validation and SameSite: also reject cross-site writes with no Origin.
        if (context.Request.Headers["Sec-Fetch-Site"] == "cross-site" &&
            context.Request.Method is not ("GET" or "HEAD" or "OPTIONS") &&
            !(allowedOrigins?.Contains(context.Request.Headers.Origin.ToString(), StringComparer.OrdinalIgnoreCase) ?? false))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        await next();
    });
}
