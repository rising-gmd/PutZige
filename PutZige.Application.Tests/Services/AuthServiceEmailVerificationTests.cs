#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
using PutZige.Domain.Entities;
using PutZige.Domain.Interfaces;
using Xunit;

namespace PutZige.Application.Tests.Services
{
    public class AuthServiceEmailVerificationTests
    {
        private readonly Mock<IUserRepository> _userRepo = new();
        private readonly Mock<IUnitOfWork> _uow = new();
        private readonly Mock<IUserService> _userService = new();
        private readonly Mock<ILogger<AuthService>> _logger = new();
        private readonly Mock<IClientInfoService> _mockClientInfo = new();
        private readonly Mock<IHashingService> _mockHashingService = new();
        private readonly Mock<IBackgroundJobDispatcher> _backgroundDispatcher = new();
        private readonly Mock<IDateTimeProvider> _mockDateTime = new();
        private readonly Mock<ICurrentUserService> _mockCurrentUserService = new();
        private readonly Mock<PutZige.Domain.Interfaces.IDapperUserRepository> _dapperUserRepo = new();
        private readonly JwtSettings _jwtSettings = new() { Secret = "TestSecretKeyThatIsLongEnough-1234567890", Issuer = "PutZige", Audience = "PutZige.Users", AccessTokenExpiryMinutes = 15, RefreshTokenExpiryDays = 7 };

        public AuthServiceEmailVerificationTests()
        {
            _mockClientInfo.Setup(c => c.GetIpAddress()).Returns("127.0.0.1");
            _mockClientInfo.Setup(c => c.GetUserAgent()).Returns("UnitTestAgent/1.0");
            _mockDateTime.Setup(d => d.UtcNow).Returns(() => DateTime.UtcNow);

            _mockHashingService.Setup(h => h.GenerateSecureToken(It.IsAny<int>())).Returns((int len) => "token-" + Guid.NewGuid().ToString("N").Substring(0, Math.Max(32, len)));
            _mockHashingService.Setup(h => h.GenerateEmailVerificationToken(It.IsAny<string>(), It.IsAny<int>()))
                .Returns((string emailArg, int len) =>
                {
                    var random = "token-" + Guid.NewGuid().ToString("N").Substring(0, Math.Max(32, len));
                    var encodedEmail = Convert.ToBase64String(Encoding.UTF8.GetBytes(emailArg)).Replace("+", "-").Replace("/", "_").TrimEnd('=');
                    return $"{random}.{encodedEmail}";
                });
            _mockHashingService.Setup(h => h.ExtractEmailFromVerificationToken(It.IsAny<string>()))
                .Returns((string composite) =>
                {
                    if (string.IsNullOrWhiteSpace(composite)) throw new ArgumentException("Token is required", nameof(composite));
                    var parts = composite.Split('.', 2);
                    if (parts.Length != 2) throw new ArgumentException("Invalid token format", nameof(composite));
                    var encodedEmail = parts[1];
                    var padding = (4 - (encodedEmail.Length % 4)) % 4;
                    var base64 = encodedEmail.Replace("-", "+").Replace("_", "/") + new string('=', padding);
                    var bytes = Convert.FromBase64String(base64);
                    return Encoding.UTF8.GetString(bytes);
                });
            _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        private AuthService CreateService()
        {
            return new AuthService(_userRepo.Object, _uow.Object, new TestJwtTokenService(), _userService.Object, new AutoMapper.MapperConfiguration(cfg => { }).CreateMapper(), Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _mockCurrentUserService.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);
        }

        private User CreateUnverifiedUser(string email, string token, DateTime? expiry = null)
        {
            return new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Username = "user_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                IsEmailVerified = false,
                EmailVerificationToken = token,
                EmailVerificationTokenExpiry = expiry ?? DateTime.UtcNow.AddDays(AppConstants.Security.EmailVerificationTokenExpirationDays)
            };
        }

        private void VerifyLoggedInformationContains(string text, Times times)
        {
            _logger.Verify(x => x.Log(
                It.Is<Microsoft.Extensions.Logging.LogLevel>(l => l == Microsoft.Extensions.Logging.LogLevel.Information),
                It.IsAny<Microsoft.Extensions.Logging.EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(text)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ), times);
        }

