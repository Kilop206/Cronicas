using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Chronicles.Api;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:Default.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<PasswordRecoveryService>();
builder.Services.AddSingleton<SmtpEmailSender>();
builder.Services.AddSignalR(options => options.MaximumReceiveMessageSize = 128 * 1024);
builder.Services.AddSingleton<RoomRegistry>();
builder.Services.AddSingleton<RoomAccessService>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
    options.AddPolicy("rolls", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var allowedOrigins = builder.Configuration["ALLOWED_ORIGINS"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = 500;
    await Results.Problem("Ocorreu um erro inesperado.").ExecuteAsync(context);
}));
app.UseCors();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/hubs/campaign") &&
        context.Request.Headers.Origin is { Count: > 0 } hubOrigins &&
        !allowedOrigins.Contains(hubOrigins.ToString(), StringComparer.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    if (context.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin) && !allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)
            && !string.Equals(origin, $"{context.Request.Scheme}://{context.Request.Host}", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        if (context.Request.HasFormContentType)
        {
            context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }
    }
    await context.RequestServices.GetRequiredService<SessionService>().AuthenticateAsync(context);
    await next();
});

app.MapHealthChecks("/health");
app.MapHub<CampaignRoomHub>("/api/hubs/campaign");
var api = app.MapGroup("/api");
api.MapCharacterEndpoints();
api.MapCharacterInventoryEndpoints();
api.MapCharacterAbilityEndpoints();
api.MapInviteEndpoints();
api.MapNarrativeEndpoints();
api.MapDiceEndpoints();
api.MapSystemEndpoints();
api.MapCampaignMembersEndpoints();
api.MapWorldEndpoints();
api.MapChatEndpoints();
api.MapEncounterEndpoints();
api.MapSearchEndpoints();
api.MapProfileExportEndpoints();
var auth = api.MapGroup("/auth");

auth.MapPost("/register", async Task<IResult> (RegisterRequest input, AppDbContext db, IPasswordHasher<User> hasher, SessionService sessions, HttpContext http) =>
{
    var email = input.Email?.Trim().ToLowerInvariant() ?? "";
    var name = input.DisplayName?.Trim() ?? "";
    if (!new EmailAddressAttribute().IsValid(email) || email.Length > 320 || name.Length is < 2 or > 80 || input.Password is null || input.Password.Length is < 12 or > 128)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["account"] = ["Informe nome, email válido e senha de 12 a 128 caracteres."] });
    if (await db.Users.AnyAsync(x => x.Email == email))
        return Results.Conflict(new { message = "Este email já está em uso." });
    var user = new User { Email = email, DisplayName = name, PasswordHash = "" };
    user.PasswordHash = hasher.HashPassword(user, input.Password);
    db.Users.Add(user);
    await db.SaveChangesAsync();
    await sessions.StartAsync(http, user);
    return Results.Created("/api/auth/me", ToUser(user));
}).RequireRateLimiting("auth");

auth.MapPost("/login", async Task<IResult> (LoginRequest input, AppDbContext db, IPasswordHasher<User> hasher, SessionService sessions, HttpContext http) =>
{
    var email = input.Email?.Trim().ToLowerInvariant() ?? "";
    var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
    if (user is null || input.Password is null || hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
        return Results.Problem("Email ou senha incorretos.", statusCode: 401);
    await sessions.StartAsync(http, user);
    return Results.Ok(ToUser(user));
}).RequireRateLimiting("auth");

auth.MapGet("/me", async Task<IResult> (HttpContext http, AppDbContext db) =>
{
    if (CurrentUserId(http) is not { } id) return Results.Unauthorized();
    var user = await db.Users.FindAsync(id);
    return user is null ? Results.Unauthorized() : Results.Ok(ToUser(user));
});

auth.MapGet("/sessions", async Task<IResult> (HttpContext http, AppDbContext db) =>
{
    if (CurrentUserId(http) is not { } userId) return Results.Unauthorized();
    var currentId = http.Items["sessionId"] is Guid sessionId ? sessionId : Guid.Empty;
    var sessions = await db.UserSessions.AsNoTracking()
        .Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow)
        .OrderByDescending(x => x.CreatedAt).Take(50)
        .Select(x => new { x.Id, x.CreatedAt, x.ExpiresAt, current = x.Id == currentId }).ToListAsync();
    return Results.Ok(sessions);
});

