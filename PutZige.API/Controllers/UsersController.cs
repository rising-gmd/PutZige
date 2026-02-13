#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using PutZige.Application.Common.Constants;
using PutZige.Application.Common.Messages;
using PutZige.Application.Common.Users;
using PutZige.Application.DTOs.Auth;
using PutZige.Application.DTOs.Common;
using PutZige.Application.Interfaces;

namespace PutZige.API.Controllers
{
    [Route("api/v1/users")]
    public sealed class UsersController : BaseApiController
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            IUserService userService,
            ILogger<UsersController> logger)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new user account.
        /// </summary>
        [HttpPost]
        [EnableRateLimiting("registration")]
        public async Task<ActionResult<ApiResponse<RegisterUserResponse>>> CreateUser(
            [FromBody] RegisterUserRequest request,
            CancellationToken ct = default)
        {
            var response = await _userService.RegisterUserAsync(
                request.Email ?? string.Empty,
                request.Username ?? string.Empty,
                request.Password ?? string.Empty,
                ct);

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

        /// <summary>
        /// Search users by username, email, or display name.
        /// </summary>
        [HttpGet("search")]
        [EnableRateLimiting("api-general")]
        public async Task<ActionResult<ApiResponse<PutZige.Application.DTOs.Users.SearchUsersResponse>>> SearchUsers(
            [FromQuery] string query,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            var request = new PutZige.Application.DTOs.Users.SearchUsersRequest { Query = query, PageNumber = pageNumber, PageSize = pageSize };

            var response = await _userService.SearchUsersAsync(request, ct);

            return Success(response, ResponseCodes.USERS_FOUND);
        }

        /// <summary>
        /// Get recent contacts (users chatted with recently).
        /// </summary>
        [HttpGet("recent-contacts")]
        [EnableRateLimiting("api-general")]
        public async Task<ActionResult<ApiResponse<System.Collections.Generic.List<PutZige.Application.DTOs.Users.UserSearchResultDto>>>> GetRecentContacts(
            [FromQuery] int limit = 10,
            CancellationToken ct = default)
        {
            var contacts = await _userService.GetRecentContactsAsync(limit, ct);

            return Success(contacts, ResponseCodes.CONVERSATIONS_RETRIEVED);
        }

        /// <summary>
        /// Get suggested users (mutual contacts, etc).
        /// </summary>
        [HttpGet("suggestions")]
        [EnableRateLimiting("api-general")]
        public async Task<ActionResult<ApiResponse<System.Collections.Generic.List<PutZige.Application.DTOs.Users.UserSearchResultDto>>>> GetSuggestions(
            [FromQuery] int limit = 10,
            CancellationToken ct = default)
        {
            var suggestions = await _userService.GetSuggestedUsersAsync(limit, ct);

            return Success(suggestions, ResponseCodes.CONVERSATIONS_RETRIEVED);
        }
    }
}