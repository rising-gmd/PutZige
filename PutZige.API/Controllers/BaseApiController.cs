using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PutZige.Application.DTOs.Common;
using System;
using System.Security.Claims;
using System.Collections.Generic;

namespace PutZige.API.Controllers
{
    [ApiController]
    public abstract class BaseApiController : ControllerBase
    {
        protected ActionResult<ApiResponse<T>> Success<T>(T data, string responseCode, string? message = null, Dictionary<string, object>? metadata = null)
            => Ok(ApiResponse<T>.Success(data, responseCode, message, metadata));

        protected ActionResult<ApiResponse<T>> Created<T>(T data, string responseCode, string? message = null, Dictionary<string, object>? metadata = null)
            => StatusCode(StatusCodes.Status201Created, ApiResponse<T>.Success(data, responseCode, message, metadata));

        protected ActionResult<ApiResponse<T>> BadRequestError<T>(string responseCode, string? message = null, Dictionary<string, string[]>? errors = null, Dictionary<string, object>? metadata = null)
            => BadRequest(ApiResponse<T>.Error(responseCode, message, errors, StatusCodes.Status400BadRequest, metadata));

        protected ActionResult<ApiResponse<T>> NotFoundError<T>(string responseCode, string? message = null, Dictionary<string, object>? metadata = null)
            => NotFound(ApiResponse<T>.Error(responseCode, message, null, StatusCodes.Status404NotFound, metadata));

        protected ActionResult<ApiResponse<T>> UnauthorizedError<T>(string responseCode, string? message = null, Dictionary<string, object>? metadata = null)
            => Unauthorized(ApiResponse<T>.Error(responseCode, message, null, StatusCodes.Status401Unauthorized, metadata));

        protected ActionResult<ApiResponse<T>> ForbiddenError<T>(string responseCode, string? message = null, Dictionary<string, object>? metadata = null)
            => StatusCode(StatusCodes.Status403Forbidden, ApiResponse<T>.Error(responseCode, message, null, StatusCodes.Status403Forbidden, metadata));

        protected ActionResult<ApiResponse<T>> ServerError<T>(string responseCode, string? message = null, Dictionary<string, object>? metadata = null)
            => StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<T>.Error(responseCode, message, null, StatusCodes.Status500InternalServerError, metadata));

        /// <summary>
        /// Extracts the authenticated user's ID from JWT claims.
        /// </summary>
        /// <returns>Guid user id</returns>
        /// <exception cref="UnauthorizedAccessException">If the user id claim is missing or invalid.</exception>
        protected Guid GetUserIdFromClaims()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid user token");
            }

            return userId;
        }
    }
}
