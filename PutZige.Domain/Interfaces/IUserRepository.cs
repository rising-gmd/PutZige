using System;
using System.Threading;
using System.Threading.Tasks;
using PutZige.Domain.Entities;

namespace PutZige.Domain.Interfaces
{
    /// <summary>
    /// User-specific repository interface.
    /// </summary>
    public interface IUserRepository : IRepository<User>
    {
        /// <summary>
        /// Gets a user by email.
        /// </summary>
        Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

        /// <summary>
        /// Gets a user by username.
        /// </summary>
        Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);

        /// <summary>
        /// Checks if an email is already taken.
        /// </summary>
        Task<bool> IsEmailTakenAsync(string email, CancellationToken ct = default);

        /// <summary>
        /// Checks if a username is already taken.
        /// </summary>
        Task<bool> IsUsernameTakenAsync(string username, CancellationToken ct = default);

        /// <summary>
        /// Gets a user by email including session navigation.
        /// </summary>
        Task<User?> GetByEmailWithSessionAsync(string email, CancellationToken ct = default);

        /// <summary>
        /// Gets a user by username including session navigation.
        /// </summary>
        Task<User?> GetByUsernameWithSessionAsync(string username, CancellationToken ct = default);

        /// <summary>
        /// Gets a user by refresh token via session.
        /// </summary>
        Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
        
        /// <summary>
        /// Gets a user by email verification token.
        /// </summary>
        Task<User?> GetByVerificationTokenAsync(string token, CancellationToken ct = default);

        /// <summary>
        /// Atomically verifies an email by token. Returns number of rows affected.
        /// </summary>
        Task<int> VerifyEmailByTokenAsync(string token, CancellationToken ct = default);

        /// <summary>
        /// Gets a user by email with tracking enabled for updates.
        /// </summary>
        Task<User?> GetByEmailForUpdateAsync(string email, CancellationToken ct = default);
    }
}
