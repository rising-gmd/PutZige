#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PutZige.Application.Services;
using PutZige.Application.Interfaces;
using PutZige.Application.DTOs;
using PutZige.Application.DTOs.Auth;
using PutZige.Domain.Interfaces;
using PutZige.Domain.Entities;
using PutZige.Application.Settings;
using PutZige.Application.Common.Messages;

namespace PutZige.Application.Tests.Services
{
    public class AuthServiceTests
    {
        private readonly Mock<IUserRepository> _userRepo = new();
        private readonly Mock<IUnitOfWork> _uow = new();
        private readonly Mock<IUserService> _userService = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly Mock<ILogger<AuthService>> _logger = new();
        private readonly Mock<IClientInfoService> _mockClientInfo = new();
        private readonly Mock<IHashingService> _mockHashingService = new();
        private readonly Mock<IDateTimeProvider> _mockDateTime = new();
        private readonly Mock<PutZige.Domain.Interfaces.IDapperUserRepository> _dapperUserRepo = new();
        private readonly Mock<IBackgroundJobDispatcher> _backgroundDispatcher = new();

        private readonly JwtSettings _jwtSettings = new() { Secret = "TestSecretKeyThatIsLongEnough-1234567890", Issuer = "PutZige", Audience = "PutZige.Users", AccessTokenExpiryMinutes = 15, RefreshTokenExpiryDays = 7 };

        private IJwtTokenService CreateJwtService() => new TestJwtTokenService();

        public AuthServiceTests()
        {
            _mockClientInfo.Setup(c => c.GetIpAddress()).Returns("127.0.0.1");
            _mockClientInfo.Setup(c => c.GetUserAgent()).Returns("UnitTestAgent/1.0");

            _mockHashingService.Setup(h => h.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _mockHashingService.Setup(h => h.HashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string s, CancellationToken ct) => new HashedValue("hash-"+s, "salt-"+s));
            _mockDateTime.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);
        }

        /// <summary>
        /// Valid username login returns login response and calls username lookup.
        /// </summary>
        [Fact]
        public async Task LoginAsync_ValidUsername_ReturnsLoginResponse()
        {
            // Arrange
            var username = "user1";
            var password = "Password123!";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "user@test.com",
                Username = username,
                DisplayName = "User One",
                PasswordHash = "hash-Password123!",
                PasswordSalt = "salt-Password123!",
                IsActive = true,
                IsEmailVerified = true
            };

