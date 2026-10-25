using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api.Services;

public sealed class SmtpEmailSender(IConfiguration configuration)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["SMTP_HOST"])
        && !string.IsNullOrWhiteSpace(configuration["SMTP_FROM"]);

    public async Task SendResetCodeAsync(string destination, string code)
    {
        if (!IsConfigured) throw new InvalidOperationException("SMTP não configurado.");
        using var message = new MailMessage(configuration["SMTP_FROM"]!, destination)
        {
            Subject = "Seu código de recuperação — Crônicas",
            Body = $"Seu código de recuperação é {code}. Ele expira em 15 minutos.\n\nSe você não solicitou a troca de senha, ignore esta mensagem."
        };
        using var client = new SmtpClient(configuration["SMTP_HOST"]!, configuration.GetValue("SMTP_PORT", 587))
        {
            EnableSsl = configuration.GetValue("SMTP_TLS", true),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 10000
        };
        var username = configuration["SMTP_USERNAME"];
        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, configuration["SMTP_PASSWORD"]);
        await client.SendMailAsync(message);
    }
}

public sealed class PasswordRecoveryService(
    AppDbContext db, IPasswordHasher<User> hasher, SmtpEmailSender emailSender, ILogger<PasswordRecoveryService> logger, RoomAccessService rooms)
{
    public bool IsConfigured => emailSender.IsConfigured;

    public async Task RequestAsync(string email)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
        if (user is null) return;
        var recent = await db.PasswordResetCodes.AnyAsync(x => x.UserId == user.Id && x.CreatedAt > DateTime.UtcNow.AddMinutes(-2));
        if (recent) return;
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(10));
        var reset = new PasswordResetCode
        {
            UserId = user.Id,
            CodeHash = Hash(code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };
        db.PasswordResetCodes.Add(reset);
        await db.SaveChangesAsync();
        try
        {
            await emailSender.SendResetCodeAsync(user.Email, code);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha no envio de recuperação de senha para usuário {UserId}", user.Id);
            db.PasswordResetCodes.Remove(reset);
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> CompleteAsync(string email, string code, string password)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
        if (user is null) return false;
        var hash = Hash(code.ToUpperInvariant());
        await using var transaction = await db.Database.BeginTransactionAsync();
        var changed = await db.PasswordResetCodes
            .Where(x => x.UserId == user.Id && x.CodeHash == hash && x.UsedAt == null && x.ExpiresAt > DateTime.UtcNow)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsedAt, DateTime.UtcNow));
        if (changed != 1) return false;
        user.PasswordHash = hasher.HashPassword(user, password);
        await db.UserSessions.Where(x => x.UserId == user.Id && x.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAt, DateTime.UtcNow));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        await rooms.RevokeUser(user.Id);
        return true;
    }

    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