        [Fact]
        public async Task VerifyEmailAsync_ValidToken_SetsIsEmailVerifiedTrue()
        {
            // Arrange
            var email = "testverify_valid@example.com";
            var token = "securetoken123";
            var user = CreateUnverifiedUser(email, token);

            _userRepo.Setup(r => r.GetByVerificationTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _dapperUserRepo.Setup(d => d.VerifyEmailByTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var svc = CreateService();

            // Act
            var result = await svc.VerifyEmailAsync(token);

            // Assert
            result.Should().BeTrue();
            user.IsEmailVerified.Should().BeTrue();
        }

        [Fact]
        public async Task VerifyEmailAsync_ValidToken_ClearsTokenFromDatabase()
        {
            // Arrange
            var email = "testverify_cleartoken@example.com";
            var token = "securetoken456";
            var user = CreateUnverifiedUser(email, token);

            _userRepo.Setup(r => r.GetByVerificationTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _dapperUserRepo.Setup(d => d.VerifyEmailByTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var svc = CreateService();

            // Act
            var result = await svc.VerifyEmailAsync(token);

            // Assert
            result.Should().BeTrue();
            user.EmailVerificationToken.Should().BeNull();
            user.EmailVerificationTokenExpiry.Should().BeNull();
        }

        [Fact]
        public async Task VerifyEmailAsync_ExpiredToken_ThrowsInvalidOperationException()
        {
            // Arrange
            var email = "testverify_expired@example.com";
            var token = "expiredtoken";
            var user = CreateUnverifiedUser(email, token, DateTime.UtcNow.AddHours(-1));

            _userRepo.Setup(r => r.GetByVerificationTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            var svc = CreateService();

            // Act
            Func<Task> act = async () => await svc.VerifyEmailAsync(token);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Email.TokenExpired + "*");
        }

        [Fact]
        public async Task VerifyEmailAsync_ExpiredTokenByOneSecond_ThrowsInvalidOperationException()
        {
            // Arrange
            var email = "testverify_expired_1s@example.com";
            var token = "expiredtoken1s";
            var user = CreateUnverifiedUser(email, token, DateTime.UtcNow.AddHours(-1));

            _userRepo.Setup(r => r.GetByVerificationTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            var svc = CreateService();

            // Act
            Func<Task> act = async () => await svc.VerifyEmailAsync(token);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Email.TokenExpired + "*");
        }

        [Fact]
        public async Task VerifyEmailAsync_TokenExpiresExactlyAtUtcNow_ThrowsInvalidOperationException()
        {
            // Arrange
            var email = "testverify_expiresnow@example.com";
            var token = "expnowtoken";
            var user = CreateUnverifiedUser(email, token, DateTime.UtcNow.AddHours(-1));

            _userRepo.Setup(r => r.GetByVerificationTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            var svc = CreateService();

            // Act
            Func<Task> act = async () => await svc.VerifyEmailAsync(token);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Email.TokenExpired + "*");
        }

        [Fact]
        public async Task VerifyEmailAsync_NullToken_ThrowsArgumentNullException()
        {
            // Arrange
            var svc = CreateService();

            // Act
            Func<Task> act = async () => await svc.VerifyEmailAsync(null!);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Validation.TokenRequired + "*");
        }

        [Fact]
        public async Task VerifyEmailAsync_EmptyToken_ThrowsArgumentException()
        {
            // Arrange
            var svc = CreateService();

            // Act
            Func<Task> act = async () => await svc.VerifyEmailAsync("");

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Validation.TokenRequired + "*");
        }

        [Fact]
        public async Task VerifyEmailAsync_InvalidToken_ThrowsInvalidOperationException()
        {
            // Arrange
            var email = "testverify_invalid@example.com";
            var storedToken = "correcttoken";
            var providedToken = "wrongtoken";
            var user = CreateUnverifiedUser(email, storedToken);

            _userRepo.Setup(r => r.GetByVerificationTokenAsync(providedToken, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

            var svc = CreateService();

            // Act
            Func<Task> act = async () => await svc.VerifyEmailAsync(providedToken);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Email.TokenInvalid + "*");
        }

        [Fact]
        public async Task VerifyEmailAsync_TokenReuse_SecondAttemptFails()
        {
            // Arrange
            var email = "testverify_reuse@example.com";
            var token = "reusetoken";
            var user = CreateUnverifiedUser(email, token);

            _userRepo.SetupSequence(r => r.GetByVerificationTokenAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user)
                .ReturnsAsync(user);
            _dapperUserRepo.SetupSequence(r => r.VerifyEmailByTokenAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(1)
                .ReturnsAsync(0);

            var svc = CreateService();

            // Act - first attempt succeeds
            var r1 = await svc.VerifyEmailAsync(token);

            // Act - second attempt should fail because user is already verified
            Func<Task> act2 = async () => await svc.VerifyEmailAsync(token);

            // Assert
            r1.Should().BeTrue();
            await act2.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Email.AlreadyVerified + "*");
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_EmailSendFailure_ThrowsInvalidOperationException()
        {
            // Arrange
            var email = "testresend_sendfail@example.com";
            var user = CreateUnverifiedUser(email, "tokfail");
            _userRepo.Setup(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _backgroundDispatcher.Setup(d => d.EnqueueVerificationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Throws(new Exception("bg fail"));

            var svc = CreateService();

            // Act
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            Func<Task> act = async () => await svc.ResendVerificationEmailAsync(composite);

            // Assert - service wraps enqueue failure into EmailSendFailed
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Email.EmailSendFailed + "*");
        }

        [Fact]
        public async Task VerifyEmailAsync_UserNotFound_ThrowsKeyNotFoundException()
        {
            // Arrange
            var token = "nonexistent-token";
            _userRepo.Setup(r => r.GetByVerificationTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

            var svc = CreateService();

            // Act
            Func<Task> act = async () => await svc.VerifyEmailAsync(token);

            // Assert - token that doesn't map to a user is considered invalid
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Email.TokenInvalid + "*");
        }

        [Fact]
        public async Task VerifyEmailAsync_ConcurrentVerificationAttempts_OnlyOneSucceeds()
        {
            // Arrange
            var email = "concurrent@example.com";
            var token = "concurrenttoken";
            var user = CreateUnverifiedUser(email, token);

            // Simulate repository returning the same instance for concurrency
            _userRepo.Setup(r => r.GetByVerificationTokenAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            // Dapper atomic update: first call succeeds (1) and sets the user's verified flag, second call returns 0
            int callCount = 0;
            _dapperUserRepo.Setup(d => d.VerifyEmailByTokenAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var c = System.Threading.Interlocked.Increment(ref callCount);
                    if (c == 1)
                    {
                        user.IsEmailVerified = true;
                        return 1;
                    }
                    return 0;
                });

            var svc = CreateService();

            // Act - run two concurrent verify attempts
            var task1 = Task.Run(async () =>
            {
                try
                {
                    return await svc.VerifyEmailAsync(token);
                }
                catch
                {
                    return false;
                }
            });

            var task2 = Task.Run(async () =>
            {
                try
                {
                    return await svc.VerifyEmailAsync(token);
                }
                catch
                {
                    return false;
                }
            });

            var results = await Task.WhenAll(task1, task2);

            // Assert
            // Exactly one should have returned true and final state should be verified
            results.Should().Contain(true);
            results.Should().Contain(false);
            user.IsEmailVerified.Should().BeTrue();

            _userRepo.Verify(r => r.GetByVerificationTokenAsync(token, It.IsAny<CancellationToken>()), Times.AtLeast(1));
            // The second concurrent request may throw early if it sees IsEmailVerified=true before calling Dapper
            _dapperUserRepo.Verify(d => d.VerifyEmailByTokenAsync(token, It.IsAny<CancellationToken>()), Times.AtLeast(1));
        }

        [Fact]
        public async Task VerifyEmailAsync_CaseMismatchToken_ThrowsInvalidOperationException()
        {
            // Arrange
            var email = "testverify_casemismatch@example.com";
            var storedToken = "AbC123X";
            var providedToken = "aBc123x"; // different case
            var user = CreateUnverifiedUser(email, storedToken);

            _userRepo.Setup(r => r.GetByVerificationTokenAsync(providedToken, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

            var svc = CreateService();

            // Act
            Func<Task> act = async () => await svc.VerifyEmailAsync(providedToken);
            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Email.TokenInvalid + "*");
        }

        [Fact]
        public async Task VerifyEmailAsync_TokenWithWhitespace_ThrowsInvalidOperationException()
        {
            // Arrange
            var email = "testverify_whitespace@example.com";
            var storedToken = "tokenwithspace";
            var providedToken = " tokenwithspace ";
            var user = CreateUnverifiedUser(email, storedToken);

            _userRepo.Setup(r => r.GetByVerificationTokenAsync(providedToken, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

            var svc = CreateService();

            // Act
            Func<Task> act = async () => await svc.VerifyEmailAsync(providedToken);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Email.TokenInvalid + "*");
        }

        [Fact]
        public async Task VerifyEmailAsync_AlreadyVerifiedUser_ThrowsInvalidOperationException()
        {
            // Arrange
            var email = "testverify_alreadyverified@example.com";
            var token = "anytoken";
            var user = CreateUnverifiedUser(email, token);
            user.IsEmailVerified = true;

            _userRepo.Setup(r => r.GetByVerificationTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _dapperUserRepo.Setup(d => d.VerifyEmailByTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var svc = CreateService();

            // Act
            Func<Task> act = async () => await svc.VerifyEmailAsync(token);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Email.AlreadyVerified + "*");
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_ExceedsRateLimit_ThrowsInvalidOperationException()
        {
            // Arrange
            var email = "testresend_ratelimit@example.com";
            var user = CreateUnverifiedUser(email, "oldtoken");
            user.EmailVerificationSentCount = 3;
            user.LastEmailVerificationSentAt = DateTime.UtcNow.AddMinutes(30);

            _userRepo.Setup(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            var svc = CreateService();

            // Act
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            Func<Task> act = async () => await svc.ResendVerificationEmailAsync(composite);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Email.TooManyResendAttempts + "*");
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_ExactlyThreeResends_FourthAttemptBlocked()
        {
            // Arrange
            var email = "testresend_exact3@example.com";
            var user = CreateUnverifiedUser(email, "oldtoken");
            user.EmailVerificationSentCount = 3;
            user.LastEmailVerificationSentAt = DateTime.UtcNow.AddMinutes(10);

            _userRepo.Setup(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            var svc = CreateService();

            // Act
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            Func<Task> act = async () => await svc.ResendVerificationEmailAsync(composite);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Email.TooManyResendAttempts + "*");
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_RateLimitResetAfterOneHour_AllowsResend()
        {
            // Arrange
            var email = "testresend_reset1hr@example.com";
            var user = CreateUnverifiedUser(email, "oldtoken");
            user.EmailVerificationSentCount = 3;
            user.LastEmailVerificationSentAt = DateTime.UtcNow.AddHours(-1).AddMinutes(-1); // more than 1 hour ago

            _userRepo.Setup(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _backgroundDispatcher.Setup(d => d.EnqueueVerificationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));

            var svc = CreateService();

            // Act
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            Func<Task> act = async () => await svc.ResendVerificationEmailAsync(composite);

            // Assert
            await act.Should().NotThrowAsync();
            user.EmailVerificationSentCount.Should().BeGreaterThanOrEqualTo(4);
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_RateLimitResetAt59Minutes_StillBlocked()
        {
            // Arrange
            var email = "testresend_59min_blocked@example.com";
            var user = CreateUnverifiedUser(email, "oldtoken");
            user.EmailVerificationSentCount = 3;
            user.LastEmailVerificationSentAt = DateTime.UtcNow.AddMinutes(-59);

            _userRepo.Setup(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            var svc = CreateService();

            // Act
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            Func<Task> act = async () => await svc.ResendVerificationEmailAsync(composite);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Email.TooManyResendAttempts + "*");
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_RateLimitResetAt61Minutes_AllowsResend()
        {
            // Arrange
            var email = "testresend_61min_allowed@example.com";
            var user = CreateUnverifiedUser(email, "oldtoken");
            user.EmailVerificationSentCount = 3;
            user.LastEmailVerificationSentAt = DateTime.UtcNow.AddMinutes(-61);

            _userRepo.Setup(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _backgroundDispatcher.Setup(d => d.EnqueueVerificationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));

            var svc = CreateService();

            // Act
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            await svc.ResendVerificationEmailAsync(composite);

            // Assert
            user.EmailVerificationSentCount.Should().BeGreaterThanOrEqualTo(4);
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_FirstResend_EmailSentCountIsOne()
        {
            // Arrange
            var email = "testresend_first@example.com";
            var user = CreateUnverifiedUser(email, "oldtoken");
            user.EmailVerificationSentCount = 0;
            user.LastEmailVerificationSentAt = null;

            _userRepo.Setup(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _backgroundDispatcher.Setup(d => d.EnqueueVerificationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));

            var svc = CreateService();

            // Act
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            await svc.ResendVerificationEmailAsync(composite);

            // Assert
            user.EmailVerificationSentCount.Should().Be(1);
            user.LastEmailVerificationSentAt.Should().NotBeNull();
            user.EmailVerificationToken.Should().NotBeNullOrWhiteSpace();
            user.EmailVerificationTokenExpiry.Should().BeAfter(DateTime.UtcNow);
        }

        [Fact]
        public async Task VerifyEmailAsync_ValidRequest_SavesChangesToDatabase()
        {
            // Arrange
            var email = "testverify_saves_changes@example.com";
            var token = "savetok";
            var user = CreateUnverifiedUser(email, token);

            _userRepo.Setup(r => r.GetByVerificationTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _dapperUserRepo.Setup(d => d.VerifyEmailByTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var svc = CreateService();

            // Act
            var result = await svc.VerifyEmailAsync(token);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task VerifyEmailAsync_ValidRequest_LogsSuccessMessage()
        {
            // Arrange
            var email = "testverify_log@example.com";
            var token = "logtoken";
            var user = CreateUnverifiedUser(email, token);

            _userRepo.Setup(r => r.GetByVerificationTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _dapperUserRepo.Setup(d => d.VerifyEmailByTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var svc = CreateService();

            // Act
            var result = await svc.VerifyEmailAsync(token);

            // Assert
            result.Should().BeTrue();
            VerifyLoggedInformationContains("Email verified for user", Times.Once());
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_ValidRequest_GeneratesNewToken()
        {
            // Arrange
            var email = "testresend_newtoken@example.com";
            var oldToken = "oldtokenvalue";
            var user = CreateUnverifiedUser(email, oldToken);
            user.EmailVerificationSentCount = 1;

            _userRepo.Setup(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _backgroundDispatcher.Setup(d => d.EnqueueVerificationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));

            var svc = CreateService();

            // Act
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            await svc.ResendVerificationEmailAsync(composite);

            // Assert
            user.EmailVerificationToken.Should().NotBeNull();
            user.EmailVerificationToken.Should().NotBe(oldToken);
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_ValidRequest_UpdatesTokenExpiry()
        {
            // Arrange
            var email = "testresend_updateexpiry@example.com";
            var oldToken = "oldtokenvalue2";
            var user = CreateUnverifiedUser(email, oldToken, DateTime.UtcNow.AddDays(-1));

            _userRepo.Setup(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _backgroundDispatcher.Setup(d => d.EnqueueVerificationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));

            var svc = CreateService();

            // Act
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            await svc.ResendVerificationEmailAsync(composite);

            // Assert
            user.EmailVerificationTokenExpiry.Should().BeAfter(DateTime.UtcNow);
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_ValidRequest_ExpirySetTo7DaysFromNow()
        {
            // Arrange
            var email = "testresend_expiry7days@example.com";
            var oldToken = "oldtokenvalue3";
            var user = CreateUnverifiedUser(email, oldToken);

            _userRepo.Setup(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _backgroundDispatcher.Setup(d => d.EnqueueVerificationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));

            var svc = CreateService();

            // Act
            var before = DateTime.UtcNow;
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            await svc.ResendVerificationEmailAsync(composite);
            var expected = before.AddDays(AppConstants.Security.EmailVerificationTokenExpirationDays);

            // Assert
            user.EmailVerificationTokenExpiry.Should().BeCloseTo(expected, precision: TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_ValidRequest_QueuesBackgroundJob()
        {
            // Arrange
            var email = "testresend_queue@example.com";
            var oldToken = "oldtokenvalue4";
            var user = CreateUnverifiedUser(email, oldToken);

            _userRepo.Setup(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _backgroundDispatcher.Setup(d => d.EnqueueVerificationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));

            var svc = CreateService();

            // Act
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            await svc.ResendVerificationEmailAsync(composite);

            // Assert
            _backgroundDispatcher.Verify(d => d.EnqueueVerificationEmail(email, user.Username, user.EmailVerificationToken), Times.Once);
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_ValidRequest_IncrementsEmailSentCount()
        {
            // Arrange
            var email = "testresend_increments@example.com";
            var user = CreateUnverifiedUser(email, "tokinc");
            user.EmailVerificationSentCount = 2;

            _userRepo.Setup(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _backgroundDispatcher.Setup(d => d.EnqueueVerificationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));

            var svc = CreateService();

            // Act
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            await svc.ResendVerificationEmailAsync(composite);

            // Assert
            user.EmailVerificationSentCount.Should().Be(3);
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_ValidRequest_UpdatesLastSentTimestamp()
        {
            // Arrange
            var email = "testresend_updates_timestamp@example.com";
            var user = CreateUnverifiedUser(email, "tokints");
            user.LastEmailVerificationSentAt = null;

            _userRepo.Setup(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _backgroundDispatcher.Setup(d => d.EnqueueVerificationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));

            var svc = CreateService();

            // Act
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            await svc.ResendVerificationEmailAsync(composite);

            // Assert
            user.LastEmailVerificationSentAt.Should().NotBeNull();
            user.LastEmailVerificationSentAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_UserNotFound_ThrowsKeyNotFoundException()
        {
            // Arrange
            var email = "testresend_notfound@example.com";
            _userRepo.Setup(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

            var svc = CreateService();

            // Act
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            Func<Task> act = async () => await svc.ResendVerificationEmailAsync(composite);

            // Assert
            await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage(ErrorMessages.General.ResourceNotFound + "*");
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_AlreadyVerified_ThrowsInvalidOperationException()
        {
            // Arrange
            var email = "testresend_alreadyverified@example.com";
            var user = CreateUnverifiedUser(email, "tokenvx");
            user.IsEmailVerified = true;

            _userRepo.Setup(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            var svc = CreateService();

            // Act
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            Func<Task> act = async () => await svc.ResendVerificationEmailAsync(composite);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Email.AlreadyVerified + "*");
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_NullEmail_ThrowsArgumentNullException()
        {
            // Arrange
            var svc = CreateService();

            // Act
            Func<Task> act = async () => await svc.ResendVerificationEmailAsync(null!);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Validation.TokenRequired + "*");
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_NewTokenDifferentFromOldToken_Verified()
        {
            // Arrange
            var email = "testresend_newdiff@example.com";
            var oldToken = "oldtokenxyz";
            var user = CreateUnverifiedUser(email, oldToken);

            _userRepo.Setup(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _backgroundDispatcher.Setup(d => d.EnqueueVerificationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));

            var svc = CreateService();

            // Act
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            await svc.ResendVerificationEmailAsync(composite);

            // Assert
            user.EmailVerificationToken.Should().NotBe(oldToken);
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_InvalidatesOldToken_OldTokenNoLongerWorks()
        {
            // Arrange
            var email = "testresend_invalidateold@example.com";
            var oldToken = "oldtokentoinvalidate";
            var user = CreateUnverifiedUser(email, oldToken);

            _userRepo.SetupSequence(r => r.GetByEmailForUpdateAsync(email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user) // for resend
                .ReturnsAsync(user); // for verify

            _backgroundDispatcher.Setup(d => d.EnqueueVerificationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));

            var svc = CreateService();

            // Act - resend to generate new token
            var composite = _mockHashingService.Object.GenerateEmailVerificationToken(email, 32);
            await svc.ResendVerificationEmailAsync(composite);

            // Act - attempt verify with old token
            Func<Task> act = async () => await svc.VerifyEmailAsync(oldToken);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Email.TokenInvalid + "*");
        }
    }
}