            _userRepo.Setup(x => x.GetByUsernameWithSessionAsync(username, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act
            var result = await svc.LoginAsync(username, password, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.AccessToken.Should().NotBeNullOrWhiteSpace();
            result.RefreshToken.Should().NotBeNullOrWhiteSpace();
            result.Username.Should().Be(username);
            _userRepo.Verify(x => x.GetByUsernameWithSessionAsync(username, It.IsAny<CancellationToken>()), Times.Once);
            _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        /// <summary>
        /// Non-existent username input returns invalid credentials.
        /// </summary>
        [Fact]
        public async Task LoginAsync_NonExistentUsername_ThrowsInvalidOperationException()
        {
            // Arrange
            var username = "nouser";
            _userRepo.Setup(x => x.GetByUsernameWithSessionAsync(username, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act
            Func<Task> act = async () => await svc.LoginAsync(username, "any", CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Authentication.InvalidCredentials + "*");
        }

        /// <summary>
        /// Valid credentials produce access and refresh tokens and persist session changes.
        /// </summary>
        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsLoginResponse()
        {
            // Arrange
            var email = "user@test.com";
            var password = "Password123!";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Username = "user1",
                DisplayName = "User One",
                PasswordHash = "hash-Password123!",
                PasswordSalt = "salt-Password123!",
                IsActive = true,
                IsEmailVerified = true
            };

            _userRepo.Setup(x => x.GetByEmailWithSessionAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act
            var result = await svc.LoginAsync(email, password, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.AccessToken.Should().NotBeNullOrWhiteSpace();
            result.RefreshToken.Should().NotBeNullOrWhiteSpace();
            result.Email.Should().Be(email);
            _userRepo.Verify(x => x.GetByEmailWithSessionAsync(email, It.IsAny<CancellationToken>()), Times.Once);
            _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        /// <summary>
        /// Invalid password increments failed attempts and locks account after fifth attempt.
        /// </summary>
        [Fact]
        public async Task LoginAsync_InvalidPassword_IncrementsFailedAttempts_AndLocksOnFifth()
        {
            // Arrange
            var email = "baduser@test.com";
            var correct = "CorrectPass1!";
            var wrong = "WrongPass!";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Username = "baduser",
                DisplayName = "Bad User",
                PasswordHash = "hash-CorrectPass1!",
                PasswordSalt = "salt-CorrectPass1!",
                IsActive = true,
                IsEmailVerified = true,
                FailedLoginAttempts = 0
            };

            _mockHashingService.Setup(h => h.VerifyAsync(wrong, user.PasswordHash, user.PasswordSalt, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _userRepo.Setup(x => x.GetByEmailWithSessionAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(() => user);
            _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act & Assert: perform 4 failed attempts
            for (int i = 1; i <= 4; i++)
            {
                Func<Task> act = async () => await svc.LoginAsync(email, wrong, CancellationToken.None);
                await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Authentication.InvalidCredentials + "*");
                user.FailedLoginAttempts.Should().Be(i);
                user.IsLocked.Should().BeFalse();
            }

            // 5th attempt locks
            Func<Task> act5 = async () => await svc.LoginAsync(email, wrong, CancellationToken.None);
            await act5.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Authentication.InvalidCredentials + "*");
            user.FailedLoginAttempts.Should().Be(5);
            user.IsLocked.Should().BeTrue();
            user.LockedUntil.Should().BeAfter(DateTime.UtcNow);
        }

        /// <summary>
        /// Locked accounts throw an account locked error when attempting login.
        /// </summary>
        [Fact]
        public async Task LoginAsync_LockedAccount_ThrowsInvalidOperationException()
        {
            // Arrange
            var email = "locked@test.com";
            var password = "Whatever1!";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Username = "locked",
                PasswordHash = "hash-Whatever1!",
                PasswordSalt = "salt-Whatever1!",
                IsActive = true,
                IsEmailVerified = true,
                IsLocked = true,
                LockedUntil = DateTime.UtcNow.AddMinutes(10)
            };

            _userRepo.Setup(x => x.GetByEmailWithSessionAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act
            Func<Task> act = async () => await svc.LoginAsync(email, password, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Authentication.AccountLocked + "*");
        }

        /// <summary>
        /// Non-existent email input returns invalid credentials.
        /// </summary>
        [Fact]
        public async Task LoginAsync_NonExistentEmail_ThrowsInvalidOperationException()
        {
            // Arrange
            var email = "noexist@test.com";
            _userRepo.Setup(x => x.GetByEmailWithSessionAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act
            Func<Task> act = async () => await svc.LoginAsync(email, "any", CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Authentication.InvalidCredentials + "*");
        }

        /// <summary>
        /// Inactive account attempts return account inactive error.
        /// </summary>
        [Fact]
        public async Task LoginAsync_InactiveAccount_ThrowsInvalidOperationException()
        {
            // Arrange
            var email = "inactive@test.com";
            var user = new User { Email = email, PasswordHash = "hash-P1!", PasswordSalt = "salt-P1!", IsActive = false, IsEmailVerified = true };
            _userRepo.Setup(x => x.GetByEmailWithSessionAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            Func<Task> act = async () => await svc.LoginAsync(email, "P1!", CancellationToken.None);
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Authentication.AccountInactive + "*");
        }

        /// <summary>
        /// Unverified email attempts return email not verified error.
        /// </summary>
        [Fact]
        public async Task LoginAsync_UnverifiedEmail_ThrowsInvalidOperationException()
        {
            // Arrange
            var email = "unverified@test.com";
            var user = new User { Email = email, PasswordHash = "hash-P1!", PasswordSalt = "salt-P1!", IsActive = true, IsEmailVerified = false };
            _userRepo.Setup(x => x.GetByEmailWithSessionAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            Func<Task> act = async () => await svc.LoginAsync(email, "P1!", CancellationToken.None);
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Authentication.EmailNotVerified + "*");
        }

        /// <summary>
        /// Valid refresh token returns new access and refresh tokens and updates session hash.
        /// </summary>
        [Fact]
        public async Task RefreshTokenAsync_ValidToken_ReturnsNewAccessToken()
        {
            // Arrange
            var email = "refresh@test.com";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Username = "refuser",
                PasswordHash = "hash-P@ssw0rd",
                PasswordSalt = "salt-P@ssw0rd",
                IsActive = true,
                IsEmailVerified = true,
                Session = new UserSession
                {
                    RefreshTokenHash = "hash-valid-refresh",
                    RefreshTokenSalt = "salt-valid-refresh",
                    RefreshTokenExpiry = DateTime.UtcNow.AddDays(1)
                }
            };

            _mockHashingService.Setup(h => h.VerifyAsync("valid-refresh", user.Session.RefreshTokenHash!, user.Session.RefreshTokenSalt!, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _userRepo.Setup(x => x.GetByRefreshTokenAsync("valid-refresh", It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act
            var resp = await svc.RefreshTokenAsync("valid-refresh", CancellationToken.None);

            // Assert
            resp.Should().NotBeNull();
            resp.AccessToken.Should().NotBeNullOrWhiteSpace();
            resp.RefreshToken.Should().NotBeNullOrWhiteSpace();
            user.Session!.RefreshTokenHash.Should().NotBe("valid-refresh");
            _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Expired refresh token throws an invalid refresh token error.
        /// </summary>
        [Fact]
        public async Task RefreshTokenAsync_ExpiredToken_ThrowsInvalidOperationException()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "expired@test.com",
                Session = new UserSession
                {
                    RefreshTokenHash = "hash-old-refresh",
                    RefreshTokenSalt = "salt-old-refresh",
                    RefreshTokenExpiry = DateTime.UtcNow.AddDays(-1)
                }
            };

            _userRepo.Setup(x => x.GetByRefreshTokenAsync("old-refresh", It.IsAny<CancellationToken>())).ReturnsAsync(user);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act
            Func<Task> act = async () => await svc.RefreshTokenAsync("old-refresh", CancellationToken.None);

            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Authentication.InvalidRefreshToken + "*");
        }

        /// <summary>
        /// Invalid refresh token input throws invalid refresh token error.
        /// </summary>
        [Fact]
        public async Task RefreshTokenAsync_InvalidToken_ThrowsInvalidOperationException()
        {
            // Arrange
            _userRepo.Setup(x => x.GetByRefreshTokenAsync("bad-token", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            Func<Task> act = async () => await svc.RefreshTokenAsync("bad-token", CancellationToken.None);
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Authentication.InvalidRefreshToken + "*");
        }

        /// <summary>
        /// Expired lockout automatically unlocks and allows next successful login.
        /// </summary>
        [Fact]
        public async Task LoginAsync_ExpiredLockout_AutoUnlocksAndAllowsLogin()
        {
            // Arrange
            var email = "expiredlock@test.com";
            var password = "Password1!";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Username = "expiredlock",
                PasswordHash = "hash-Password1!",
                PasswordSalt = "salt-Password1!",
                IsActive = true,
                IsEmailVerified = true,
                IsLocked = true,
                LockedUntil = DateTime.UtcNow.AddMinutes(-5), // expired
                FailedLoginAttempts = 4
            };

            _userRepo.Setup(x => x.GetByEmailWithSessionAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act
            var resp = await svc.LoginAsync(email, password, CancellationToken.None);

            // Assert
            resp.Should().NotBeNull();
            user.IsLocked.Should().BeFalse();
            user.LockedUntil.Should().BeNull();
            user.FailedLoginAttempts.Should().Be(0);
        }

        /// <summary>
        /// Successful login after previous failed attempts resets the counter and clears last failed time.
        /// </summary>
        [Fact]
        public async Task LoginAsync_SuccessfulLoginAfterFailedAttempts_ResetsFailedAttemptsCounter()
        {
            // Arrange
            var email = "afterfails@test.com";
            var password = "GoodPass1!";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Username = "afterfails",
                PasswordHash = "hash-GoodPass1!",
                PasswordSalt = "salt-GoodPass1!",
                IsActive = true,
                IsEmailVerified = true,
                FailedLoginAttempts = 3,
                LastFailedLoginAttempt = DateTime.UtcNow.AddMinutes(-1)
            };

            _userRepo.Setup(x => x.GetByEmailWithSessionAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act
            var resp = await svc.LoginAsync(email, password, CancellationToken.None);

            // Assert
            resp.Should().NotBeNull();
            user.FailedLoginAttempts.Should().Be(0);
            user.LastFailedLoginAttempt.Should().BeNull();
        }

        /// <summary>
        /// Locked accounts with null LockedUntil remain locked and throw.
        /// </summary>
        [Fact]
        public async Task LoginAsync_LockedAccountWithNullLockedUntil_StillThrowsLocked()
        {
            // Arrange
            var email = "weirdlock@test.com";
            var password = "XyZ1!";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Username = "weirdlock",
                PasswordHash = "hash-XyZ1!",
                PasswordSalt = "salt-XyZ1!",
                IsActive = true,
                IsEmailVerified = true,
                IsLocked = true,
                LockedUntil = null
            };

            _userRepo.Setup(x => x.GetByEmailWithSessionAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act
            Func<Task> act = async () => await svc.LoginAsync(email, password, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Authentication.AccountLocked + "*");
        }

        /// <summary>
        /// Successful login updates last login timestamp and IP address.
        /// </summary>
        [Fact]
        public async Task LoginAsync_UpdatesLastLoginAtAndLastLoginIp_OnSuccess()
        {
            // Arrange
            var email = "lastlogin@test.com";
            var password = "Login1!";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Username = "lastlogin",
                PasswordHash = "hash-Login1!",
                PasswordSalt = "salt-Login1!",
                IsActive = true,
                IsEmailVerified = true
            };

            _userRepo.Setup(x => x.GetByEmailWithSessionAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act
            var resp = await svc.LoginAsync(email, password, CancellationToken.None);

            // Assert
            user.LastLoginAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
            user.LastLoginIp.Should().Be("127.0.0.1");
        }

        /// <summary>
        /// Creates a new session if the user's session is null during login.
        /// </summary>
        [Fact]
        public async Task LoginAsync_CreatesNewSession_WhenSessionIsNull()
        {
            // Arrange
            var email = "nosession@test.com";
            var password = "Session1!";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Username = "nosession",
                PasswordHash = "hash-Session1!",
                PasswordSalt = "salt-Session1!",
                IsActive = true,
                IsEmailVerified = true,
                Session = null
            };

            _userRepo.Setup(x => x.GetByEmailWithSessionAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act
            var resp = await svc.LoginAsync(email, password, CancellationToken.None);

            // Assert
            user.Session.Should().NotBeNull();
            user.Session!.RefreshTokenHash.Should().NotBeNullOrWhiteSpace();
            user.Session.RefreshTokenSalt.Should().NotBeNullOrWhiteSpace();
            user.Session.IsOnline.Should().BeTrue();
        }

        /// <summary>
        /// Updates existing session instance instead of replacing it when logging in.
        /// </summary>
        [Fact]
        public async Task LoginAsync_UpdatesExistingSession_WhenSessionExists()
        {
            // Arrange
            var email = "withsession@test.com";
            var password = "SessionUp1!";
            var existingSession = new UserSession
            {
                RefreshTokenHash = "old-hash",
                RefreshTokenSalt = "old-salt",
                RefreshTokenExpiry = DateTime.UtcNow.AddDays(1),
                IsOnline = false,
                LastActiveAt = DateTime.UtcNow.AddDays(-1)
            };

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Username = "withsession",
                PasswordHash = "hash-SessionUp1!",
                PasswordSalt = "salt-SessionUp1!",
                IsActive = true,
                IsEmailVerified = true,
                Session = existingSession
            };

            _userRepo.Setup(x => x.GetByEmailWithSessionAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act
            var resp = await svc.LoginAsync(email, password, CancellationToken.None);

            // Assert
            user.Session.Should().NotBeNull();
            ReferenceEquals(user.Session, existingSession).Should().BeTrue();
            user.Session!.RefreshTokenHash.Should().NotBe("old-hash");
            user.Session.RefreshTokenSalt.Should().NotBe("old-salt");
        }

        /// <summary>
        /// Throws when refresh token hash verification fails during refresh.
        /// </summary>
        [Fact]
        public async Task RefreshTokenAsync_InvalidHashVerification_ThrowsInvalidRefreshToken()
        {
            // Arrange
            var token = "some-refresh";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "rt_invalid@test.com",
                Session = new UserSession
                {
                    RefreshTokenHash = "hash1",
                    RefreshTokenSalt = "salt1",
                    RefreshTokenExpiry = DateTime.UtcNow.AddDays(1)
                }
            };

            _userRepo.Setup(x => x.GetByRefreshTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _mockHashingService.Setup(h => h.VerifyAsync(token, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act
            Func<Task> act = async () => await svc.RefreshTokenAsync(token, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Authentication.InvalidRefreshToken + "*");
        }

        /// <summary>
        /// Throws when the user returned for a refresh token has no session.
        /// </summary>
        [Fact]
        public async Task RefreshTokenAsync_NullSession_ThrowsInvalidRefreshToken()
        {
            // Arrange
            var token = "no-session-token";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "no_session@test.com",
                Session = null
            };

            _userRepo.Setup(x => x.GetByRefreshTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act
            Func<Task> act = async () => await svc.RefreshTokenAsync(token, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Authentication.InvalidRefreshToken + "*");
        }

        /// <summary>
        /// Updates session last active timestamp on successful refresh.
        /// </summary>
        [Fact]
        public async Task RefreshTokenAsync_UpdatesLastActiveAt_OnSuccess()
        {
            // Arrange
            var token = "refresh-ok";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "rt_ok@test.com",
                Username = "rtok",
                Session = new UserSession
                {
                    RefreshTokenHash = "hash-rt",
                    RefreshTokenSalt = "salt-rt",
                    RefreshTokenExpiry = DateTime.UtcNow.AddDays(1),
                    LastActiveAt = DateTime.UtcNow.AddDays(-1)
                }
            };

            _userRepo.Setup(x => x.GetByRefreshTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _mockHashingService.Setup(h => h.VerifyAsync(token, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _mockHashingService.Setup(h => h.HashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string s, CancellationToken ct) => new HashedValue("newhash-"+s, "newsalt-"+s));
            _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var svc = new AuthService(_userRepo.Object, _uow.Object, CreateJwtService(), _userService.Object, _mapper.Object, Options.Create(_jwtSettings), _mockClientInfo.Object, _mockHashingService.Object, _mockDateTime.Object, _dapperUserRepo.Object, _logger.Object, _backgroundDispatcher.Object);

            // Act
            var resp = await svc.RefreshTokenAsync(token, CancellationToken.None);

            // Assert
            user.Session!.LastActiveAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
        }

        private class TestJwtTokenService : IJwtTokenService
        {
            public string GenerateAccessToken(Guid userId, string email, string username, int expiryMinutes, out DateTime expiresAt)
            {
                expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);
                return $"access-{userId}";
            }

            public string GenerateRefreshToken()
            {
                return Guid.NewGuid().ToString("N");
            }

            public System.Security.Claims.ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true)
            {
                return null;
            }
        }
    }
}

