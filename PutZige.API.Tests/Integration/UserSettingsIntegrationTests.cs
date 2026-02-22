#nullable enable
using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PutZige.API.Tests.Integration;
using PutZige.Application.DTOs.Common;
using PutZige.Application.DTOs.Users;
using PutZige.Infrastructure.Data;
using PutZige.Domain.Entities;
using Xunit;

namespace PutZige.API.Tests.Integration
{
    public class UserSettingsIntegrationTests : IntegrationTestBase
    {
        [Fact]
        public async Task PatchPreferences_Then_GetPreferences_ReturnsUpdatedValues()
        {
            // Arrange - create user directly in DB
            var userId = Guid.NewGuid();
            using (var scope = Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = new User
                {
                    Id = userId,
                    Email = "prefsint@example.com",
                    Username = "prefsint",
                    PasswordHash = "h",
                    PasswordSalt = "s",
                    DisplayName = "Prefs Int"
                };
                await db.Users.AddAsync(user);
                await db.SaveChangesAsync();
            }

            var patch = new
            {
                preferences = new
                {
                    timeZoneId = "Asia/Karachi",
                    theme = "dark",
                    language = "en"
                }
            };

            Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userId.ToString());

            // Act - PATCH
            var patchRes = await Client.PatchAsJsonAsync(TestApiEndpoints.UserSettingsPreferences, patch);
            patchRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var patchPayload = await patchRes.Content.ReadFromJsonAsync<ApiResponse<UserPreferencesDto>>();
            patchPayload.Should().NotBeNull();
            patchPayload!.IsSuccess.Should().BeTrue();
            patchPayload.Data.Should().NotBeNull();
            patchPayload.Data!.Theme.Should().Be("dark");
            patchPayload.Data.TimeZoneId.Should().Be("Asia/Karachi");
            patchPayload.Data.Language.Should().Be("en");

            // Act - GET
            var getRes = await Client.GetAsync(TestApiEndpoints.UserSettingsPreferences);
            getRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var getPayload = await getRes.Content.ReadFromJsonAsync<ApiResponse<UserPreferencesDto>>();
            getPayload.Should().NotBeNull();
            getPayload!.IsSuccess.Should().BeTrue();
            getPayload.Data.Should().NotBeNull();
            getPayload.Data!.Theme.Should().Be("dark");
            getPayload.Data.TimeZoneId.Should().Be("Asia/Karachi");
            getPayload.Data.Language.Should().Be("en");
        }
    }
}
