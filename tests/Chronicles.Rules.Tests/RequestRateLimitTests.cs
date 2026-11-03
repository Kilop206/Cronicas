using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Chronicles.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class RequestRateLimitTests
{
    private const string Key = "local-test-proxy-key-do-not-use-in-production";
    public static async Task RunAsync()
    {
        void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException("Rate limits: " + label); }
        var identity = new ClientRequestIdentity(Key);
        var context = new DefaultHttpContext(); context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.1"; context.Request.Headers["CF-Connecting-IP"] = "203.0.113.2";
        Check(identity.ClientIp(context) == "10.0.0.5", "untrusted forwarded headers cannot choose an identity");
        SetSignature(context,"203.0.113.10",1000);
        Check(identity.ClientIp(context,1000) == "203.0.113.10", "signed edge identity is recognized");
        Check(identity.ClientIp(context,1091) == "10.0.0.5", "old assertions expire");
        Check(identity.ClientIp(context,909) == "10.0.0.5", "future assertions are rejected");
        context.Request.Headers["X-Cronicas-Client-IP"] = "203.0.113.20";
        Check(identity.ClientIp(context,1000) == "10.0.0.5", "tampered IP cannot reuse signature");
        SetSignature(context,"::ffff:203.0.113.10",1000);
        Check(identity.ClientIp(context,1000) == "203.0.113.10", "IPv4 mapped addresses normalize to one partition");
        Check(new ClientRequestIdentity(null).ClientIp(context,1000) == "10.0.0.5", "unconfigured deployments never trust edge headers blindly");
        context.Request.Headers["X-Cronicas-Proxy-Signature"] = new string('z',64);
        Check(identity.ClientIp(context,1000) == "10.0.0.5", "invalid hex is rejected safely");
        var userA = Guid.NewGuid(); var userB = Guid.NewGuid();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> { ["PROXY_IDENTITY_KEY"] = Key });
        builder.Services.AddRequestRateLimits(builder.Configuration);
        await using var app = builder.Build();
        app.Use(async (http,next) => { if (Guid.TryParse(http.Request.Headers["Test-Authenticated-User"],out var id)) http.User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,id.ToString())],"test")); await next(); });
        app.UseRateLimiter();
        app.MapPost("/login", () => Results.Ok()).RequireRateLimiting("auth");
        app.MapPost("/register", () => Results.Ok()).RequireRateLimiting("auth");
        app.MapPost("/invites", () => Results.Ok()).RequireRateLimiting("account");
        app.MapPost("/rolls", () => Results.Ok()).RequireRateLimiting("rolls");
        await app.StartAsync();
        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new(url) };
        async Task<HttpResponseMessage> Send(string route, string ip, Guid? user = null, bool forged = false)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,route);
            var stamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
            request.Headers.Add("X-Cronicas-Client-IP",ip); request.Headers.Add("X-Cronicas-Proxy-Time",stamp); request.Headers.Add("X-Cronicas-Proxy-Signature",forged ? new string('0',64) : Sign(ip,stamp));
            if (user is { } id) request.Headers.Add("Test-Authenticated-User",id.ToString());
            return await client.SendAsync(request);
        }
        for (int i=0;i<20;i++) { using var ok=await Send("/login","203.0.113.10"); Check(ok.IsSuccessStatusCode,"login budget before exhaustion"); }
        using var limited = await Send("/login","203.0.113.10");
        Check(limited.StatusCode == HttpStatusCode.TooManyRequests && limited.Headers.RetryAfter?.Delta?.TotalSeconds > 0,"true excess returns 429 and Retry-After");
        Check((await limited.Content.ReadAsStringAsync()).Contains("retryAfterSeconds",StringComparison.Ordinal),"rejection supplies a useful wait time");
        using var alternate=await Send("/LOGIN/","203.0.113.10"); Check(alternate.StatusCode == HttpStatusCode.TooManyRequests,"route casing and trailing slash cannot reset the login budget");
        using var fresh=await Send("/login","203.0.113.20"); Check(fresh.IsSuccessStatusCode,"first attempt by second visitor succeeds on same proxy connection");
        using var register=await Send("/register","203.0.113.10"); Check(register.IsSuccessStatusCode,"login exhaustion does not block registration");
        using var invite=await Send("/invites","203.0.113.10",userA); Check(invite.IsSuccessStatusCode,"login exhaustion does not block authenticated invites");
        for (int i=0;i<120;i++) { using var ok=await Send("/rolls","203.0.113.10",userA); Check(ok.IsSuccessStatusCode,"activity budget before exhaustion"); }
        using var exhausted=await Send("/rolls","203.0.113.20",userA); Check(exhausted.StatusCode == HttpStatusCode.TooManyRequests,"same account cannot bypass activity limit by changing network");
        using var other=await Send("/rolls","203.0.113.10",userB); Check(other.IsSuccessStatusCode,"another account behind the same IP keeps its first request");
        for (int i=0;i<20;i++) { using var ok=await Send("/login",$"198.51.100.{i+1}",forged:true); Check(ok.IsSuccessStatusCode,"invalid proxy signatures use socket partition"); }
        using var spoofed=await Send("/login","198.51.100.99",forged:true); Check(spoofed.StatusCode == HttpStatusCode.TooManyRequests,"forged headers cannot rotate anonymous identity");
        await app.StopAsync();
        Console.WriteLine("Rate limits: signed proxy identities, spoof rejection, endpoint isolation, per-account activity and Retry-After passed.");
    }
    private static string Sign(string ip,string timestamp) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key),Encoding.UTF8.GetBytes($"v1\n{timestamp}\n{ip}")));
    private static void SetSignature(HttpContext context,string ip,long timestamp)
    {
        var text=timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture); context.Request.Headers["X-Cronicas-Client-IP"]=ip; context.Request.Headers["X-Cronicas-Proxy-Time"]=text; context.Request.Headers["X-Cronicas-Proxy-Signature"]=Sign(ip,text);
    }
}
