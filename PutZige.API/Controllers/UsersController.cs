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
    [Route("api/v1/users")]
    public sealed class UsersController : BaseApiController
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserService userService, ILogger<UsersController> logger)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
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
    }
}