auth.MapDelete("/sessions/{sessionId:guid}", async Task<IResult> (Guid sessionId, HttpContext http, AppDbContext db, RoomAccessService rooms) =>
{
    if (CurrentUserId(http) is not { } userId) return Results.Unauthorized();
    if (http.Items["sessionId"] is Guid currentId && currentId == sessionId)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["session"] = ["Use Sair da conta para encerrar a sessão atual."] });
    var changed = await db.UserSessions.Where(x => x.Id == sessionId && x.UserId == userId && x.RevokedAt == null)
        .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAt, DateTime.UtcNow));
    if (changed == 0) return Results.NotFound();
    await rooms.RevokeSession(sessionId);
    return Results.NoContent();
}).RequireRateLimiting("auth");

auth.MapPost("/logout", async Task<IResult> (HttpContext http, SessionService sessions) =>
{
    await sessions.RevokeAsync(http);
    return Results.NoContent();
});

auth.MapPost("/password-reset/request", async Task<IResult> (ResetRequest input, PasswordRecoveryService recovery) =>
{
    if (!recovery.IsConfigured)
        return Results.Problem("A recuperação por email ainda não está configurada.", statusCode: 503);
    var email = input.Email?.Trim().ToLowerInvariant() ?? "";
    if (!new EmailAddressAttribute().IsValid(email) || email.Length > 320)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Informe um email válido."] });
    await recovery.RequestAsync(email);
    return Results.Accepted();
}).RequireRateLimiting("auth");

auth.MapPost("/password-reset/complete", async Task<IResult> (ResetCompleteRequest input, PasswordRecoveryService recovery) =>
{
    var email = input.Email?.Trim().ToLowerInvariant() ?? "";
    var code = input.Code?.Trim() ?? "";
    if (!new EmailAddressAttribute().IsValid(email) || email.Length > 320 || !System.Text.RegularExpressions.Regex.IsMatch(code, "^[0-9a-fA-F]{20}$") || input.Password is null || input.Password.Length is < 12 or > 128)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["reset"] = ["Verifique email, código e senha de 12 a 128 caracteres."] });
    return await recovery.CompleteAsync(email, code, input.Password)
        ? Results.NoContent()
        : Results.ValidationProblem(new Dictionary<string, string[]> { ["reset"] = ["Código inválido ou expirado."] });
}).RequireRateLimiting("auth");

api.MapPatch("/profile", async Task<IResult> (ProfileRequest input, HttpContext http, AppDbContext db) =>
{
    if (CurrentUserId(http) is not { } id) return Results.Unauthorized();
    var name = input.DisplayName?.Trim() ?? "";
    if (name.Length is < 2 or > 80 || input.Bio?.Length > 1000 ||
        input.AvatarUrl is { Length: > 0 } avatar && (avatar.Length > 2048 || !Uri.TryCreate(avatar, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["profile"] = ["Nome ou biografia fora do limite."] });
    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.Unauthorized();
    user.DisplayName = name;
    user.Bio = input.Bio?.Trim();
    user.AvatarUrl = string.IsNullOrWhiteSpace(input.AvatarUrl) ? null : input.AvatarUrl.Trim();
    await db.SaveChangesAsync();
    return Results.Ok(ToUser(user));
});

api.MapPatch("/profile/preferences", async Task<IResult> (PreferencesRequest input, HttpContext http, AppDbContext db) =>
{
    if (CurrentUserId(http) is not { } id) return Results.Unauthorized();
    if (input.Accent is not ("gold" or "violet" or "wine" or "azure"))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["accent"] = ["Cor de destaque inválida."] });
    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.Unauthorized();
    user.Accent = input.Accent;
    await db.SaveChangesAsync();
    return Results.Ok(ToUser(user));
});

