using System.Security.Cryptography;
using System.Text;
using Chronicles.Api;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

public static class IdentityRecoveryTests
{
    public static async Task Run(Func<AppDbContext> context)
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Password recovery: " + label); }
        static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        const string code = "0123456789ABCDEF0123", otherCode = "ABCDEF01234567890123";
        var hasher = new PasswordHasher<User>();
        var user = new User { Email = "quality-reset@example.test", DisplayName = "Recovery", PasswordHash = "" };
        user.PasswordHash = hasher.HashPassword(user, "previous-password-123");
        await using (var seed = context())
        {
            seed.Users.Add(user);
            seed.PasswordResetCodes.AddRange(
                new() { UserId = user.Id, CodeHash = Hash(code), ExpiresAt = DateTime.UtcNow.AddMinutes(15) },
                new() { UserId = user.Id, CodeHash = Hash(otherCode), ExpiresAt = DateTime.UtcNow.AddMinutes(15) });
            seed.UserSessions.Add(new() { UserId = user.Id, TokenHash = Hash("session"), ExpiresAt = DateTime.UtcNow.AddDays(1) });
            await seed.SaveChangesAsync();
        }
        var services = new ServiceCollection(); services.AddLogging(); services.AddSignalR();
        using var provider = services.BuildServiceProvider();
        var rooms = new RoomAccessService(new RoomRegistry(), provider.GetRequiredService<IHubContext<CampaignRoomHub>>());
        PasswordRecoveryService Recovery(AppDbContext db) => new(db, hasher, new SmtpEmailSender(new ConfigurationBuilder().Build()), NullLogger<PasswordRecoveryService>.Instance, rooms);
        await using (var db = context()) Check(await Recovery(db).CompleteAsync(user.Email, code.ToLowerInvariant(), "replacement-password-123"), "valid case-insensitive code succeeds");
        await using (var db = context())
        {
            Check(await db.PasswordResetCodes.Where(x => x.UserId == user.Id).AllAsync(x => x.UsedAt != null), "all outstanding codes consumed");
            Check(await db.UserSessions.Where(x => x.UserId == user.Id).AllAsync(x => x.RevokedAt != null), "old sessions revoked");
            var saved = await db.Users.FindAsync(user.Id);
            Check(hasher.VerifyHashedPassword(saved!, saved!.PasswordHash, "replacement-password-123") != PasswordVerificationResult.Failed, "password changed");
            Check(!await Recovery(db).CompleteAsync(user.Email, code, "replayed-password-123"), "used code cannot be replayed");
            Check(!await Recovery(db).CompleteAsync(user.Email, otherCode, "second-password-123"), "older outstanding code cannot change password again");
        }
        Console.WriteLine("Password recovery: password update, all-code invalidation, session revocation and replay rejection passed.");
    }
}
