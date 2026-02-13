#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PutZige.Domain.DTOs;
using PutZige.Domain.Models;

namespace PutZige.Domain.Interfaces;

public interface IDapperUserRepository
{
    // Existing methods (keep for backward compatibility / tests)
    Task<int> VerifyEmailByTokenAsync(string token, CancellationToken ct = default);
    Task<UserProfileProjection?> GetProfileByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Original lightweight search used by some callers (limit-based)
    /// </summary>
    Task<IEnumerable<UserSearchProjection>> SearchUsersAsync(string query, Guid currentUserId, int limit, CancellationToken ct = default);

    /// <summary>
    /// Search users by query string (username, email, displayName) with pagination.
    /// Optimized with full-text search for billion-scale.
    /// </summary>
    Task<PagedResult<UserSearchProjection>> SearchUsersAsync(
        string query,
        Guid excludeUserId,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Get recent contacts (users chatted with in last N days).
    /// </summary>
    Task<IEnumerable<UserSearchProjection>> GetRecentContactsAsync(
        Guid userId,
        int limit,
        int daysSince,
        CancellationToken ct = default);

    /// <summary>
    /// Get suggested users (mutual contacts, same org, etc).
    /// </summary>
    Task<IEnumerable<UserSearchProjection>> GetSuggestedUsersAsync(
        Guid userId,
        int limit,
        CancellationToken ct = default);
}
