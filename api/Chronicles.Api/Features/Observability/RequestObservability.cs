using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;

namespace Chronicles.Api;

public static partial class RequestObservability
{
    private static readonly Meter Meter = new("Chronicles.Http", "1.0.0");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("chronicles.http.duration", "ms");
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("chronicles.http.requests");

    public static void UseRequestObservability(this WebApplication app) => app.Use(async (context, next) =>
    {
        var supplied = context.Request.Headers["X-Correlation-ID"].ToString();
        var correlation = CorrelationPattern().IsMatch(supplied) ? supplied : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlation;
        Activity.Current?.SetTag("chronicles.correlation_id", correlation);
        context.Response.OnStarting(() =>
        {
            // Exception handling may clear response headers after this middleware has run.
            context.Response.Headers["X-Correlation-ID"] = correlation;
            return Task.CompletedTask;
        });
        using var scope = app.Logger.BeginScope(new Dictionary<string, object> { ["correlationId"] = correlation });
        var started = Stopwatch.GetTimestamp();
        try { await next(); }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            // Route templates are bounded and exclude query strings, invite/OBS tokens and identifiers.
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            var tags = new TagList { { "route", route }, { "method", context.Request.Method }, { "status", context.Response.StatusCode } };
            Duration.Record(elapsed, tags); Requests.Add(1, tags);
            app.Logger.LogInformation("HTTP {Method} {Route} returned {StatusCode} in {ElapsedMs} ms; correlation {CorrelationId}",
                context.Request.Method, route, context.Response.StatusCode, Math.Round(elapsed, 2), correlation);
        }
    });

    [GeneratedRegex("^[a-zA-Z0-9_-]{16,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex CorrelationPattern();
}
