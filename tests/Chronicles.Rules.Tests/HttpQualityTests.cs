using System.Net;
using Chronicles.Api;
using Chronicles.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class HttpQualityTests
{
    public static async Task Run()
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("HTTP quality: " + label); }
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        await using var app = builder.Build(); app.UseHttpResponseProtection(["https://trusted.example.test"]);
        app.MapMethods("/api/probe", ["GET", "POST"], () => Results.Ok(new { ok = true }));
        app.MapGet("/api/denied", () => Results.Unauthorized());
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(10) };
        foreach (var path in new[] { "/api/probe", "/api/denied", "/api/missing" })
        {
            using var response = await client.GetAsync(path);
            Check(response.Headers.CacheControl?.NoStore == true, "successful and rejected API responses cannot be cached");
            Check(response.Headers.GetValues("X-Robots-Tag").Single().Contains("noindex"), "API never indexed");
            Check(response.Headers.GetValues("X-Content-Type-Options").Single() == "nosniff", "nosniff on every response");
            Check(response.Headers.GetValues("Content-Security-Policy").Single().Contains("frame-ancestors 'none'"), "no framing of API errors");
        }
        using var crossSite = new HttpRequestMessage(HttpMethod.Post, "/api/probe"); crossSite.Headers.Add("Sec-Fetch-Site", "cross-site");
        using var rejected = await client.SendAsync(crossSite); Check(rejected.StatusCode == HttpStatusCode.Forbidden, "cross-site write with no Origin is rejected");
        using var trusted = new HttpRequestMessage(HttpMethod.Post, "/api/probe"); trusted.Headers.Add("Sec-Fetch-Site", "cross-site"); trusted.Headers.Add("Origin", "https://trusted.example.test");
        using var trustedResponse = await client.SendAsync(trusted); Check(trustedResponse.IsSuccessStatusCode, "explicitly allowed CORS origins preserve existing access");
        using var accepted = await client.PostAsync("/api/probe", new StringContent("{}")); Check(accepted.IsSuccessStatusCode, "non-browser API clients keep working");
        await app.StopAsync();

        using var email = RecoveryEmail.Create("sender@example.test", "recipient@example.test", "<unsafe>");
        Check(email.AlternateViews.Count == 2, "recovery email has plain text and HTML");
        Check(email.AlternateViews[0].ContentType.MediaType == "text/plain" && email.AlternateViews[1].ContentType.MediaType == "text/html", "multipart types are explicit");
        using var reader = new StreamReader(email.AlternateViews[1].ContentStream);
        var html = await reader.ReadToEndAsync(); Check(html.Contains("&lt;unsafe&gt;") && !html.Contains("<unsafe>"), "HTML does not interpolate unsafe tokens");
        Console.WriteLine("HTTP quality: cache, indexing, security headers, cross-site writes and multipart email passed.");
    }
}
