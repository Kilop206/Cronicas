using System.Net;
using System.Net.Mail;
using System.Net.Mime;
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
        using var message = RecoveryEmail.Create(configuration["SMTP_FROM"]!, destination, code);
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

public static class RecoveryEmail
{
    public static MailMessage Create(string sender, string destination, string code)
    {
        var message = new MailMessage(sender, destination) { Subject = "Seu código de recuperação — Crônicas", SubjectEncoding = Encoding.UTF8 };
        var text = $"Crônicas\n\nSeu código de recuperação é {code}. Ele expira em 15 minutos e pode ser usado uma vez.\n\nVolte à tela de recuperação do Crônicas e informe o código. Nunca compartilhe este código. Se você não solicitou a troca de senha, ignore esta mensagem.";
        var html = $"""
            <!doctype html><html lang="pt-BR"><head><meta charset="utf-8"></head>
            <body style="margin:0;background:#101419;color:#eeeae0;font-family:Arial,sans-serif">
            <table role="presentation" style="width:100%;border-collapse:collapse"><tr><td style="padding:32px 20px">
            <h1 style="color:#d5bd91;font-family:Georgia,serif">Crônicas</h1><h2>Recuperação de acesso</h2>
            <p>Seu código de recuperação é:</p><p style="font-size:24px;letter-spacing:2px;color:#d5bd91;overflow-wrap:anywhere">{WebUtility.HtmlEncode(code)}</p>
            <p>Ele expira em 15 minutos e pode ser usado uma vez.</p><p>Volte à tela de recuperação do Crônicas e informe o código. Nunca compartilhe este código.</p>
            <p>Se você não solicitou a troca de senha, ignore esta mensagem.</p></td></tr></table></body></html>
            """;
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(text, Encoding.UTF8, MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, MediaTypeNames.Text.Html));
        return message;
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
        // A successful reset also invalidates any other outstanding code for this account.
        await db.PasswordResetCodes.Where(x => x.UserId == user.Id && x.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsedAt, DateTime.UtcNow));
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
