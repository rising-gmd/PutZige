#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using Bogus;
using AutoMapper;
using Microsoft.Extensions.Logging;
using PutZige.Application.Services;
using PutZige.Application.Interfaces;
using PutZige.Application.DTOs;
using PutZige.Application.DTOs.Auth;
using PutZige.Domain.Interfaces;
using PutZige.Domain.Entities;
using PutZige.Application.Common.Messages;
using PutZige.Application.Common.Constants;

namespace PutZige.Application.Tests.Services
{
    public class UserServiceTests : IDisposable
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ILogger<UserService>> _mockLogger;
        private readonly Mock<IHashingService> _mockHashingService;
        private readonly UserService _sut;
        private readonly Faker _faker;
        private readonly Mock<IDateTimeProvider> _mockDateTime;
        private readonly DateTime _fixedNow;
        private readonly CancellationToken _ct = CancellationToken.None;

        public UserServiceTests()
        {
            _mockUserRepository = new Mock<IUserRepository>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockMapper = new Mock<IMapper>();
            _mockLogger = new Mock<ILogger<UserService>>();
            _mockHashingService = new Mock<IHashingService>();
            _faker = new Faker();

            _fixedNow = DateTime.UtcNow;
            _mockDateTime = new Mock<IDateTimeProvider>();
            _mockDateTime.Setup(d => d.UtcNow).Returns(() => _fixedNow);

            _mockHashingService.Setup(h => h.GenerateSecureToken(It.IsAny<int>())).Returns(() => Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("+","-").Replace("/","_").TrimEnd('='));
            _mockHashingService.Setup(h => h.HashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string s, CancellationToken ct) => new HashedValue("hash-"+s, "salt-"+s));

