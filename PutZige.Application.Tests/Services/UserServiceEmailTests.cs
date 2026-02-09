#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;
using PutZige.Application.Common.Constants;
using PutZige.Application.Common.Messages;
using PutZige.Application.Interfaces;
using PutZige.Application.Services;
using PutZige.Application.Settings;
using PutZige.Application.DTOs.Auth;
using PutZige.Domain.Entities;
using PutZige.Domain.Interfaces;
using Xunit;

namespace PutZige.Application.Tests.Services
{
    public class UserServiceEmailTests
    {
        private readonly Mock<IUserRepository> _userRepo = new();
        private readonly Mock<IUnitOfWork> _uow = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly Mock<IHashingService> _hashing = new();
        private readonly Mock<IDateTimeProvider> _mockDateTime = new();
        private readonly Mock<IBackgroundJobDispatcher> _bg = new();
        private readonly Mock<ILogger<UserService>> _logger = new();

        public UserServiceEmailTests()
        {
            _mockDateTime.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);
            _hashing.Setup(h => h.GenerateSecureToken(It.IsAny<int>())).Returns((int len) => {
                var bytes = new byte[Math.Max(1, len)];
                System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
                return Convert.ToBase64String(bytes).Replace("+","-").Replace("/","_").TrimEnd('=');
            });
            _hashing.Setup(h => h.GenerateEmailVerificationToken(It.IsAny<string>(), It.IsAny<int>()))
                .Returns((string emailArg, int len) =>
                {
                    var bytes = new byte[Math.Max(1, len)];
                    System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
                    var random = Convert.ToBase64String(bytes).Replace("+","-").Replace("/","_").TrimEnd('=');
                    var encodedEmail = Convert.ToBase64String(Encoding.UTF8.GetBytes(emailArg)).Replace("+","-").Replace("/","_").TrimEnd('=');
                    return $"{random}.{encodedEmail}";
                });
            _hashing.Setup(h => h.HashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string s, CancellationToken ct) => new PutZige.Application.DTOs.HashedValue("hash-"+s, "salt-"+s));
            _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mapper.Setup(m => m.Map<RegisterUserResponse>(It.IsAny<User>())).Returns((User u) => new RegisterUserResponse
            {
                UserId = u.Id,
                Email = u.Email ?? string.Empty,
                Username = u.Username ?? string.Empty,
                IsEmailVerified = u.IsEmailVerified,
                CreatedAt = u.CreatedAt
            });
        }

        [Fact]
        public async Task RegisterUserAsync_ValidRequest_QueuesVerificationEmail()
        {
            // Arrange
            var email = "queue_test@example.com";
            var username = "queueuser";
            var pwd = "Password1!";

            _userRepo.Setup(r => r.IsEmailTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var svc = CreateSvc();

            // Act
            await svc.RegisterUserAsync(email, username, pwd);

            // Assert
            _bg.Verify(d => d.EnqueueVerificationEmail(It.Is<string>(e => e == email), It.Is<string>(u => u == username), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RegisterUserAsync_ValidRequest_EmailJobContainsCorrectToken()
        {
            // Arrange
            var email = "queue_token@example.com";
            var username = "queueusertoken";
            var pwd = "Password1!";
            User? captured = null;

            _userRepo.Setup(r => r.IsEmailTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Callback<User, CancellationToken>((u, ct) => captured = u).Returns(Task.CompletedTask);

            var svc = CreateSvc();

            // Act
            await svc.RegisterUserAsync(email, username, pwd);

            // Assert
            _bg.Verify(d => d.EnqueueVerificationEmail(email, username, captured!.EmailVerificationToken), Times.Once);
        }

        [Fact]
        public async Task RegisterUserAsync_EmailJobEnqueueFails_UserStillCreated()
        {
            // Arrange
            var email = "queue_fail@example.com";
            var username = "queuefail";
            var pwd = "Password1!";

            _userRepo.Setup(r => r.IsEmailTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _bg.Setup(d => d.EnqueueVerificationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Throws(new Exception("enqueue failed"));

            var svc = CreateSvc();

            // Act
            var resp = await svc.RegisterUserAsync(email, username, pwd);

            // Assert - still created and returned
            resp.Should().NotBeNull();
            _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RegisterUserAsync_EmailJobEnqueueFails_LogsError()
        {
            // Arrange
            var email = "queue_fail_log@example.com";
            var username = "queuefaillog";
            var pwd = "Password1!";

            var mockLogger = new Mock<ILogger<UserService>>();
            var svc = new UserService(_userRepo.Object, _uow.Object, _mapper.Object, _hashing.Object, _mockDateTime.Object, _bg.Object, mockLogger.Object);

            _userRepo.Setup(r => r.IsEmailTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _bg.Setup(d => d.EnqueueVerificationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Throws(new Exception("enqueue failed"));

            // Act
            var resp = await svc.RegisterUserAsync(email, username, pwd);

            // Assert
            // Verify an Error-level log containing the failure message was written
            mockLogger.Verify(l => l.Log(
                It.Is<Microsoft.Extensions.Logging.LogLevel>(ll => ll == Microsoft.Extensions.Logging.LogLevel.Error),
                It.IsAny<Microsoft.Extensions.Logging.EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to enqueue verification email for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        private UserService CreateSvc()
        {
            return new UserService(_userRepo.Object, _uow.Object, _mapper.Object, _hashing.Object, _mockDateTime.Object, _bg.Object, _logger.Object);
        }

        [Fact]
        public async Task RegisterUserAsync_ValidRequest_GeneratesVerificationToken()
        {
            // Arrange
            var email = "user_token@example.com";
            var username = "user_token";
            var pwd = "Password1!";

            _userRepo.Setup(r => r.IsEmailTakenAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.IsUsernameTakenAsync(username, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var svc = CreateSvc();

            // Act
            var resp = await svc.RegisterUserAsync(email, username, pwd);

            // Assert - captured via repository call
            _userRepo.Verify(r => r.AddAsync(It.Is<User>(u => !string.IsNullOrWhiteSpace(u.EmailVerificationToken)), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RegisterUserAsync_ValidRequest_TokenIs32BytesOrMore()
        {
            // Arrange
            var email = "user_token_size@example.com";
            var username = "user_tokensize";
            var pwd = "Password1!";

            _userRepo.Setup(r => r.IsEmailTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            User? captured = null;
            _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Callback<User, CancellationToken>((u, ct) => captured = u).Returns(Task.CompletedTask);

            var svc = CreateSvc();

            // Act
            await svc.RegisterUserAsync(email, username, pwd);

            // Assert
            captured.Should().NotBeNull();
            // Extract random portion before the dot
            var composite = captured!.EmailVerificationToken!;
            var parts = composite.Split('.', 2);
            parts.Length.Should().Be(2);
            var randomPart = parts[0];
            var b64 = randomPart.Replace('-', '+').Replace('_', '/');
            // Restore padding for base64 if trimmed
            switch (b64.Length % 4)
            {
                case 2: b64 += "=="; break;
                case 3: b64 += "="; break;
                case 0: break;
                default: break;
            }

            var tokenBytes = System.Convert.FromBase64String(b64);
            tokenBytes.Length.Should().BeGreaterThanOrEqualTo(32);
        }

        [Fact]
        public async Task RegisterUserAsync_ValidRequest_TokenIsUrlSafe()
        {
            // Arrange
            var email = "user_token_urlsafe@example.com";
            var username = "user_urlsafe";
            var pwd = "Password1!";

            _userRepo.Setup(r => r.IsEmailTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            User? captured = null;
            _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Callback<User, CancellationToken>((u, ct) => captured = u).Returns(Task.CompletedTask);

            var svc = CreateSvc();

            // Act
            await svc.RegisterUserAsync(email, username, pwd);

            // Assert
            captured!.EmailVerificationToken!.Should().NotContain("+");
            captured.EmailVerificationToken!.Should().NotContain("/");
            captured.EmailVerificationToken!.Should().NotEndWith("=");
        }

        [Fact]
        public async Task RegisterUserAsync_ValidRequest_SetsExpiryTo7Days()
        {
            // Arrange
            var email = "user_token_expiry@example.com";
            var username = "user_expires";
            var pwd = "Password1!";

            _userRepo.Setup(r => r.IsEmailTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            User? captured = null;
            _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Callback<User, CancellationToken>((u, ct) => captured = u).Returns(Task.CompletedTask);

            var svc = CreateSvc();

            // Act
            await svc.RegisterUserAsync(email, username, pwd);

            // Assert
            captured!.EmailVerificationTokenExpiry.Should().BeCloseTo(DateTime.UtcNow.AddDays(AppConstants.Security.EmailVerificationTokenExpirationDays), TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task RegisterUserAsync_ValidRequest_SetsIsEmailVerifiedFalse()
        {
            // Arrange
            var email = "user_token_verifiedfalse@example.com";
            var username = "user_verifiedfalse";
            var pwd = "Password1!";

            _userRepo.Setup(r => r.IsEmailTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            User? captured = null;
            _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Callback<User, CancellationToken>((u, ct) => captured = u).Returns(Task.CompletedTask);

            var svc = CreateSvc();

            // Act
            await svc.RegisterUserAsync(email, username, pwd);

            // Assert
            captured!.IsEmailVerified.Should().BeFalse();
        }

        [Fact]
        public async Task RegisterUserAsync_TwoUsers_GenerateDifferentTokens()
        {
            // Arrange
            var email1 = "user1_unique@example.com";
            var username1 = "user1unique";
            var email2 = "user2_unique@example.com";
            var username2 = "user2unique";
            var pwd = "Password1!";

            _userRepo.Setup(r => r.IsEmailTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            User? cap1 = null; User? cap2 = null;
            var seq = 0;
            _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Callback<User, CancellationToken>((u, ct) => {
                if (seq++ == 0) cap1 = u; else cap2 = u;
            }).Returns(Task.CompletedTask);

            var svc = CreateSvc();

            // Act
            await svc.RegisterUserAsync(email1, username1, pwd);
            await svc.RegisterUserAsync(email2, username2, pwd);

            // Assert
            cap1.Should().NotBeNull();
            cap2.Should().NotBeNull();
            cap1!.EmailVerificationToken.Should().NotBe(cap2!.EmailVerificationToken);
        }
    }
}
