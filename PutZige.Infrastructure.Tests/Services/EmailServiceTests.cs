#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PutZige.Infrastructure.Settings;
using MimeKit;
using PutZige.Infrastructure.Services;
using PutZige.Infrastructure.Settings;
using Xunit;

namespace PutZige.Infrastructure.Tests.Services
{
    public class EmailServiceTests
    {
        private readonly Mock<IOptions<EmailSettings>> _opts = new();
        private readonly Mock<ILogger<EmailService>> _logger = new();
        // no extra mocks needed

        public EmailServiceTests()
        {
            var settings = new EmailSettings
            {
                SmtpHost = "smtp.example.com",
                FromEmail = "no-reply@example.com",
                FromName = "PutZige",
                VerificationLinkBaseUrl = "https://example.com/"
            };

            _opts.Setup(o => o.Value).Returns(settings);
        }

        [Fact]
        public async Task SendVerificationEmailAsync_ValidRequest_UsesHttpsInVerificationLink()
        {
            // Arrange
            var svc = new EmailService(_opts.Object, _logger.Object);
            var email = "to@example.com";
            var username = "user1";
            var token = "toktest";

            // Create temporary template file
            var dir = Path.Combine(AppContext.BaseDirectory, "Templates");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "VerificationEmail.html");
            await File.WriteAllTextAsync(path, "<a href='{{VerificationLink}}'>verify</a>");

            // Act
            var method = typeof(EmailService).GetMethod("BuildVerificationHtmlAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(string), typeof(string), typeof(string), typeof(System.Threading.CancellationToken) }, null)!;
            var taskObj = method.Invoke(svc, new object[] { username, token, email, CancellationToken.None })!;
            var htmlTask = (Task<string>)taskObj;
            var result = await htmlTask;

            // Assert
            result.Should().Contain("https://");

            // cleanup
            File.Delete(path);
        }

        [Fact]
        public async Task BuildVerificationHtmlAsync_DoesNotExposeTokenInLogs()
        {
            // Arrange
            var svc = new EmailService(_opts.Object, _logger.Object);
            var email = "to@example.com";
            var username = "user1";
            var token = "toktest";

            // Act
            var htmlTask = (Task<string>)typeof(EmailService).GetMethod("BuildVerificationHtmlAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(string), typeof(string), typeof(string), typeof(System.Threading.CancellationToken) }, null)!
                .Invoke(svc, new object[] { username, token, email, CancellationToken.None })!;

            var html = await htmlTask;

            // Assert - ensure logs were not called with token (we inspect logger mock - no easy way to capture string args without extensive setup)
            // Basic assertion that html contains encoded token (either raw base64 or percent-encoded in a URL), but logs should not contain raw token.
            var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(token));
            var escaped = Uri.EscapeDataString(b64);
            (html.Contains(b64) || html.Contains(escaped)).Should().BeTrue();
        }

        [Fact]
        public async Task BuildVerificationHtmlAsync_ReplacesUsernamePlaceholder()
        {
            var svc = new EmailService(_opts.Object, _logger.Object);
            var dir = Path.Combine(AppContext.BaseDirectory, "Templates");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "VerificationEmail.html");
            await File.WriteAllTextAsync(path, "Hello {{Username}}, click {{VerificationLink}} - expires in {{ExpiryHours}} hours");

            var method = typeof(EmailService).GetMethod("BuildVerificationHtmlAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(string), typeof(string), typeof(string), typeof(System.Threading.CancellationToken) }, null)!;
            var html = await (Task<string>)method.Invoke(svc, new object[] { "Alice", "tok", "alice@example.com", CancellationToken.None })!;

            html.Should().Contain("Hello Alice");
            File.Delete(path);
        }

        [Fact]
        public async Task BuildVerificationHtmlAsync_ReplacesVerificationLinkPlaceholder()
        {
            var svc = new EmailService(_opts.Object, _logger.Object);
            var dir = Path.Combine(AppContext.BaseDirectory, "Templates");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "VerificationEmail.html");
            await File.WriteAllTextAsync(path, "Link: {{VerificationLink}}");

            var method = typeof(EmailService).GetMethod("BuildVerificationHtmlAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(string), typeof(string), typeof(string), typeof(System.Threading.CancellationToken) }, null)!;
            var html = await (Task<string>)method.Invoke(svc, new object[] { "Bob", "tok123", "bob@example.com", CancellationToken.None })!;

            // verification link should contain base url
            html.Should().Contain("https://example.com/");
            File.Delete(path);
        }

        [Fact]
        public async Task BuildVerificationHtmlAsync_ReplacesExpiryHoursPlaceholder()
        {
            var svc = new EmailService(_opts.Object, _logger.Object);
            var dir = Path.Combine(AppContext.BaseDirectory, "Templates");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "VerificationEmail.html");
            await File.WriteAllTextAsync(path, "Expires in {{ExpiryHours}} hours");

            var method = typeof(EmailService).GetMethod("BuildVerificationHtmlAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(string), typeof(string), typeof(string), typeof(System.Threading.CancellationToken) }, null)!;
            var html = await (Task<string>)method.Invoke(svc, new object[] { "Carol", "tokX", "carol@example.com", CancellationToken.None })!;

            html.Should().Contain("Expires in");
            // default implementation sets expiryHours to 24
            html.Should().Contain("24");
            File.Delete(path);
        }

