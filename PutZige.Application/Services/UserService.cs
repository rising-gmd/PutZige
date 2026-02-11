#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging;
using PutZige.Application.Common;
using PutZige.Application.Common.Constants;
using PutZige.Application.Common.Messages;
using PutZige.Application.Common.Users;
using PutZige.Application.DTOs.Auth;
using PutZige.Application.DTOs.Common;
using PutZige.Application.Interfaces;
using PutZige.Domain.Entities;
using PutZige.Domain.Interfaces;

namespace PutZige.Application.Services
{
    /// <summary>
    /// Service for user registration and management.
    /// </summary>
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UserService>? _logger;
        private readonly IMapper _mapper;
        private readonly IHashingService _hashingService;
        private readonly IBackgroundJobDispatcher _backgroundJobDispatcher;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ICurrentUserService _currentUserService;
        private readonly IDapperUserRepository? _dapperUserRepository;

        public UserService(
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IHashingService hashingService,
            IDateTimeProvider dateTimeProvider,
            ICurrentUserService? currentUserService = null,
            IDapperUserRepository? dapperUserRepository = null,
            IBackgroundJobDispatcher? backgroundJobDispatcher = null,
            ILogger<UserService>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(userRepository);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(hashingService);
            ArgumentNullException.ThrowIfNull(dateTimeProvider);

            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _hashingService = hashingService;
            _backgroundJobDispatcher = backgroundJobDispatcher ?? new NoOpBackgroundJobDispatcher();
            _dateTimeProvider = dateTimeProvider;
            _currentUserService = currentUserService ?? new NoOpCurrentUserService();
            _dapperUserRepository = dapperUserRepository;
        }

        /// <summary>
        /// Searches for users by query string with validation and mapping.
        /// </summary>
        public async Task<UserSearchResponse> SearchUsersAsync(string query, CancellationToken ct = default)
        {
            // Validation logic moved from controller
            if (string.IsNullOrWhiteSpace(query) || query.Length < 1)
            {
                throw new AppException(ResponseCodes.VALIDATION_FAILED, "Query parameter is required and must be at least 1 character");
            }

            var currentUserId = _currentUserService?.TryGetUserId() ?? Guid.Empty;

            if (_dapperUserRepository == null)
            {
                return new UserSearchResponse
                {
                    Users = Array.Empty<UserSearchDto>(),
                    TotalCount = 0
                };
            }

            var results = await _dapperUserRepository.SearchUsersAsync(query, currentUserId, 20, ct).ConfigureAwait(false);

            if (results == null || !results.Any())
            {
                return new UserSearchResponse
                {
                    Users = Array.Empty<UserSearchDto>(),
                    TotalCount = 0
                };
            }

            // Mapping logic moved from controller
            var userDtos = results.Select(u => new UserSearchDto
            {
                Id = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                Email = u.Email,
                JobTitle = u.JobTitle,
                Bio = u.Bio,
                ProfilePictureUrl = u.ProfilePictureUrl
            }).ToArray();

            return new UserSearchResponse
            {
                Users = userDtos,
                TotalCount = userDtos.Length
            };
        }

