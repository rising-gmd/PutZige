#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using PutZige.Application.DTOs.Auth;
using PutZige.Application.DTOs.Common;

namespace PutZige.Application.Interfaces
{
    /// <summary>
    /// Service contract for user registration and management.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Registers a new user with validation and hashing and returns a response DTO.
        /// </summary>
        Task<RegisterUserResponse> RegisterUserAsync(
            string email,
            string username,
            string password,
            CancellationToken ct = default);

        /// <summary>
        /// Gets a user by email.
        /// </summary>
        Task<RegisterUserResponse?> GetUserByEmailAsync(string email, CancellationToken ct = default);

        /// <summary>
        /// Soft deletes a user by id.
        /// </summary>
        Task SoftDeleteUserAsync(Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Updates user's last login info and session details.
        /// </summary>
        Task UpdateLoginInfoAsync(Guid userId, string? ipAddress, string refreshToken, DateTime refreshTokenExpiry, CancellationToken ct = default);

        /// <summary>
        /// Gets the profile for the current authenticated user. Throws if user not found or unauthorized.
        /// </summary>
        Task<UserProfileResponse> GetMyProfileAsync(CancellationToken ct = default);
    }
}