api.MapGet("/dashboard", async Task<IResult> (HttpContext http, AppDbContext db) =>
{
    if (CurrentUserId(http) is not { } id) return Results.Unauthorized();
    var campaigns = await db.CampaignMemberships.AsNoTracking().Where(x => x.UserId == id)
        .OrderByDescending(x => x.Campaign.UpdatedAt).Take(6)
        .Select(x => new CampaignView(x.CampaignId, x.Campaign.Name, x.Campaign.Description, x.Campaign.Status,
            x.Campaign.AccentColor, x.Role, x.Campaign.UpdatedAt, x.Campaign.SystemId, x.Campaign.WorldId)).ToListAsync();
    var total = await db.CampaignMemberships.CountAsync(x => x.UserId == id);
    var favorites = await db.CharacterFavorites.AsNoTracking().Where(x => x.UserId == id && x.Character.Campaign.Memberships.Any(m => m.UserId == id))
        .OrderByDescending(x => x.CreatedAt).Take(8)
        .Select(x => new { x.CharacterId, x.Character.Name, x.Character.PortraitUrl, x.Character.CampaignId,
            campaignName = x.Character.Campaign.Name, x.Character.Health, x.Character.MaxHealth }).ToListAsync();
    var upcoming = await db.NarrativeEntries.AsNoTracking()
        .Where(x => x.Kind == "sessions" && x.EventAt >= DateTime.UtcNow && x.Campaign.Memberships.Any(m => m.UserId == id) &&
            (x.Visibility == "campaign" || x.Visibility == "private" && x.AuthorId == id ||
             x.Campaign.Memberships.Any(m => m.UserId == id && (m.Role == "owner" || m.Role == "gm" || m.Role == "co-gm"))))
        .OrderBy(x => x.EventAt).Take(8)
        .Select(x => new { x.Id, x.CampaignId, x.Title, x.EventAt, campaignName = x.Campaign.Name }).ToListAsync();
    return Results.Ok(new { totalCampaigns = total, activeCampaigns = await db.CampaignMemberships.CountAsync(x => x.UserId == id && x.Campaign.Status == "active"), recentCampaigns = campaigns, favoriteCharacters = favorites, upcomingSessions = upcoming });
});

api.MapGet("/campaigns", async Task<IResult> (HttpContext http, AppDbContext db, int? page, string? search) =>
{
    if (CurrentUserId(http) is not { } id) return Results.Unauthorized();
    var currentPage = Math.Clamp(page ?? 1, 1, 100000);
    var query = db.CampaignMemberships.AsNoTracking().Where(x => x.UserId == id);
    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim();
        query = query.Where(x => EF.Functions.ILike(x.Campaign.Name, $"%{term}%"));
    }
    var total = await query.CountAsync();
    var items = await query.OrderByDescending(x => x.Campaign.UpdatedAt).Skip((currentPage - 1) * 20).Take(20)
        .Select(x => new CampaignView(x.CampaignId, x.Campaign.Name, x.Campaign.Description, x.Campaign.Status,
            x.Campaign.AccentColor, x.Role, x.Campaign.UpdatedAt, x.Campaign.SystemId, x.Campaign.WorldId)).ToListAsync();
    return Results.Ok(new { items, total, page = currentPage });
});

api.MapPost("/campaigns", async Task<IResult> (CampaignRequest input, HttpContext http, AppDbContext db) =>
{
    if (CurrentUserId(http) is not { } id) return Results.Unauthorized();
    var name = input.Name?.Trim() ?? "";
    if (name.Length is < 3 or > 120 || input.Description is null || input.Description.Length > 2000 || input.AccentColor is null || !System.Text.RegularExpressions.Regex.IsMatch(input.AccentColor, "^#[0-9a-fA-F]{6}$"))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["campaign"] = ["Verifique nome, descrição e cor."] });
    var campaign = new Campaign { Name = name, Description = input.Description.Trim(), AccentColor = input.AccentColor, OwnerId = id, SystemId = SystemCatalog.GenericId };
    campaign.Memberships.Add(new CampaignMembership { UserId = id, Role = "owner" });
    db.Campaigns.Add(campaign);
    await db.SaveChangesAsync();
    return Results.Created($"/api/campaigns/{campaign.Id}", new CampaignView(campaign.Id, campaign.Name, campaign.Description, campaign.Status, campaign.AccentColor, "owner", campaign.UpdatedAt, campaign.SystemId, campaign.WorldId));
});

