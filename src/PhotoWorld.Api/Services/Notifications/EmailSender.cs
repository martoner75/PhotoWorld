using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace PhotoWorld.Api.Services.Notifications;

public sealed class EmailOptions
{
    public const string Section = "Email";

    /// <summary>Sender, e.g. "PhotoWorld &lt;notifications@your-domain&gt;". The domain must be verified with your provider.</summary>
    public string From { get; set; } = "";

    /// <summary>The website's public address, for links in emails (confirm, unsubscribe, posts).</summary>
    public string WebBaseUrl { get; set; } = "";

    public SmtpOptions Smtp { get; set; } = new();

    /// <summary>When no SMTP host is set (development), emails are written here as .eml files instead.</summary>
    public string PickupDirectory { get; set; } = "emails";

    /// <summary>How often the sender looks for new posts.</summary>
    public int PollMinutes { get; set; } = 5;

    public bool UsesSmtp => !string.IsNullOrWhiteSpace(Smtp.Host);
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyDictionary<string, string>? Headers = null);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

/// <summary>Sends through any SMTP provider (STARTTLS on 587, or TLS on 465).</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var o = options.Value;
        // MailKit requires a completed online revocation check by default, which fails for certificates whose CA no
        // longer runs OCSP (e.g. Let's Encrypt, used by Resend): "incomplete certificate revocation check". Chain,
        // expiry and host name are still validated; HttpClient doesn't check revocation by default either.
        using var client = new SmtpClient { CheckCertificateRevocation = false };
        await client.ConnectAsync(o.Smtp.Host, o.Smtp.Port,
            o.Smtp.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
        if (!string.IsNullOrEmpty(o.Smtp.Username)) await client.AuthenticateAsync(o.Smtp.Username, o.Smtp.Password, ct);
        await client.SendAsync(MimeMessages.Build(o.From, message), ct);
        await client.DisconnectAsync(true, ct);
    }
}

/// <summary>Development stand-in: writes each email to a .eml file (open it with Mail or any email app).</summary>
public sealed class PickupDirectoryEmailSender(IOptions<EmailOptions> options, IHostEnvironment env, ILogger<PickupDirectoryEmailSender> logger)
    : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var o = options.Value;
        var dir = Path.Combine(env.ContentRootPath, o.PickupDirectory);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml");
        await MimeMessages.Build(string.IsNullOrWhiteSpace(o.From) ? "PhotoCommunity <notifications@localhost>" : o.From, message)
            .WriteToAsync(path, ct);
        logger.LogInformation("Email \"{Subject}\" to {To} written to {Path} (no SMTP host configured)", message.Subject, message.To, path);
    }
}

internal static class MimeMessages
{
    public static MimeMessage Build(string from, EmailMessage message)
    {
        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(from));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        foreach (var (name, value) in message.Headers ?? new Dictionary<string, string>()) mime.Headers.Add(name, value);
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();
        return mime;
    }
}
