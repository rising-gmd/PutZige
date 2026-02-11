#nullable enable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PutZige.Application.Common.Constants;
using PutZige.Application.DTOs.Common;
using PutZige.Application.DTOs.SignalR;
using PutZige.Application.Interfaces;

namespace PutZige.API.Controllers
{
    [Route("api/v1/signalr")]
    [Authorize]
    public sealed class SignalRController : BaseApiController
    {
        private readonly IJwtTokenService _jwtTokenService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUserService _userService;

        public SignalRController(
            IJwtTokenService jwtTokenService,
            ICurrentUserService currentUserService,
            IUserService userService)
        {
            _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        /// <summary>
        /// Issues a short-lived SignalR connection token for the authenticated user.
        /// </summary>
        [HttpPost("negotiate")]
        public async Task<ActionResult<ApiResponse<SignalRNegotiateResponse>>> Negotiate(CancellationToken ct)
        {
            var userId = _currentUserService.GetUserId();
            var userProfile = await _userService.GetMyProfileAsync(ct);

            // 30-second token — long enough to complete the SignalR handshake
            var connectionToken = _jwtTokenService.GenerateAccessToken(
                userId,
                userProfile.Email,
                userProfile.Username,
                expiryMinutes: 0.5,
                out _);

            return Success(
                new SignalRNegotiateResponse
                {
                    ConnectionToken = connectionToken,
                    ExpiresIn = 30
                },
                ResponseCodes.NEGOTIATE_SUCCESS,
                "Negotiate token generated");
        }
    }
}