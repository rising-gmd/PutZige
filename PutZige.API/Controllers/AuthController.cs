#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PutZige.Application.DTOs.Auth;
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

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Logs in an existing user.
        /// </summary>
        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request, CancellationToken ct)
        {
            var response = await _authService.LoginAsync(request.Identifier, request.Password, ct);
            return Success(response, ResponseCodes.LOGIN_SUCCESS, SuccessMessages.Authentication.LoginSuccessful);
        }

        /// <summary>
        /// Refreshes the authentication token.
        /// </summary>
        [HttpPost("refresh-token")]
        [EnableRateLimiting("refresh-token")]
        public async Task<ActionResult<ApiResponse<RefreshTokenResponse>>> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
        {
            var response = await _authService.RefreshTokenAsync(request.RefreshToken, ct);
            return Success(response, ResponseCodes.LOGIN_SUCCESS, SuccessMessages.Authentication.TokenRefreshed);
        }

        [HttpPost("verify-email")]
        public async Task<ActionResult<ApiResponse<object>>> VerifyEmail([FromBody] PutZige.Application.DTOs.Auth.VerifyEmailRequest request, CancellationToken ct)
        {
            await _authService.VerifyEmailAsync(request.Token, ct);
            return Success<object>(null, ResponseCodes.EMAIL_VERIFIED, SuccessMessages.Authentication.EmailVerified);
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
