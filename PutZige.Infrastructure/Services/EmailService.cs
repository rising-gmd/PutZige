using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using PutZige.Application.Interfaces;
using PutZige.Infrastructure.Settings;

namespace PutZige.Infrastructure.Services;

public sealed class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;
    private readonly string _templatePath;

    public EmailService(IOptions<EmailSettings> options, ILogger<EmailService> logger)
    {
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "VerificationEmail.html");
    }

    public async Task SendVerificationEmailAsync(string toEmail, string username, string verificationToken, CancellationToken ct = default)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(_settings.FromName ?? "", _settings.FromEmail));

        // Validate recipient email early to provide consistent exceptions and avoid attempting SMTP operations
        if (toEmail is null)
        {
            throw new ArgumentNullException(nameof(toEmail));
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            throw new ArgumentException(PutZige.Application.Common.Messages.ErrorMessages.Validation.TokenRequired, nameof(toEmail));
        }

        // Basic email format sanity check to fail fast for clearly invalid values
        if (!toEmail.Contains('@'))
        {
            throw new FormatException(PutZige.Application.Common.Messages.ErrorMessages.Validation.EmailInvalidFormat);
        }

        // Rely on MailboxAddress.Parse to validate full format and throw FormatException for invalid inputs
        var mailbox = MailboxAddress.Parse(toEmail);
        message.To.Add(mailbox);
        message.Subject = "Please verify your email";

        var bodyHtml = await BuildVerificationHtmlAsync(username, verificationToken, toEmail, ct).ConfigureAwait(false);
        var builder = new BodyBuilder {HtmlBody = bodyHtml};
        message.Body = builder.ToMessageBody();

        await SendAsync(message, ct).ConfigureAwait(false);
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string username, string resetToken, CancellationToken ct = default)
    {
        // Placeholder for future implementation
        throw new NotImplementedException();
    }

    private async Task<string> BuildVerificationHtmlAsync(string username, string verificationToken, string toEmail, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        string template;
        if (File.Exists(_templatePath))
        {
            template = await File.ReadAllTextAsync(_templatePath, ct).ConfigureAwait(false);
        }
        else
        {
            _logger.LogWarning("Verification email template not found at {path}", _templatePath);
            template = "<p>Hello {{Username}}</p><p>Please verify: <a href='{{VerificationLink}}'>link</a></p>";
        }

        var tokenEncoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(verificationToken));
        var verificationLink = $"{_settings.VerificationLinkBaseUrl}?token={Uri.EscapeDataString(tokenEncoded)}";


        var expiryHours = TimeSpan.FromDays(1).TotalHours; // keep default until constants wired

        template = template.Replace("{{Username}}", username)
                           .Replace("{{VerificationLink}}", verificationLink.ToString())
                           .Replace("{{ExpiryHours}}", ((int)expiryHours).ToString());

        return template;
    }

    private async Task SendAsync(MimeMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient();
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            timeoutCts.CancelAfter(_settings.SendTimeoutMs);

            var secureSocket = _settings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;

            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, secureSocket, timeoutCts.Token).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(_settings.SmtpUsername))
            {
                await client.AuthenticateAsync(_settings.SmtpUsername, _settings.SmtpPassword ?? string.Empty, timeoutCts.Token).ConfigureAwait(false);
            }

            await client.SendAsync(message, timeoutCts.Token).ConfigureAwait(false);
            await client.DisconnectAsync(true, timeoutCts.Token).ConfigureAwait(false);

            _logger.LogInformation("Verification email sent to {to}", message.To.ToString());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("Email send cancelled for {to}", message.To.ToString());
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Email send timed out for {to}", message.To.ToString());
            throw new TimeoutException(PutZige.Application.Common.Messages.ErrorMessages.Email.EmailSendTimeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {to}", message.To.ToString());
            throw;
        }
    }
}
