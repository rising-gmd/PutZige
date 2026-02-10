#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PutZige.Application.Interfaces;
using PutZige.Application.DTOs.SignalR;
using PutZige.Application.DTOs.Common;
using PutZige.Application.Common.Constants;

namespace PutZige.API.Controllers
{
    [Route("api/v1/signalr")]
    [Authorize]
    public sealed class SignalRController : BaseApiController
    {
        private readonly IJwtTokenService _jwtTokenService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUserService _userService;

        public SignalRController(IJwtTokenService jwtTokenService, ICurrentUserService currentUserService, IUserService userService)
        {
            _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        [HttpPost("negotiate")]
        public async Task<ActionResult<ApiResponse<SignalRNegotiateResponse>>> Negotiate(CancellationToken ct)
        {
            try
            {
            var userId = _currentUserService.GetUserId();
            var userProfile = await _userService.GetMyProfileAsync(ct);
            // Generate a short-lived token (30 seconds = 0.5 minutes)
            var connectionToken = _jwtTokenService.GenerateAccessToken(userId, userProfile.Email, userProfile.Username, expiryMinutes: 0.5, out var expiresAt);

            return Success(new SignalRNegotiateResponse
            {
                ConnectionToken = connectionToken,
                ExpiresIn = 30
            }, ResponseCodes.NEGOTIATE_SUCCESS, "Negotiate token generated");
            }
            catch (Exception ex)
            {
                var logger = HttpContext.RequestServices.GetService(typeof(ILogger<SignalRController>)) as ILogger<SignalRController>;
                logger?.LogError(ex, "Failed to generate SignalR negotiate token for user");
                return ServerError<SignalRNegotiateResponse>(ResponseCodes.INTERNAL_SERVER_ERROR, "Failed to generate negotiate token");
            }
        }
    }
}