        /// <summary>
        /// Returns a profile DTO for the current authenticated user.
        /// </summary>
        public async Task<UserProfileResponse> GetMyProfileAsync(CancellationToken ct = default)
        {
            var userId = _currentUserService.GetUserId();

            var user = await _dapperUserRepository.GetProfileByIdAsync(userId, ct).ConfigureAwait(false);

            if (user == null)
            {
                throw new KeyNotFoundException(ErrorMessages.General.ResourceNotFound);
            }

            return new UserProfileResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                DisplayName = user.DisplayName,
                JobTitle = null,
                Bio = user.Bio,
                ProfilePictureUrl = user.ProfilePictureUrl,
                CreatedAt = user.CreatedAt,
                LastSeenAt = user.LastSeenAt
            };
        }

        /// <summary>
        /// Registers a new user with validation and hashing and returns a response DTO.
        /// </summary>
        public async Task<RegisterUserResponse> RegisterUserAsync(string email, string username, string password, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(email)) throw new AppException(ResponseCodes.EMAIL_REQUIRED, ErrorMessages.Validation.EmailRequired);
            if (string.IsNullOrWhiteSpace(username)) throw new AppException(ResponseCodes.USERNAME_REQUIRED, ErrorMessages.Validation.UsernameRequired);
            if (string.IsNullOrWhiteSpace(password)) throw new AppException(ResponseCodes.PASSWORD_REQUIRED, ErrorMessages.Validation.PasswordRequired);

            _logger?.LogInformation("User registration attempt - Email: {Email}", email);

            // Check availability
            if (await _userRepository.IsEmailTakenAsync(email, ct))
            {
                _logger?.LogWarning("Registration failed - Email already exists: {Email}", email);
                throw new AppException(ResponseCodes.EMAIL_ALREADY_EXISTS, ErrorMessages.Authentication.EmailAlreadyTaken);
            }

            if (await _userRepository.IsUsernameTakenAsync(username, ct))
            {
                _logger?.LogWarning("Registration failed - Username already exists: {Username}", username);
                throw new AppException(ResponseCodes.USERNAME_TAKEN, ErrorMessages.Authentication.UsernameAlreadyTaken);
            }

            // Create a cryptographically secure composite verification token that embeds email
            var token = _hashingService.GenerateEmailVerificationToken(email, 32);

            // Hash password
            var hashed = await _hashingService.HashAsync(password, ct);

            var user = new User
            {
                Email = email,
                Username = username,
                PasswordHash = hashed.Hash,
                PasswordSalt = hashed.Salt,
                EmailVerificationToken = token,
                EmailVerificationTokenExpiry = _dateTimeProvider.UtcNow.AddDays(AppConstants.Security.EmailVerificationTokenExpirationDays),
                IsEmailVerified = false,
                CreatedAt = _dateTimeProvider.UtcNow
            };

            _logger?.LogInformation("Creating user entity - Email: {Email}", email);

            await _userRepository.AddAsync(user, ct);

            _logger?.LogInformation("Saving changes to database");

            await _unitOfWork.SaveChangesAsync(ct);

            _logger?.LogInformation("User entity created - UserId: {UserId}", user.Id);

            // Queue verification email (non-blocking)
            try
            {
                _backgroundJobDispatcher.EnqueueVerificationEmail(user.Email, user.Username, user.EmailVerificationToken!);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to enqueue verification email for user {Email}", user.Email);
            }

            // Map to response DTO
            var response = _mapper.Map<RegisterUserResponse>(user);

            return response;
        }

        /// <summary>
        /// Gets a user by email.
        /// </summary>
        public async Task<RegisterUserResponse?> GetUserByEmailAsync(string email, CancellationToken ct = default)
        {
            var user = await _userRepository.GetByEmailAsync(email, ct);
            return user == null ? null : _mapper.Map<RegisterUserResponse>(user);
        }

        /// <summary>
        /// Soft deletes a user by id.
        /// </summary>
        public async Task SoftDeleteUserAsync(Guid userId, CancellationToken ct = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, ct);
            if (user == null) throw new KeyNotFoundException(ErrorMessages.General.ResourceNotFound);
            _userRepository.Delete(user);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Updates user's last login info and session details.
        /// </summary>
        public async Task UpdateLoginInfoAsync(Guid userId, string? ipAddress, string refreshToken, DateTime refreshTokenExpiry, CancellationToken ct = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, ct);
            if (user == null) throw new KeyNotFoundException(ErrorMessages.General.ResourceNotFound);

            user.LastLoginAt = _dateTimeProvider.UtcNow;
            user.LastLoginIp = ipAddress;
            user.FailedLoginAttempts = 0;

            // Hash refresh token before storage
            var hashed = await _hashingService.HashAsync(refreshToken, ct);

            if (user.Session == null)
            {
                user.Session = new UserSession
                {
                    UserId = user.Id,
                    RefreshTokenHash = hashed.Hash,
                    RefreshTokenSalt = hashed.Salt,
                    RefreshTokenExpiry = refreshTokenExpiry,
                    IsOnline = true,
                    LastActiveAt = _dateTimeProvider.UtcNow
                };
            }
            else
            {
                user.Session.RefreshTokenHash = hashed.Hash;
                user.Session.RefreshTokenSalt = hashed.Salt;
                user.Session.RefreshTokenExpiry = refreshTokenExpiry;
                user.Session.IsOnline = true;
                user.Session.LastActiveAt = _dateTimeProvider.UtcNow;
            }

            await _unitOfWork.SaveChangesAsync(ct);
        }

        private sealed class NoOpCurrentUserService : ICurrentUserService
        {
            public Guid GetUserId() => Guid.Empty;
            public Guid? TryGetUserId() => null;
            public string? GetUserEmail() => null;
            public string? GetUserName() => null;
            public bool IsAuthenticated() => false;
        }
    }
}