            // Initialize system under test
            _sut = new UserService(
                _mockUserRepository.Object,
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockHashingService.Object,
                _mockDateTime.Object,
                backgroundJobDispatcher: null,
                logger: _mockLogger.Object);
        }

        public void Dispose()
        {
            // No unmanaged resources to dispose; mocks will be GC'ed
        }

        /// <summary>
        /// Registers a user with valid input and returns a mapped RegisterUserResponse.
        /// </summary>
        [Fact]
        public async Task RegisterUserAsync_ValidData_ReturnsUserResponse()
        {
            // Arrange
            var email = _faker.Internet.Email();
            var username = _faker.Internet.UserName();
            var password = _faker.Internet.Password(12);

            _mockUserRepository
                .Setup(x => x.IsEmailTakenAsync(email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _mockUserRepository
                .Setup(x => x.IsUsernameTakenAsync(username, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Capture the user passed to AddAsync for later assertions
            User? capturedUser = null;
            _mockUserRepository
                .Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .Callback<User, CancellationToken>((u, ct) => capturedUser = u)
                .Returns(Task.CompletedTask);

            _mockUnitOfWork
                .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _mockMapper
                .Setup(m => m.Map<RegisterUserResponse>(It.IsAny<User>()))
                .Returns((User u) => new RegisterUserResponse
                {
                    UserId = u.Id,
                    Email = u.Email ?? string.Empty,
                    Username = u.Username ?? string.Empty,
                    IsEmailVerified = u.IsEmailVerified,
                    CreatedAt = u.CreatedAt
                });

            // Act
            var result = await _sut.RegisterUserAsync(email, username, password, _ct);

            // Assert
            result.Should().NotBeNull();
            result.Email.Should().Be(email);
            result.Username.Should().Be(username);

            _mockUserRepository.Verify(x => x.IsEmailTakenAsync(email, It.IsAny<CancellationToken>()), Times.Once);
            _mockUserRepository.Verify(x => x.IsUsernameTakenAsync(username, It.IsAny<CancellationToken>()), Times.Once);
            _mockUserRepository.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

            capturedUser.Should().NotBeNull();
            capturedUser!.Email.Should().Be(email);
            capturedUser.Username.Should().Be(username);
            capturedUser.IsEmailVerified.Should().BeFalse();
            capturedUser.EmailVerificationToken.Should().NotBeNullOrWhiteSpace();
            capturedUser.EmailVerificationTokenExpiry.Should().BeAfter(_fixedNow);
        }

        /// <summary>
        /// Ensures password is hashed and salt stored when registering a new user.
        /// </summary>
        [Fact]
        public async Task RegisterUserAsync_ValidData_HashesPassword()
        {
            // Arrange
            var email = _faker.Internet.Email();
            var username = _faker.Internet.UserName();
            var password = _faker.Internet.Password(12);

            _mockUserRepository.Setup(x => x.IsEmailTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _mockUserRepository.Setup(x => x.IsUsernameTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            User? capturedUser = null;
            _mockUserRepository.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .Callback<User, CancellationToken>((u, ct) => capturedUser = u)
                .Returns(Task.CompletedTask);

            _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mockMapper.Setup(m => m.Map<RegisterUserResponse>(It.IsAny<User>())).Returns((User u) => new RegisterUserResponse
            {
                UserId = u.Id,
                Email = u.Email ?? string.Empty,
                Username = u.Username ?? string.Empty,
                IsEmailVerified = u.IsEmailVerified,
                CreatedAt = u.CreatedAt
            });

            // Act
            var result = await _sut.RegisterUserAsync(email, username, password, _ct);

            // Assert
            capturedUser.Should().NotBeNull();
            capturedUser!.PasswordHash.Should().NotBeNullOrWhiteSpace();
            capturedUser.PasswordHash.Should().Be("hash-" + password);
            capturedUser.PasswordSalt.Should().Be("salt-" + password);
        }

        /// <summary>
        /// Generates a URL-safe email verification token and sets expiry when registering.
        /// </summary>
        [Fact]
        public async Task RegisterUserAsync_ValidData_GeneratesVerificationToken()
        {
            // Arrange
            var email = _faker.Internet.Email();
            var username = _faker.Internet.UserName();
            var password = _faker.Internet.Password(12);

            _mockUserRepository.Setup(x => x.IsEmailTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _mockUserRepository.Setup(x => x.IsUsernameTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            User? capturedUser = null;
            _mockUserRepository.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .Callback<User, CancellationToken>((u, ct) => capturedUser = u)
                .Returns(Task.CompletedTask);

            _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mockMapper.Setup(m => m.Map<RegisterUserResponse>(It.IsAny<User>())).Returns((User u) => new RegisterUserResponse
            {
                UserId = u.Id,
                Email = u.Email ?? string.Empty,
                Username = u.Username ?? string.Empty,
                IsEmailVerified = u.IsEmailVerified,
                CreatedAt = u.CreatedAt
            });

            // Act
            var result = await _sut.RegisterUserAsync(email, username, password, _ct);

            // Assert
            capturedUser.Should().NotBeNull();
            capturedUser!.EmailVerificationToken.Should().NotBeNullOrWhiteSpace();
            // Token should be URL-safe base64 (no '+' or '/') and not end with '=' padding
            capturedUser.EmailVerificationToken.Should().NotContain("+");
            capturedUser.EmailVerificationToken.Should().NotContain("/");
            capturedUser.EmailVerificationToken.Should().NotEndWith("=");

            // Expiry approximately 7 days from now (allow small tolerance)
            var expectedExpiry = _fixedNow.AddDays(AppConstants.Security.EmailVerificationTokenExpirationDays);
            capturedUser.EmailVerificationTokenExpiry.Should().BeCloseTo(expectedExpiry, precision: TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Persists a newly registered user to the repository and commits the unit of work.
        /// </summary>
        [Fact]
        public async Task RegisterUserAsync_ValidData_SavesUserToRepository()
        {
            // Arrange
            var email = _faker.Internet.Email();
            var username = _faker.Internet.UserName();
            var password = _faker.Internet.Password(12);

            _mockUserRepository.Setup(x => x.IsEmailTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _mockUserRepository.Setup(x => x.IsUsernameTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            _mockUserRepository.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mockMapper.Setup(m => m.Map<RegisterUserResponse>(It.IsAny<User>())).Returns((User u) => new RegisterUserResponse
            {
                UserId = u.Id,
                Email = u.Email ?? string.Empty,
                Username = u.Username ?? string.Empty,
                IsEmailVerified = u.IsEmailVerified,
                CreatedAt = u.CreatedAt
            });

            // Act
            var result = await _sut.RegisterUserAsync(email, username, password, _ct);

            // Assert
            _mockUserRepository.Verify(x => x.AddAsync(It.Is<User>(u => u.Email == email && u.Username == username), It.IsAny<CancellationToken>()), Times.Once);
            _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Maps created User to RegisterUserResponse DTO using AutoMapper.
        /// </summary>
        [Fact]
        public async Task RegisterUserAsync_ValidData_MapsToResponseDto()
        {
            // Arrange
            var email = _faker.Internet.Email();
            var username = _faker.Internet.UserName();
            var password = _faker.Internet.Password(12);

            _mockUserRepository.Setup(x => x.IsEmailTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _mockUserRepository.Setup(x => x.IsUsernameTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            _mockUserRepository.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var responseReturned = new RegisterUserResponse
            {
                UserId = Guid.NewGuid(),
                Email = email,
                Username = username,
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow
            };

            _mockMapper.Setup(m => m.Map<RegisterUserResponse>(It.IsAny<User>())).Returns(responseReturned);

            // Act
            var result = await _sut.RegisterUserAsync(email, username, password, _ct);

            // Assert
            _mockMapper.Verify(m => m.Map<RegisterUserResponse>(It.IsAny<User>()), Times.Once);
            result.Should().NotBeNull();
            result.Email.Should().Be(responseReturned.Email);
            result.Username.Should().Be(responseReturned.Username);
            result.UserId.Should().Be(responseReturned.UserId);
        }

        /// <summary>
        /// Throws InvalidOperationException when attempting to register with an email that already exists.
        /// </summary>
        [Fact]
        public async Task RegisterUserAsync_DuplicateEmail_ThrowsInvalidOperationException()
        {
            // Arrange
            var email = _faker.Internet.Email();
            var username = _faker.Internet.UserName();
            var password = _faker.Internet.Password(12);

            _mockUserRepository.Setup(x => x.IsEmailTakenAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            Func<Task> act = async () => await _sut.RegisterUserAsync(email, username, password, _ct);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Authentication.EmailAlreadyTaken);

            _mockUserRepository.Verify(x => x.IsEmailTakenAsync(email, It.IsAny<CancellationToken>()), Times.Once);
            _mockUserRepository.Verify(x => x.IsUsernameTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockUserRepository.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>
        /// Throws InvalidOperationException when attempting to register with a username that is already taken.
        /// </summary>
        [Fact]
        public async Task RegisterUserAsync_DuplicateUsername_ThrowsInvalidOperationException()
        {
            // Arrange
            var email = _faker.Internet.Email();
            var username = _faker.Internet.UserName();
            var password = _faker.Internet.Password(12);

            _mockUserRepository.Setup(x => x.IsEmailTakenAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _mockUserRepository.Setup(x => x.IsUsernameTakenAsync(username, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            Func<Task> act = async () => await _sut.RegisterUserAsync(email, username, password, _ct);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Authentication.UsernameAlreadyTaken);

            _mockUserRepository.Verify(x => x.IsEmailTakenAsync(email, It.IsAny<CancellationToken>()), Times.Once);
            _mockUserRepository.Verify(x => x.IsUsernameTakenAsync(username, It.IsAny<CancellationToken>()), Times.Once);
            _mockUserRepository.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>
        /// Throws ArgumentException when email parameter is null or empty.
        /// </summary>
        [Fact]
        public async Task RegisterUserAsync_NullEmail_ThrowsArgumentException()
        {
            // Arrange
            string? email = null;
            var username = _faker.Internet.UserName();
            var password = _faker.Internet.Password(12);

            // Act
            Func<Task> act = async () => await _sut.RegisterUserAsync(email!, username, password, _ct);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Validation.EmailRequired + "*");

            _mockUserRepository.Verify(x => x.IsEmailTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>
        /// Throws ArgumentException when username is empty.
        /// </summary>
        [Fact]
        public async Task RegisterUserAsync_EmptyUsername_ThrowsArgumentException()
        {
            // Arrange
            var email = _faker.Internet.Email();
            var username = "";
            var displayName = _faker.Person.FullName;
            var password = _faker.Internet.Password(12);

            // Act
            Func<Task> act = async () => await _sut.RegisterUserAsync(email, username, password, _ct);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Validation.UsernameRequired + "*");

            _mockUserRepository.Verify(x => x.IsUsernameTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>
        /// Throws ArgumentException when password is null or empty.
        /// </summary>
        [Fact]
        public async Task RegisterUserAsync_NullPassword_ThrowsArgumentException()
        {
            // Arrange
            var email = _faker.Internet.Email();
            var username = _faker.Internet.UserName();
            string? password = null;
    
            // Act
            Func<Task> act = async () => await _sut.RegisterUserAsync(email, username, password!, _ct);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().WithMessage(ErrorMessages.Validation.PasswordRequired + "*");

            _mockUserRepository.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>
        /// Verifies that UpdateLoginInfoAsync hashes the refresh token and stores both hash and salt on the session.
        /// </summary>
        [Fact]
        public async Task UpdateLoginInfoAsync_HashesRefreshToken_StoresBothHashAndSalt()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "existing@example.com",
                Username = "existing",
                DisplayName = "Existing User"
            };

            var refreshToken = "refresh-token-xyz";
            var expiry = _fixedNow.AddDays(7);

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            _mockHashingService.Setup(h => h.HashAsync(refreshToken, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashedValue("hashed-refresh", "refresh-salt"));

            _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await _sut.UpdateLoginInfoAsync(userId, "127.0.0.1", refreshToken, expiry, _ct);

            // Assert
            _mockHashingService.Verify(h => h.HashAsync(refreshToken, It.IsAny<CancellationToken>()), Times.Once);

            user.Session.Should().NotBeNull();
            user.Session!.RefreshTokenHash.Should().Be("hashed-refresh");
            user.Session!.RefreshTokenSalt.Should().Be("refresh-salt");
            user.Session!.RefreshTokenExpiry.Should().Be(expiry);

            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Creates a new session when none exists and sets refresh token and activity details.
        /// </summary>
        [Fact]
        public async Task UpdateLoginInfoAsync_CreatesNewSession_WhenSessionNull()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "sessionnull@example.com",
                Username = "sessionnull",
                DisplayName = "Session Null",
                Session = null
            };

            var refreshToken = "new-refresh-token";
            var expiry = _fixedNow.AddDays(14);

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            _mockHashingService.Setup(h => h.HashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashedValue("rt-hash", "rt-salt"));

            _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await _sut.UpdateLoginInfoAsync(userId, "10.0.0.1", refreshToken, expiry, _ct);

            // Assert
            user.Session.Should().NotBeNull();
            user.Session!.UserId.Should().Be(user.Id);
            user.Session!.RefreshTokenHash.Should().Be("rt-hash");
            user.Session!.RefreshTokenSalt.Should().Be("rt-salt");
            user.Session!.IsOnline.Should().BeTrue();
            user.Session!.LastActiveAt.Should().Be(_fixedNow);

            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Updates fields on an existing session instance instead of replacing it.
        /// </summary>
        [Fact]
        public async Task UpdateLoginInfoAsync_UpdatesExistingSession_WhenSessionExists()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var existingSession = new UserSession
            {
                UserId = userId,
                RefreshTokenHash = "old-hash",
                RefreshTokenSalt = "old-salt",
                IsOnline = false,
                LastActiveAt = DateTime.UtcNow.AddDays(-1)
            };

            var user = new User
            {
                Id = userId,
                Email = "existingsession@example.com",
                Username = "existingsession",
                DisplayName = "Existing Session",
                Session = existingSession
            };

            var refreshToken = "another-refresh-token";
            var expiry = _fixedNow.AddDays(30);

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            _mockHashingService.Setup(h => h.HashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashedValue("new-hash", "new-salt"));

            _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await _sut.UpdateLoginInfoAsync(userId, "192.168.0.5", refreshToken, expiry, _ct);

            // Assert
            // Same instance should be updated
            ReferenceEquals(user.Session, existingSession).Should().BeTrue();

            user.Session!.RefreshTokenHash.Should().Be("new-hash");
            user.Session!.RefreshTokenSalt.Should().Be("new-salt");
            user.Session!.IsOnline.Should().BeTrue();
            user.Session!.LastActiveAt.Should().Be(_fixedNow);

            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Resets failed login attempts and updates last login timestamp and IP on successful login.
        /// </summary>
        [Fact]
        public async Task UpdateLoginInfoAsync_ResetsFailedLoginAttempts_OnSuccess()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "failures@example.com",
                Username = "failures",
                DisplayName = "Failures",
                FailedLoginAttempts = 3,
                LastFailedLoginAttempt = DateTime.UtcNow.AddHours(-2)
            };

            var refreshToken = "rt-for-reset";
            var expiry = _fixedNow.AddDays(1);
            var ip = "8.8.8.8";

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            _mockHashingService.Setup(h => h.HashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashedValue("reset-hash", "reset-salt"));

            _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await _sut.UpdateLoginInfoAsync(userId, ip, refreshToken, expiry, _ct);

            // Assert
            user.FailedLoginAttempts.Should().Be(0);
            user.LastLoginIp.Should().Be(ip);
            user.LastLoginAt.Should().Be(_fixedNow);

            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
