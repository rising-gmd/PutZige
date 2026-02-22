#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using PutZige.Application.Services;
using PutZige.Application.Interfaces;
using PutZige.Domain.Interfaces;
using PutZige.Domain.Entities;
using PutZige.Application.DTOs.Users;

namespace PutZige.Application.Tests.Services
{
    public class UserSettingsServiceTests
    {
        private readonly Mock<IUserRepository> _userRepository = new();
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<ICurrentUserService> _currentUserService = new();
        private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();
        private readonly Mock<ILogger<UserSettingsService>> _logger = new();

        private readonly UserSettingsService _service;

        public UserSettingsServiceTests()
        {
            _service = new UserSettingsService(
                _userRepository.Object,
                _unitOfWork.Object,
                _currentUserService.Object,
                _dateTimeProvider.Object,
                _logger.Object);
        }

        [Fact]
        public async Task UpdatePreferencesAsync_ThrowsNotFound_WhenUserMissing()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _currentUserService.Setup(s => s.GetUserId()).Returns(userId);

            _userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>() ))
                .ReturnsAsync((User?)null);

            var request = new UpdatePreferencesRequest
            {
                Preferences = new UserPreferencesPatchDto { Theme = "dark" }
            };

            // Act
            Func<Task> act = async () => await _service.UpdatePreferencesAsync(request, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().Where(ex => ex.ResponseCode == PutZige.Application.Common.Constants.ResponseCodes.NOT_FOUND);
        }

        [Fact]
        public async Task UpdatePreferencesAsync_ThrowsValidationFailed_WhenPreferencesNull()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _currentUserService.Setup(s => s.GetUserId()).Returns(userId);

            var user = new User { Id = userId, Settings = new UserSettings { UserId = userId, Preferences = new UserPreferences() } };
            _userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>() ))
                .ReturnsAsync(user);

            UpdatePreferencesRequest? request = new UpdatePreferencesRequest { Preferences = null };

            // Act
            Func<Task> act = async () => await _service.UpdatePreferencesAsync(request!, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<PutZige.Application.Common.AppException>().Where(ex => ex.ResponseCode == PutZige.Application.Common.Constants.ResponseCodes.VALIDATION_FAILED);
        }

        [Fact]
        public async Task UpdatePreferencesAsync_CreatesSettings_WhenSettingsNull()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _currentUserService.Setup(s => s.GetUserId()).Returns(userId);

            var user = new User { Id = userId, Settings = null };
            _userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>() ))
                .ReturnsAsync(user);

            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var request = new UpdatePreferencesRequest
            {
                Preferences = new UserPreferencesPatchDto
                {
                    TimeZoneId = "Asia/Karachi",
                    Language = "fr"
                }
            };

            // Act
            var result = await _service.UpdatePreferencesAsync(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.TimeZoneId.Should().Be("Asia/Karachi");
            result.Language.Should().Be("fr");
            result.Theme.Should().Be("system"); // default

            _userRepository.Verify(r => r.Update(It.Is<User>(u => u.Settings != null && u.Settings.Preferences.TimeZoneId == "Asia/Karachi" && u.Settings.Preferences.Language == "fr")), Times.Once);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdatePreferencesAsync_Merges_MultipleFieldsCorrectly()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _currentUserService.Setup(s => s.GetUserId()).Returns(userId);

            var existingPrefs = new UserPreferences("UTC", "light", "en");
            var settings = new UserSettings { UserId = userId, Preferences = existingPrefs };
            var user = new User { Id = userId, Settings = settings };

            _userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>() ))
                .ReturnsAsync(user);

            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var request = new UpdatePreferencesRequest
            {
                Preferences = new UserPreferencesPatchDto
                {
                    TimeZoneId = "Europe/Paris",
                    Language = "de"
                }
            };

            // Act
            var result = await _service.UpdatePreferencesAsync(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.TimeZoneId.Should().Be("Europe/Paris");
            result.Language.Should().Be("de");
            result.Theme.Should().Be("light");
        }

        [Fact]
        public async Task UpdatePreferencesAsync_MergesWithExistingPreferences()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _currentUserService.Setup(s => s.GetUserId()).Returns(userId);

            var existingPrefs = new UserPreferences("UTC", "system", "en");
            var settings = new UserSettings { UserId = userId, Preferences = existingPrefs };
            var user = new User { Id = userId, Settings = settings };

            _userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
                .ReturnsAsync(user);

            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var request = new UpdatePreferencesRequest
            {
                Preferences = new UserPreferencesPatchDto
                {
                    Theme = "dark"
                }
            };

            // Act
            var result = await _service.UpdatePreferencesAsync(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Theme.Should().Be("dark");
            result.TimeZoneId.Should().Be("UTC");
            result.Language.Should().Be("en");

            _userRepository.Verify(r => r.Update(It.Is<User>(u => u.Settings != null && u.Settings.Preferences.Theme == "dark")), Times.Once);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetPreferencesAsync_ReturnsDefaultsWhenNoSettings()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _currentUserService.Setup(s => s.GetUserId()).Returns(userId);

            var user = new User { Id = userId, Settings = null };
            _userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
                .ReturnsAsync(user);

            // Act
            var result = await _service.GetPreferencesAsync(CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.TimeZoneId.Should().Be("UTC");
            result.Theme.Should().Be("system");
            result.Language.Should().Be("en");
        }
    }
}
