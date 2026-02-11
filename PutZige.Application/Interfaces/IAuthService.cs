#nullable enable
using PutZige.Application.DTOs.Auth;
namespace PutZige.Application.Interfaces
{
    /// <summary>
    /// Service contract for user authentication and session management.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Authenticates a user with identifier (email or username) and password.
        /// </summary>
        /// <param name="identifier">Email address or username</param>
        /// <param name="password">Plain text password</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Login response containing tokens and user profile</returns>
        /// <exception cref="AppException">When credentials are invalid or account is locked/inactive</exception>
        Task<LoginResponse> LoginAsync(string identifier, string password, CancellationToken ct = default);

        /// <summary>
        /// Generates new access and refresh tokens using a valid refresh token.
        /// </summary>
        /// <param name="refreshToken">Current refresh token</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>New token pair</returns>
        /// <exception cref="AppException">When refresh token is invalid or expired</exception>
        Task<RefreshTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

        /// <summary>
        /// Logs out the current authenticated user and invalidates their session.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        Task LogoutAsync(CancellationToken ct = default);

        /// <summary>
        /// Verifies a user's email address using the verification token.
        /// </summary>
        /// <param name="token">Email verification token</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>True if verification successful</returns>
        /// <exception cref="AppException">When token is invalid, expired, or already used</exception>
        Task<bool> VerifyEmailAsync(string token, CancellationToken ct = default);

        /// <summary>
        /// Resends the email verification link to a user.
        /// </summary>
        /// <param name="token">Original verification token to identify the user</param>
        /// <param name="ct">Cancellation token</param>
        /// <exception cref="AppException">When rate limit exceeded or email already verified</exception>
        Task ResendVerificationEmailAsync(string token, CancellationToken ct = default);
    }
}