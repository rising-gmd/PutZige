#nullable enable
using PutZige.Application.Common.Users;
using PutZige.Application.DTOs.Auth;
using PutZige.Application.DTOs.Common;
using PutZige.Application.DTOs.Users;

namespace PutZige.Application.Interfaces
{
    /// <summary>
    /// Service contract for user registration and management.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Registers a new user with validation and hashing.
        /// </summary>
        /// <param name="email">User email address</param>
        /// <param name="username">Unique username</param>
        /// <param name="password">Plain text password (will be hashed)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Registration response with user details</returns>
        Task<RegisterUserResponse> RegisterUserAsync(
            string email,
            string username,
            string password,
            CancellationToken ct = default);

        /// <summary>
        /// Retrieves the current authenticated user's profile.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>User profile details</returns>
        /// <exception cref="KeyNotFoundException">When user not found</exception>
        Task<UserProfileResponse> GetMyProfileAsync(CancellationToken ct = default);

        /// <summary>
        /// Searches for users by query string (username, email, or display name).
        /// Legacy API kept for compatibility.
        /// </summary>
        /// <param name="query">Search query (minimum 1 character)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>List of matching users with total count</returns>
        Task<UserSearchResponse> SearchUsersAsync(string query, CancellationToken ct = default);

        /// <summary>
        /// Searches for users by query string (username, email, or display name).
        /// New paginated API.
        /// </summary>
        Task<SearchUsersResponse> SearchUsersAsync(SearchUsersRequest request, CancellationToken ct = default);

        /// <summary>
        /// Get recent contacts for current user.
        /// </summary>
        Task<System.Collections.Generic.List<UserSearchResultDto>> GetRecentContactsAsync(
            int limit = 10,
            CancellationToken ct = default);

        /// <summary>
        /// Get suggested users for current user.
        /// </summary>
        Task<System.Collections.Generic.List<UserSearchResultDto>> GetSuggestedUsersAsync(
            int limit = 10,
            CancellationToken ct = default);

        /// <summary>
        /// Retrieves a user by email address.
        /// </summary>
        /// <param name="email">User email</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>User details or null if not found</returns>
        Task<RegisterUserResponse?> GetUserByEmailAsync(string email, CancellationToken ct = default);

        /// <summary>
        /// Soft deletes a user account by marking as deleted.
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="ct">Cancellation token</param>
        /// <exception cref="KeyNotFoundException">When user not found</exception>
        Task SoftDeleteUserAsync(Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Updates user's last login timestamp, IP address, and refresh token.
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="ipAddress">Login IP address</param>
        /// <param name="refreshToken">New refresh token (will be hashed)</param>
        /// <param name="refreshTokenExpiry">Token expiration time</param>
        /// <param name="ct">Cancellation token</param>
        /// <exception cref="KeyNotFoundException">When user not found</exception>
        Task UpdateLoginInfoAsync(
            Guid userId,
            string? ipAddress,
            string refreshToken,
            DateTime refreshTokenExpiry,
            CancellationToken ct = default);
    }
}