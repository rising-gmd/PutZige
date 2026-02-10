#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PutZige.Application.DTOs.Auth;
using PutZige.Application.Settings;
using Microsoft.Extensions.Options;
using PutZige.Application.DTOs.Common;
using PutZige.Application.Interfaces;
using PutZige.Application.Common.Messages;
using PutZige.Application.Common.Constants;
using Microsoft.AspNetCore.RateLimiting;

namespace PutZige.API.Controllers
{
    [Route("api/v1/auth")]
    public sealed class AuthController : BaseApiController
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        private readonly PutZige.Application.Interfaces.ICurrentUserService _currentUserService;
        private readonly PutZige.Application.Interfaces.IUserService _userService;
        private readonly JwtSettings _jwtSettings;

        public AuthController(IAuthService authService, ILogger<AuthController> logger, PutZige.Application.Interfaces.ICurrentUserService currentUserService, PutZige.Application.Interfaces.IUserService userService, IOptions<JwtSettings> jwtOptions)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _jwtSettings = jwtOptions?.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
        }

        /// <summary>
        /// Logs in an existing user.
        /// </summary>
        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request, CancellationToken ct)
        {
            var response = await _authService.LoginAsync(request.Identifier, request.Password, ct);
            _logger?.LogInformation("Login successful for identifier {Identifier} - UserId: {UserId}", request.Identifier, response.UserId);

            // Set HttpOnly cookies for access and refresh tokens
            Response.Cookies.Append(PutZige.Application.Common.Constants.CookieConstants.ACCESS_TOKEN, response.AccessToken, new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
                Path = PutZige.Application.Common.Constants.ApiConstants.API_PATH,
                Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes)
            });

            // Set non-HttpOnly XSRF token cookie so frontend JS can read it
            Response.Cookies.Append("XSRF-TOKEN", Guid.NewGuid().ToString(), new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
                Path = PutZige.Application.Common.Constants.ApiConstants.API_PATH,
                Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes)
            });

            Response.Cookies.Append(PutZige.Application.Common.Constants.CookieConstants.REFRESH_TOKEN, response.RefreshToken, new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
                Path = PutZige.Application.Common.Constants.ApiConstants.API_PATH,
                Expires = DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays)
            });

            // Rotate XSRF token
            Response.Cookies.Append("XSRF-TOKEN", Guid.NewGuid().ToString(), new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
                Path = PutZige.Application.Common.Constants.ApiConstants.API_PATH,
                Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes)
            });

            // Return user profile only
            var userProfile = new LoginResponse
            {
                UserId = response.UserId,
                Email = response.Email,
                Username = response.Username,
                DisplayName = response.DisplayName,
                AccessToken = string.Empty,
                RefreshToken = string.Empty,
                ExpiresIn = 0
            };

            return Success(userProfile, ResponseCodes.LOGIN_SUCCESS, SuccessMessages.Authentication.LoginSuccessful);
        }

        /// <summary>
        /// Refreshes the authentication token.
        /// </summary>
        [HttpPost("refresh-token")]
        [EnableRateLimiting("refresh-token")]
        public async Task<ActionResult<ApiResponse<object>>> RefreshToken(CancellationToken ct)
        {
            if (!Request.Cookies.TryGetValue(PutZige.Application.Common.Constants.CookieConstants.REFRESH_TOKEN, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
            {
                return UnauthorizedError<object>(ResponseCodes.UNAUTHORIZED, "Refresh token missing");
            }

            var response = await _authService.RefreshTokenAsync(refreshToken, ct);

            // Rotate cookies
            Response.Cookies.Append(PutZige.Application.Common.Constants.CookieConstants.ACCESS_TOKEN, response.AccessToken, new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
                Path = PutZige.Application.Common.Constants.ApiConstants.API_PATH,
                Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes)
            });

            Response.Cookies.Append(PutZige.Application.Common.Constants.CookieConstants.REFRESH_TOKEN, response.RefreshToken, new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
                Path = PutZige.Application.Common.Constants.ApiConstants.API_PATH,
                Expires = DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays)
            });

            _logger?.LogInformation("Refresh token rotated for subject (cookie)");

            return Success<object>(null, ResponseCodes.TOKEN_REFRESHED, SuccessMessages.Authentication.TokenRefreshed);
        }

        [HttpPost("verify-email")]
        public async Task<ActionResult<ApiResponse<object>>> VerifyEmail([FromBody] PutZige.Application.DTOs.Auth.VerifyEmailRequest request, CancellationToken ct)
        {
            await _authService.VerifyEmailAsync(request.Token, ct);
            return Success<object>(null, ResponseCodes.EMAIL_VERIFIED, SuccessMessages.Authentication.EmailVerified);
        }

        [HttpPost("logout")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<ActionResult<ApiResponse<object>>> Logout(CancellationToken ct)
        {
            var userId = _currentUserService.GetUserId();
            await _authService.LogoutAsync(userId, ct);

            Response.Cookies.Delete(PutZige.Application.Common.Constants.CookieConstants.ACCESS_TOKEN, new Microsoft.AspNetCore.Http.CookieOptions { Path = PutZige.Application.Common.Constants.ApiConstants.API_PATH });
            Response.Cookies.Delete(PutZige.Application.Common.Constants.CookieConstants.REFRESH_TOKEN, new Microsoft.AspNetCore.Http.CookieOptions { Path = PutZige.Application.Common.Constants.ApiConstants.API_PATH });
            Response.Cookies.Delete("XSRF-TOKEN", new Microsoft.AspNetCore.Http.CookieOptions { Path = PutZige.Application.Common.Constants.ApiConstants.API_PATH });

            return Success<object>(null, ResponseCodes.LOGOUT_SUCCESS, "Logged out successfully");
        }

        [HttpGet("me")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<ActionResult<ApiResponse<PutZige.Application.DTOs.Common.UserProfileResponse>>> GetMe(CancellationToken ct)
        {
            var userId = _currentUserService.GetUserId();
            var profile = await _userService.GetMyProfileAsync(ct);
            return Success(profile, ResponseCodes.PROFILE_RETRIEVED_SUCCESSFULLY, "Profile retrieved");
        }

        [HttpPost("resend-verification")]
        [EnableRateLimiting("email-resend")]
        public async Task<ActionResult<ApiResponse<object>>> ResendVerification([FromBody] PutZige.Application.DTOs.Auth.ResendVerificationRequest request, CancellationToken ct)
        {
            await _authService.ResendVerificationEmailAsync(request.Token, ct);
            return Success<object>(null, ResponseCodes.EMAIL_VERIFICATION_SENT, SuccessMessages.Authentication.VerificationEmailSent);
        }
    }
}
