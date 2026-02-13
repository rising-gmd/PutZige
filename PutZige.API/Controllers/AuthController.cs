using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PutZige.Application.Common.Constants;
using PutZige.Application.Common.Messages;
using PutZige.Application.DTOs.Auth;
using PutZige.Application.DTOs.Common;
using PutZige.Application.Interfaces;

namespace PutZige.API.Controllers
{
    [Route("api/v1/auth")]
    public sealed class AuthController : BaseApiController
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;
        private readonly ICookieService _cookieService;
        private readonly IUserService _userService;

        public AuthController(
            IAuthService authService,
            ILogger<AuthController> logger,
            ICookieService cookieService,
            IUserService userService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cookieService = cookieService ?? throw new ArgumentNullException(nameof(cookieService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        /// <summary>
        /// Authenticates a user and establishes a session.
        /// </summary>
        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(
            [FromBody] LoginRequest request,
            CancellationToken ct)
        {
            var response = await _authService.LoginAsync(request.Identifier, request.Password, ct);

            _cookieService.SetAuthCookies(Response, response.AccessToken, response.RefreshToken);

            return Success(response, ResponseCodes.LOGIN_SUCCESS, SuccessMessages.Authentication.LoginSuccessful);
        }

        /// <summary>
        /// Refreshes the authentication token using the refresh token cookie.
        /// </summary>
        [HttpPost("refresh-token")]
        [EnableRateLimiting("refresh-token")]
        public async Task<ActionResult<ApiResponse<object>>> RefreshToken(CancellationToken ct)
        {
            var refreshToken = _cookieService.GetRefreshToken(Request);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return UnauthorizedError<object>(ResponseCodes.UNAUTHORIZED, "Refresh token missing");
            }

            var response = await _authService.RefreshTokenAsync(refreshToken, ct);

            _cookieService.SetAuthCookies(Response, response.AccessToken, response.RefreshToken);

            return Success<object>(null, ResponseCodes.TOKEN_REFRESHED, SuccessMessages.Authentication.TokenRefreshed);
        }

        /// <summary>
        /// Verifies a user's email address using the verification token.
        /// </summary>
        [HttpPost("verify-email")]
        public async Task<ActionResult<ApiResponse<object>>> VerifyEmail(
            [FromBody] VerifyEmailRequest request,
            CancellationToken ct)
        {
            await _authService.VerifyEmailAsync(request.Token, ct);
            return Success<object>(null, ResponseCodes.EMAIL_VERIFIED, SuccessMessages.Authentication.EmailVerified);
        }

        /// <summary>
        /// Logs out the current user and clears session cookies.
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> Logout(CancellationToken ct)
        {
            await _authService.LogoutAsync(ct);

            _cookieService.ClearAuthCookies(Response);

            return Success<object>(null, ResponseCodes.LOGOUT_SUCCESS, "Logged out successfully");
        }

        /// <summary>
        /// Retrieves the current authenticated user's profile.
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<UserProfileResponse>>> GetMe(CancellationToken ct)
        {
            var profile = await _userService.GetMyProfileAsync(ct);
            return Success(profile, ResponseCodes.PROFILE_RETRIEVED_SUCCESSFULLY, "Profile retrieved");
        }

        /// <summary>
        /// Resends the email verification link to the user.
        /// </summary>
        [HttpPost("resend-verification")]
        [EnableRateLimiting("email-resend")]
        public async Task<ActionResult<ApiResponse<object>>> ResendVerification(
            [FromBody] ResendVerificationRequest request,
            CancellationToken ct)
        {
            await _authService.ResendVerificationEmailAsync(request.Token, ct);
            return Success<object>(null, ResponseCodes.EMAIL_VERIFICATION_SENT, SuccessMessages.Authentication.VerificationEmailSent);
        }
    }
}