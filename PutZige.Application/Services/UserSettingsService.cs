#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PutZige.Application.Interfaces;
using PutZige.Application.DTOs.Users;
using PutZige.Application.Common.Constants;
using PutZige.Application.Common;
using PutZige.Domain.Interfaces;
using PutZige.Domain.Entities;

namespace PutZige.Application.Services
{
    public class UserSettingsService : IUserSettingsService
    {
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<UserSettingsService> _logger;

        public UserSettingsService(
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            IDateTimeProvider dateTimeProvider,
            ILogger<UserSettingsService> logger)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<UserPreferencesDto> UpdatePreferencesAsync(UpdatePreferencesRequest request, CancellationToken cancellationToken = default)
        {
            if (request?.Preferences == null)
                throw new AppException(ResponseCodes.VALIDATION_FAILED, "preferences is required");

            var userId = _currentUserService.GetUserId();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken, u => u.Settings).ConfigureAwait(false);
            if (user == null)
                throw new AppException(ResponseCodes.NOT_FOUND, "User not found");

            var existing = user.Settings?.Preferences ?? new UserPreferences();

            var merged = new UserPreferences(
                request.Preferences.TimeZoneId ?? existing.TimeZoneId,
                request.Preferences.Theme ?? existing.Theme,
                request.Preferences.Language ?? existing.Language,
                request.Preferences.IsDarkMode ?? existing.IsDarkMode
            );

            if (user.Settings == null)
            {
                user.Settings = new UserSettings
                {
                    UserId = user.Id,
                    Preferences = merged
                };
            }
            else
            {
                user.Settings.Preferences = merged;
            }

            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("User preferences updated - UserId: {UserId} TimeZoneId: {TimeZoneId} Theme: {Theme} Language: {Language} IsDarkMode: {IsDarkMode}",
                userId, merged.TimeZoneId, merged.Theme, merged.Language, merged.IsDarkMode);

            return new UserPreferencesDto
            {
                TimeZoneId = merged.TimeZoneId,
                Theme = merged.Theme,
                Language = merged.Language,
                IsDarkMode = merged.IsDarkMode
            };
        }

        public async Task<UserPreferencesDto> GetPreferencesAsync(CancellationToken cancellationToken = default)
        {
            var userId = _currentUserService.GetUserId();
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken, u => u.Settings).ConfigureAwait(false);
            if (user == null)
                throw new AppException(ResponseCodes.NOT_FOUND, "User not found");

            var prefs = user.Settings?.Preferences ?? new UserPreferences();

            return new UserPreferencesDto
            {
                TimeZoneId = prefs.TimeZoneId,
                Theme = prefs.Theme,
                Language = prefs.Language,
                IsDarkMode = prefs.IsDarkMode
            };
        }
    }
}
