#nullable enable
using System;
using System.Linq;
using Xunit;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using PutZige.Domain.Entities;

namespace PutZige.Infrastructure.Tests.Repositories
{
    public class UserSettingsPersistenceTests : IClassFixture<DatabaseFixture>
    {
        private readonly DatabaseFixture _fixture;

        public UserSettingsPersistenceTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void SaveAndRead_UserSettings_PersistsAllFields()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var prefs = new UserPreferences("Asia/Karachi", "dark", "en");

            using (var ctx = _fixture.CreateContext())
            {
                var user = new User
                {
                    Id = userId,
                    Email = "persist@example.com",
                    Username = "persistuser",
                    PasswordHash = "h",
                    PasswordSalt = "s",
                    DisplayName = "Persist"
                };

                var settings = new UserSettings
                {
                    UserId = userId,
                    Preferences = prefs
                };

                ctx.Users.Add(user);
                ctx.UserSettings.Add(settings);
                ctx.SaveChanges();
            }

            // Act
            using (var ctx = _fixture.CreateContext())
            {
                var loaded = ctx.Users.Include(u => u.Settings).FirstOrDefault(u => u.Id == userId);

                // Assert
                loaded.Should().NotBeNull();
                loaded!.Settings.Should().NotBeNull();
                loaded.Settings!.Preferences.Should().NotBeNull();
                loaded.Settings.Preferences.TimeZoneId.Should().Be("Asia/Karachi");
                loaded.Settings.Preferences.Theme.Should().Be("dark");
                loaded.Settings.Preferences.Language.Should().Be("en");

                // Cleanup
                ctx.UserSettings.RemoveRange(ctx.UserSettings.Where(s => s.UserId == userId));
                var u = ctx.Users.FirstOrDefault(u2 => u2.Id == userId);
                if (u != null) ctx.Users.Remove(u);
                ctx.SaveChanges();
            }
        }

        [Fact]
        public void Update_TimeZoneId_IsPersisted()
        {
            // Arrange
            var userId = Guid.NewGuid();
            using (var ctx = _fixture.CreateContext())
            {
                var user = new User { Id = userId, Email = "up@example.com", Username = "upuser", PasswordHash = "h", PasswordSalt = "s" };
                var settings = new UserSettings { UserId = userId, Preferences = new UserPreferences("UTC", "system", "en") };
                ctx.Users.Add(user);
                ctx.UserSettings.Add(settings);
                ctx.SaveChanges();
            }

            // Act
            using (var ctx = _fixture.CreateContext())
            {
                var existing = ctx.UserSettings.FirstOrDefault(s => s.UserId == userId);
                existing.Should().NotBeNull();
                existing!.Preferences = new UserPreferences("Europe/London", existing.Preferences.Theme, existing.Preferences.Language);
                ctx.UserSettings.Update(existing);
                ctx.SaveChanges();
            }

            // Assert
            using (var ctx = _fixture.CreateContext())
            {
                var loaded = ctx.UserSettings.FirstOrDefault(s => s.UserId == userId);
                loaded.Should().NotBeNull();
                loaded!.Preferences.TimeZoneId.Should().Be("Europe/London");

                // Cleanup
                ctx.UserSettings.RemoveRange(ctx.UserSettings.Where(s => s.UserId == userId));
                var u = ctx.Users.FirstOrDefault(u2 => u2.Id == userId);
                if (u != null) ctx.Users.Remove(u);
                ctx.SaveChanges();
            }
        }

        [Fact]
        public void Save_NullPreferences_DoesNotThrow_AndDefaultsOnRead()
        {
            // Arrange
            var userId = Guid.NewGuid();
            using (var ctx = _fixture.CreateContext())
            {
                var user = new User { Id = userId, Email = "null@example.com", Username = "nulluser", PasswordHash = "h", PasswordSalt = "s" };
                var settings = new UserSettings { UserId = userId, Preferences = null! };
                ctx.Users.Add(user);
                ctx.UserSettings.Add(settings);
                ctx.SaveChanges();
            }

            // Act & Assert
            using (var ctx = _fixture.CreateContext())
            {
                var loaded = ctx.UserSettings.Include(s => s.User).FirstOrDefault(s => s.UserId == userId);
                loaded.Should().NotBeNull();
                // If provider stored null or "null" JSON, converter should coalesce to default
                loaded!.Preferences.Should().NotBeNull();
                loaded.Preferences.TimeZoneId.Should().NotBeNullOrWhiteSpace();

                // Cleanup
                ctx.UserSettings.RemoveRange(ctx.UserSettings.Where(s => s.UserId == userId));
                var u = ctx.Users.FirstOrDefault(u2 => u2.Id == userId);
                if (u != null) ctx.Users.Remove(u);
                ctx.SaveChanges();
            }
        }

        [Fact]
        public void MultipleUsers_NoCrossContamination()
        {
            // Arrange
            var userA = Guid.NewGuid();
            var userB = Guid.NewGuid();

            using (var ctx = _fixture.CreateContext())
            {
                ctx.Users.Add(new User { Id = userA, Email = "a@example.com", Username = "usera", PasswordHash = "h", PasswordSalt = "s" });
                ctx.UserSettings.Add(new UserSettings { UserId = userA, Preferences = new UserPreferences("Asia/Karachi", "dark", "en") });

                ctx.Users.Add(new User { Id = userB, Email = "b@example.com", Username = "userb", PasswordHash = "h", PasswordSalt = "s" });
                ctx.UserSettings.Add(new UserSettings { UserId = userB, Preferences = new UserPreferences("Europe/Paris", "light", "fr") });

                ctx.SaveChanges();
            }

            // Act & Assert
            using (var ctx = _fixture.CreateContext())
            {
                var a = ctx.UserSettings.Include(s => s.User).FirstOrDefault(s => s.UserId == userA);
                var b = ctx.UserSettings.Include(s => s.User).FirstOrDefault(s => s.UserId == userB);

                a.Should().NotBeNull();
                b.Should().NotBeNull();

                a!.Preferences.TimeZoneId.Should().Be("Asia/Karachi");
                b!.Preferences.TimeZoneId.Should().Be("Europe/Paris");

                // Cleanup
                ctx.UserSettings.RemoveRange(ctx.UserSettings.Where(s => s.UserId == userA || s.UserId == userB));
                var users = ctx.Users.Where(u => u.Id == userA || u.Id == userB).ToList();
                ctx.Users.RemoveRange(users);
                ctx.SaveChanges();
            }
        }
    }
}
