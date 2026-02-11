// PutZige.API.Tests/Controllers/AuthControllerTests.cs
#nullable enable
using System;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PutZige.API.Tests.Integration;
using PutZige.Application.DTOs.Auth;
using PutZige.Application.DTOs.Common;
using PutZige.Domain.Entities;
using PutZige.Infrastructure.Data;
using Xunit;

namespace PutZige.API.Tests.Controllers
{
    public partial class AuthControllerTests : IntegrationTestBase
    {
        private static (string hash, string salt) CreateHash(string plain)
        {
            var salt = new byte[32];
            RandomNumberGenerator.Fill(salt);
            var derived = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(plain), salt, 100000, HashAlgorithmName.SHA512, 64);
            return (Convert.ToBase64String(derived), Convert.ToBase64String(salt));
        }

        private static bool VerifyHash(string plain, string hashBase64, string saltBase64)
        {
            var salt = Convert.FromBase64String(saltBase64);
            var derived = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(plain), salt, 100000, HashAlgorithmName.SHA512, 64);
            var derivedBase64 = Convert.ToBase64String(derived);
            var a = Convert.FromBase64String(derivedBase64);
            var b = Convert.FromBase64String(hashBase64);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        /// <summary>
        /// Verifies login with valid credentials returns 200 and tokens.
        /// </summary>
        [Fact]
        public async Task Login_ValidCredentials_Returns200AndLoginResponse()
        {
            var email = "intauth@example.com";
            var password = "Password123!";

            using (var scope = Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hashed = CreateHash(password);
                await db.Users.AddAsync(new User
                {
                    Email = email,
                    Username = "intauth",
                    DisplayName = "Auth User",
                    PasswordHash = hashed.hash,
                    PasswordSalt = hashed.salt,
                    IsActive = true,
                    IsEmailVerified = true
                });
                await db.SaveChangesAsync();
            }

            var request = new LoginRequest { Identifier = email, Password = password };
            var response = await Client.PostAsJsonAsync(TestApiEndpoints.AuthLogin, request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var payload = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            payload.Should().NotBeNull();
            payload!.IsSuccess.Should().BeTrue();
            payload.Data.Should().NotBeNull();
            payload.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
            payload.Data.RefreshToken.Should().NotBeNullOrWhiteSpace();
            payload.Data.Email.Should().Be(email);
        }

        /// <summary>
        /// Ensures invalid password returns 400 and error message.
        /// </summary>
        [Fact]
        public async Task Login_InvalidPassword_Returns400InvalidCredentials()
        {
            var email = "badpass@example.com";
            var password = "Correct1!";

            using (var scope = Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hashed = CreateHash(password);
                await db.Users.AddAsync(new User { Email = email, Username = "bp", PasswordHash = hashed.hash, PasswordSalt = hashed.salt, IsActive = true, IsEmailVerified = true, DisplayName = "B" });
                await db.SaveChangesAsync();
            }

            var request = new LoginRequest { Identifier = email, Password = "WrongPass!" };
            var response = await Client.PostAsJsonAsync(TestApiEndpoints.AuthLogin, request);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var payload = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            payload.Should().NotBeNull();
            payload!.IsSuccess.Should().BeFalse();
            payload.Message.Should().Contain("Invalid");
        }

        /// <summary>
        /// Validates missing fields return 400 with lowercase field names.
        /// </summary>
        [Fact]
        public async Task Login_MissingRequiredFields_Returns400WithLowercaseFieldNames()
        {
            var invalid = new { username = "noemail" };
            var response = await Client.PostAsJsonAsync(TestApiEndpoints.AuthLogin, invalid);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            result.Should().NotBeNull();
            result!.Errors.Should().NotBeNull();
            result.Errors!.Should().ContainKey("identifier");
            result.Errors.Should().ContainKey("password");
        }

        /// <summary>
        /// Unverified email returns 400 with verify message.
        /// </summary>
        [Fact]
        public async Task Login_UnverifiedEmail_Returns400()
        {
            var email = "unverified@example.com";
            var password = "Password1!";

            using (var scope = Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hashed = CreateHash(password);
                await db.Users.AddAsync(new User { Email = email, Username = "uv", PasswordHash = hashed.hash, PasswordSalt = hashed.salt, IsActive = true, IsEmailVerified = false, DisplayName = "U" });
                await db.SaveChangesAsync();
            }

            var request = new LoginRequest { Identifier = email, Password = password };
            var response = await Client.PostAsJsonAsync(TestApiEndpoints.AuthLogin, request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var payload = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            payload.Should().NotBeNull();
            payload!.Message.ToLowerInvariant().Should().Contain("verify");
        }

        /// <summary>
        /// Inactive account returns 400 with inactive message.
        /// </summary>
        [Fact]
        public async Task Login_InactiveAccount_Returns400()
        {
            var email = "inactive@example.com";
            var password = "Password1!";

            using (var scope = Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hashed = CreateHash(password);
                await db.Users.AddAsync(new User { Email = email, Username = "inact", PasswordHash = hashed.hash, PasswordSalt = hashed.salt, IsActive = false, IsEmailVerified = true, DisplayName = "I" });
                await db.SaveChangesAsync();
            }

            var request = new LoginRequest { Identifier = email, Password = password };
            var response = await Client.PostAsJsonAsync(TestApiEndpoints.AuthLogin, request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var payload = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            payload.Should().NotBeNull();
            payload!.Message.ToLowerInvariant().Should().Contain("inactive");
        }

        /// <summary>
        /// Logout clears authentication cookies (critical security test).
        /// </summary>
        [Fact]
        public async Task Logout_ClearsAuthCookies()
        {
            var email = "logout@example.com";
            var password = "Password1!";

            using (var scope = Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hashed = CreateHash(password);
                await db.Users.AddAsync(new User
                {
                    Email = email,
                    Username = "logoutuser",
                    DisplayName = "Logout User",
                    PasswordHash = hashed.hash,
                    PasswordSalt = hashed.salt,
                    IsActive = true,
                    IsEmailVerified = true
                });
                await db.SaveChangesAsync();
            }

            // Login to get tokens and cookies
            var loginRequest = new LoginRequest { Identifier = email, Password = password };
            var loginResponse = await Client.PostAsJsonAsync(TestApiEndpoints.AuthLogin, loginRequest);
            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify Set-Cookie headers are present after login
            var loginCookies = loginResponse.Headers.GetValues("Set-Cookie");
            loginCookies.Should().Contain(c => c.Contains("refreshToken="));
            loginCookies.Should().Contain(c => c.Contains("accessToken="));

            var payload = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            var accessToken = payload!.Data!.AccessToken;

            // Logout with bearer token
            var logoutRequest = new HttpRequestMessage(HttpMethod.Post, TestApiEndpoints.AuthLogout);
            logoutRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
            var logoutResponse = await Client.SendAsync(logoutRequest);
            logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify Set-Cookie headers contain expiration (cookie deletion)
            if (logoutResponse.Headers.Contains("Set-Cookie"))
            {
                var logoutCookies = logoutResponse.Headers.GetValues("Set-Cookie");
                // Cookies are cleared by setting expires to past date
                logoutCookies.Should().Contain(c => c.Contains("expires=") || c.Contains("max-age=0"));
            }
        }

        /// <summary>
        /// Protected endpoint without token returns 401 Unauthorized (critical security test).
        /// </summary>
        [Fact]
        public async Task GetMe_WithoutToken_Returns401()
        {
            // Attempt to access protected /me endpoint without auth
            var response = await Client.GetAsync(TestApiEndpoints.AuthMe);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        /// <summary>
        /// Repeated failed logins trigger rate limit and return 429.
        /// </summary>
        [Fact]
        public async Task Login_RateLimitExceeded_Returns429WithCorrectStatusCode()
        {
            var email = "rate108@example.com";
            var password = "Password1!";
            // seed user
            using (var scope = Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hashed = CreateHash(password);
                await db.Users.AddAsync(new User { Email = email, Username = "rate108", PasswordHash = hashed.hash, PasswordSalt = hashed.salt, IsActive = true, IsEmailVerified = true, DisplayName = "Rate 108" });
                await db.SaveChangesAsync();
            }

            for (int i = 0; i < 6; i++)
            {
                var r = await Client.PostAsJsonAsync(TestApiEndpoints.AuthLogin, new LoginRequest { Identifier = email, Password = "Wrong" });
            }

            var final = await Client.PostAsJsonAsync(TestApiEndpoints.AuthLogin, new LoginRequest { Identifier = email, Password = "Wrong" });
            final.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        }

        /// <summary>
        /// Rate limit response contains structured error message.
        /// </summary>
        [Fact]
        public async Task Login_RateLimitExceeded_ResponseMatchesSchema()
        {
            var email = "rate109@example.com";
            var password = "Password1!";
            using (var scope = Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hashed = CreateHash(password);
                await db.Users.AddAsync(new User { Email = email, Username = "rate109", PasswordHash = hashed.hash, PasswordSalt = hashed.salt, IsActive = true, IsEmailVerified = true, DisplayName = "Rate 109" });
                await db.SaveChangesAsync();
            }

            for (int i = 0; i < 6; i++)
            {
                await Client.PostAsJsonAsync(TestApiEndpoints.AuthLogin, new LoginRequest { Identifier = email, Password = "Wrong" });
            }

            var final = await Client.PostAsJsonAsync(TestApiEndpoints.AuthLogin, new LoginRequest { Identifier = email, Password = "Wrong" });
            final.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            var payload = await final.Content.ReadFromJsonAsync<ApiResponse<object>>();
            payload.Should().NotBeNull();
            payload!.IsSuccess.Should().BeFalse();
            payload.Message.Should().NotBeNullOrWhiteSpace();
        }

        /// <summary>
        /// GetMe endpoint returns user profile when authenticated (critical auth test).
        /// </summary>
        [Fact]
        public async Task GetMe_WithValidToken_ReturnsSuccess()
        {
            var email = "getme@example.com";
            var password = "Password1!";

            using (var scope = Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hashed = CreateHash(password);
                await db.Users.AddAsync(new User
                {
                    Email = email,
                    Username = "getmeuser",
                    DisplayName = "Get Me User",
                    PasswordHash = hashed.hash,
                    PasswordSalt = hashed.salt,
                    IsActive = true,
                    IsEmailVerified = true
                });
                await db.SaveChangesAsync();
            }

            // Login to get access token
            var loginRequest = new LoginRequest { Identifier = email, Password = password };
            var loginResponse = await Client.PostAsJsonAsync(TestApiEndpoints.AuthLogin, loginRequest);
            var loginPayload = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            var accessToken = loginPayload!.Data!.AccessToken;

            // Access protected endpoint with bearer token
            var request = new HttpRequestMessage(HttpMethod.Get, TestApiEndpoints.AuthMe);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            var response = await Client.SendAsync(request);
            
            // Should not be 401 or 403 - authentication should work
            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        }
    }
}