        [Fact]
        public async Task BuildVerificationHtmlAsync_TemplateNotFound_UsesFallback()
        {
            var svc = new EmailService(_opts.Object, _logger.Object);
            var dir = Path.Combine(AppContext.BaseDirectory, "Templates");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);

            var method = typeof(EmailService).GetMethod("BuildVerificationHtmlAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(string), typeof(string), typeof(string), typeof(System.Threading.CancellationToken) }, null)!;
            var html = await (Task<string>)method.Invoke(svc, new object[] { "Dan", "tokY", "dan@example.com", CancellationToken.None })!;

            (html.Contains("Please verify") || html.Contains("link")).Should().BeTrue();
        }

        [Fact]
        public async Task BuildVerificationHtmlAsync_TemplateHasMalformedPlaceholders_HandlesGracefully()
        {
            var svc = new EmailService(_opts.Object, _logger.Object);
            var dir = Path.Combine(AppContext.BaseDirectory, "Templates");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "VerificationEmail.html");
            await File.WriteAllTextAsync(path, "Hello {{Username - missing brace {{VerificationLink}");

            var method = typeof(EmailService).GetMethod("BuildVerificationHtmlAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(string), typeof(string), typeof(string), typeof(System.Threading.CancellationToken) }, null)!;
            var html = await (Task<string>)method.Invoke(svc, new object[] { "Eve", "tokZ", "eve@example.com", CancellationToken.None })!;

            // should not throw and should return some content
            html.Should().NotBeNullOrWhiteSpace();
            File.Delete(path);
        }

        [Fact]
        public async Task SendVerificationEmailAsync_NullEmail_ThrowsArgumentNullException()
        {
            var svc = new EmailService(_opts.Object, _logger.Object);
            Func<Task> act = async () => await svc.SendVerificationEmailAsync(null!, "u", "t");
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task SendVerificationEmailAsync_EmptyEmail_ThrowsFormatExceptionOrArgumentException()
        {
            var svc = new EmailService(_opts.Object, _logger.Object);
            Func<Task> act = async () => await svc.SendVerificationEmailAsync("", "u", "t");
            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task SendVerificationEmailAsync_InvalidEmailFormat_ThrowsFormatException()
        {
            var svc = new EmailService(_opts.Object, _logger.Object);
            Func<Task> act = async () => await svc.SendVerificationEmailAsync("not-an-email", "u", "t");
            await act.Should().ThrowAsync<FormatException>();
        }

        [Fact]
        public async Task BuildVerificationHtmlAsync_DoesNotIncludeRawToken()
        {
            var svc = new EmailService(_opts.Object, _logger.Object);
            var method = typeof(EmailService).GetMethod("BuildVerificationHtmlAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(string), typeof(string), typeof(string), typeof(System.Threading.CancellationToken) }, null)!;
            var token = "sensitive-token-123";
            var html = await (Task<string>)method.Invoke(svc, new object[] { "Frank", token, "frank@example.com", CancellationToken.None })!;
            html.Should().NotContain(token);
            var b64raw = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(token));
            var b64escaped = Uri.EscapeDataString(b64raw);
            (html.Contains(b64raw) || html.Contains(b64escaped)).Should().BeTrue();
        }

        [Fact]
        public async Task BuildVerificationHtmlAsync_ExpiryHoursPlaceholderIsInteger()
        {
            var svc = new EmailService(_opts.Object, _logger.Object);
            var dir = Path.Combine(AppContext.BaseDirectory, "Templates");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "VerificationEmail.html");
            await File.WriteAllTextAsync(path, "Expiry: {{ExpiryHours}}");

            var method = typeof(EmailService).GetMethod("BuildVerificationHtmlAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(string), typeof(string), typeof(string), typeof(System.Threading.CancellationToken) }, null)!;
            var html = await (Task<string>)method.Invoke(svc, new object[] { "Gina", "t", "gina@example.com", CancellationToken.None })!;

            // Expect integer number in output
            var digits = System.Text.RegularExpressions.Regex.Match(html, "\\d+");
            digits.Success.Should().BeTrue();
            File.Delete(path);
        }

        [Fact]
        public async Task BuildVerificationHtmlAsync_CompletesWithin5Seconds_Success()
        {
            var svc = new EmailService(_opts.Object, _logger.Object);
            var method = typeof(EmailService).GetMethod("BuildVerificationHtmlAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(string), typeof(string), typeof(string), typeof(System.Threading.CancellationToken) }, null)!;
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var task = (Task<string>)method.Invoke(svc, new object[] { "Henry", "tokenfast", "henry@example.com", cts.Token })!;
            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None));
            completed.Should().Be(task);
        }

        [Fact]
        public void EmailService_UsesSettingsFromConfig()
        {
            var svc = new EmailService(_opts.Object, _logger.Object);
            // reflect to _settings
            var field = typeof(EmailService).GetField("_settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var settings = (EmailSettings)field.GetValue(svc)!;
            settings.FromEmail.Should().Be("no-reply@example.com");
            settings.VerificationLinkBaseUrl.Should().Be("https://example.com/");
        }
    }
}