api.MapGet("/campaigns/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
{
    if (CurrentUserId(http) is not { } userId) return Results.Unauthorized();
    var member = await db.CampaignMemberships.AsNoTracking().Where(x => x.UserId == userId && x.CampaignId == id)
        .Select(x => new CampaignView(x.CampaignId, x.Campaign.Name, x.Campaign.Description, x.Campaign.Status,
            x.Campaign.AccentColor, x.Role, x.Campaign.UpdatedAt, x.Campaign.SystemId, x.Campaign.WorldId)).SingleOrDefaultAsync();
    return member is null ? Results.NotFound() : Results.Ok(member);
});

api.MapPatch("/campaigns/{id:guid}", async Task<IResult> (Guid id, CampaignRequest input, HttpContext http, AppDbContext db) =>
{
    if (CurrentUserId(http) is not { } userId) return Results.Unauthorized();
    var member = await db.CampaignMemberships.Include(x => x.Campaign).SingleOrDefaultAsync(x => x.UserId == userId && x.CampaignId == id);
    if (member is null) return Results.NotFound();
    if (member.Role != "owner") return Results.Problem("Apenas o proprietário pode alterar a campanha.", statusCode: 403);
    var name = input.Name?.Trim() ?? "";
    if (name.Length is < 3 or > 120 || input.Description is null || input.Description.Length > 2000 || input.AccentColor is null || !System.Text.RegularExpressions.Regex.IsMatch(input.AccentColor, "^#[0-9a-fA-F]{6}$"))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["campaign"] = ["Verifique nome, descrição e cor."] });
    member.Campaign.Name = name;
    member.Campaign.Description = input.Description.Trim();
    member.Campaign.AccentColor = input.AccentColor;
    member.Campaign.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(new CampaignView(id, member.Campaign.Name, member.Campaign.Description, member.Campaign.Status, member.Campaign.AccentColor, member.Role, member.Campaign.UpdatedAt, member.Campaign.SystemId, member.Campaign.WorldId));
});

api.MapPatch("/campaigns/{id:guid}/status", async Task<IResult> (Guid id, StatusRequest input, HttpContext http, AppDbContext db) =>
{
    if (CurrentUserId(http) is not { } userId) return Results.Unauthorized();
    var member = await db.CampaignMemberships.Include(x => x.Campaign).SingleOrDefaultAsync(x => x.UserId == userId && x.CampaignId == id);
    if (member is null) return Results.NotFound();
    if (member.Role != "owner") return Results.Problem("Apenas o proprietário pode alterar a campanha.", statusCode: 403);
    if (input.Status is not ("planning" or "active" or "paused" or "completed" or "archived"))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Status inválido."] });
    member.Campaign.Status = input.Status;
    member.Campaign.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.NoContent();
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SystemCatalog.SeedAsync(db);
}
app.Run();

static Guid? CurrentUserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
static object ToUser(User user) => new { user.Id, user.Email, user.DisplayName, user.Bio, user.AvatarUrl, user.Accent, user.CreatedAt };

record RegisterRequest(string Email, string DisplayName, string Password);
record LoginRequest(string Email, string Password);
record ResetRequest(string Email);
record ResetCompleteRequest(string Email, string Code, string Password);
record ProfileRequest(string DisplayName, string? Bio, string? AvatarUrl);
record PreferencesRequest(string Accent);
record CampaignRequest(string Name, string Description, string AccentColor);
record StatusRequest(string Status);
record CampaignView(Guid Id, string Name, string Description, string Status, string AccentColor, string Role, DateTime UpdatedAt, Guid? SystemId, Guid? WorldId);
