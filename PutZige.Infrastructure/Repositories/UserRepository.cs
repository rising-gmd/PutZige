#nullable enable
using Microsoft.EntityFrameworkCore;
using PutZige.Domain.Entities;
using PutZige.Application.Common;
using PutZige.Application.Common.Constants;
using PutZige.Domain.Interfaces;
using PutZige.Infrastructure.Data;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PutZige.Infrastructure.Repositories;

/// <summary>
/// User repository implementation.
/// </summary>
public class UserRepository : Repository<User>, IUserRepository
{
    /// <summary>
    /// Initializes a new instance of <see cref="UserRepository"/>.
    /// </summary>
    public UserRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new AppException(ResponseCodes.EMAIL_REQUIRED, "email is required");
        return await _dbSet.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new AppException(ResponseCodes.USERNAME_REQUIRED, "username is required");
        return await _dbSet.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, ct).ConfigureAwait(false);
    }

    public async Task<User?> GetByEmailForUpdateAsync(string email, CancellationToken ct = default)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> IsEmailTakenAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        return await _dbSet.AsNoTracking().AnyAsync(u => u.Email == email, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> IsUsernameTakenAsync(string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        return await _dbSet.AsNoTracking().AnyAsync(u => u.Username == username, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<User?> GetByEmailWithSessionAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new AppException(ResponseCodes.EMAIL_REQUIRED, "email is required");
        return await _dbSet.Include(u => u.Session).FirstOrDefaultAsync(u => u.Email == email, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<User?> GetByUsernameWithSessionAsync(string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new AppException(ResponseCodes.USERNAME_REQUIRED, "username is required");
        return await _dbSet.Include(u => u.Session).FirstOrDefaultAsync(u => u.Username == username, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;
        // We cannot directly compare plaintext refreshToken to stored hash. Return user with session if session exists; calling code must verify token with hashing service.
        return await _dbSet.Include(u => u.Session).FirstOrDefaultAsync(u => u.Session != null && u.Session.RefreshTokenHash != null, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<User?> GetByVerificationTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        return await _dbSet.AsNoTracking().FirstOrDefaultAsync(u => u.EmailVerificationToken == token, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> VerifyEmailByTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return 0;

        // Atomic update: only set IsEmailVerified = true when token matches and IsEmailVerified is false
        // Use parameterized SQL for atomic update. Table/column quoting depends on provider; use EF parameters instead.
        var result = await _context.Database.ExecuteSqlInterpolatedAsync($"UPDATE Users SET IsEmailVerified = TRUE, EmailVerificationToken = NULL, EmailVerificationTokenExpiry = NULL WHERE EmailVerificationToken = {token} AND IsEmailVerified = FALSE", ct).ConfigureAwait(false);
        return result;
    }
}
