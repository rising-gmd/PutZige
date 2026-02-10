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
using System.Linq;
using PutZige.Application.Common.Constants;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;

namespace PutZige.API.Controllers
{
    [Route("api/v1/users")]
    public sealed class UsersController : BaseApiController
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;
        private readonly PutZige.Application.Interfaces.ICurrentUserService _currentUserService;

        public UsersController(IUserService userService, PutZige.Application.Interfaces.ICurrentUserService currentUserService, ILogger<UsersController> logger)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new user account. (RESTful: POST to collection)
        /// </summary>
        [HttpPost]
        [EnableRateLimiting("registration")]
        public async Task<ActionResult<ApiResponse<RegisterUserResponse>>> CreateUser([FromBody] RegisterUserRequest request, CancellationToken ct = default)
        {
            var response = await _userService.RegisterUserAsync(request.Email ?? string.Empty, request.Username ?? string.Empty, request.Password ?? string.Empty, ct);

            return Created(response, ResponseCodes.REGISTRATION_SUCCESS, SuccessMessages.Authentication.RegistrationSuccessful);
        }

        /// <summary>
        /// Returns the authenticated user's profile.
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<UserProfileResponse>>> GetMe(CancellationToken ct = default)
        {
            var profile = await _userService.GetMyProfileAsync(ct);

            return Success(profile, ResponseCodes.PROFILE_RETRIEVED_SUCCESSFULLY, SuccessMessages.UserProfile.ProfileRetrieved);
        }

        [HttpGet("search")]
        public async Task<ActionResult<ApiResponse<object>>> Search([FromQuery] string query, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 1)
            {
                return BadRequestError<object>(ResponseCodes.VALIDATION_FAILED, "Query parameter is required and must be at least 1 character");
            }

            var results = await _userService.SearchUsersAsync(query, ct).ConfigureAwait(false);

            var users = results.Select(u => new
            {
                id = u.Id,
                username = u.Username,
                displayName = u.DisplayName,
                email = u.Email,
                jobTitle = u.JobTitle,
                bio = u.Bio,
                profilePictureUrl = u.ProfilePictureUrl
            }).ToArray();
            var response = (object)new { users, totalCount = users.Length };

            return Success(response, ResponseCodes.USERS_FOUND);
        }
    }
}
