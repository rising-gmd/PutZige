using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using PutZige.Domain.DTOs;
using PutZige.Domain.Interfaces;
using PutZige.Infrastructure.Data.Dapper;
using PutZige.Infrastructure.Data.Dapper.SqlQueries;

namespace PutZige.Infrastructure.Repositories.Dapper
{
    public sealed class DapperUserRepository : IDapperUserRepository
    {
        private readonly DapperContext _context;
        private readonly ILogger<DapperUserRepository> _logger;

        public DapperUserRepository(DapperContext context, ILogger<DapperUserRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<System.Collections.Generic.IEnumerable<PutZige.Domain.DTOs.UserSearchProjection>> SearchUsersAsync(string query, Guid currentUserId, int limit, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query)) return System.Array.Empty<PutZige.Domain.DTOs.UserSearchProjection>();

            var conn = _context.GetOpenConnection();

            try
            {
                var param = new { Query = $"%{query}%", CurrentUserId = currentUserId, Limit = limit };
                var result = await conn.QueryAsync<PutZige.Domain.DTOs.UserSearchProjection>(new CommandDefinition(UserQueries.SEARCH_USERS, param, cancellationToken: ct)).ConfigureAwait(false);
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Dapper SearchUsersAsync canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dapper SearchUsersAsync failed");
                throw;
            }
        }

        public async Task<System.Collections.Generic.IEnumerable<PutZige.Domain.DTOs.UserSearchProjection>> GetRecentContactsAsync(Guid userId, int limit, int daysSince, CancellationToken ct = default)
        {
            var conn = _context.GetOpenConnection();

            try
            {
                var param = new { UserId = userId, Limit = limit, DaysSince = daysSince };

                return await conn.QueryAsync<PutZige.Domain.DTOs.UserSearchProjection>(
                    new CommandDefinition(UserQueries.RECENT_CONTACTS, param, cancellationToken: ct));
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("GetRecentContactsAsync canceled for user: {UserId}", userId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetRecentContactsAsync failed for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<System.Collections.Generic.IEnumerable<PutZige.Domain.DTOs.UserSearchProjection>> GetSuggestedUsersAsync(Guid userId, int limit, CancellationToken ct = default)
        {
            var conn = _context.GetOpenConnection();

            try
            {
                var param = new { UserId = userId, Limit = limit };

                return await conn.QueryAsync<PutZige.Domain.DTOs.UserSearchProjection>(
                    new CommandDefinition(UserQueries.SUGGESTED_USERS, param, cancellationToken: ct));
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("GetSuggestedUsersAsync canceled for user: {UserId}", userId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSuggestedUsersAsync failed for user: {UserId}", userId);
                return Array.Empty<PutZige.Domain.DTOs.UserSearchProjection>();
            }
        }
        private const int DefaultDaysSince = 7;

        public async Task<PutZige.Domain.Models.PagedResult<PutZige.Domain.DTOs.UserSearchProjection>> SearchUsersAsync(
            string query,
            Guid excludeUserId,
            int pageNumber,
            int pageSize,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return PutZige.Domain.Models.PagedResult<PutZige.Domain.DTOs.UserSearchProjection>.Empty(pageNumber, pageSize);

            var conn = _context.GetOpenConnection();

            try
            {
                var searchTerm = $"%{query}%";
                var offset = (pageNumber - 1) * pageSize;

                // Use centralized SQL from UserQueries
                var countSql = UserQueries.SEARCH_USERS_PAGED_COUNT;
                var dataSql = UserQueries.SEARCH_USERS_PAGED_DATA;

                var param = new
                {
                    SearchTerm = searchTerm,
                    Query = query,
                    ExcludeUserId = excludeUserId,
                    Offset = offset,
                    PageSize = pageSize
                };

                // Execute in parallel
                var countTask = conn.ExecuteScalarAsync<long>(
                    new CommandDefinition(countSql, param, cancellationToken: ct));

                var dataTask = conn.QueryAsync<PutZige.Domain.DTOs.UserSearchProjection>(
                    new CommandDefinition(dataSql, param, cancellationToken: ct));

                await Task.WhenAll(countTask, dataTask).ConfigureAwait(false);

                return PutZige.Domain.Models.PagedResult<PutZige.Domain.DTOs.UserSearchProjection>.Create(
                    dataTask.Result,
                    pageNumber,
                    pageSize,
                    countTask.Result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("SearchUsersAsync canceled for query {Query}", query);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchUsersAsync failed for query: {Query}", query);
                throw;
            }
        }

        public async Task<PutZige.Domain.DTOs.UserProfileProjection?> GetProfileByIdAsync(Guid id, CancellationToken ct = default)
        {
            if (id == Guid.Empty) return null;

            var conn = _context.GetOpenConnection();

            try
            {
                var result = await conn.QuerySingleOrDefaultAsync<UserProfileProjection>(new CommandDefinition(UserQueries.GET_USER_PROFILE_BY_ID, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Dapper GetProfileByIdAsync canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dapper GetProfileByIdAsync failed");
                throw;
            }
        }

        public async Task<int> VerifyEmailByTokenAsync(string token, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(token)) return 0;

            var conn = _context.GetOpenConnection();

            try
            {
                var result = await conn.ExecuteAsync(new CommandDefinition(UserQueries.VERIFY_EMAIL_BY_TOKEN, new { Token = token }, cancellationToken: ct)).ConfigureAwait(false);
                _logger.LogInformation("Dapper VerifyEmailByToken executed, rows affected: {Rows}", result);
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Dapper VerifyEmailByToken canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dapper VerifyEmailByToken failed");
                throw;
            }
        }

        public async Task UpdateSessionOnlineStatusAsync(Guid userId, bool isOnline, DateTime lastActiveAt, CancellationToken ct = default)
        {
            if (userId == Guid.Empty) return;

            var conn = _context.GetOpenConnection();

            try
            {
                var param = new { UserId = userId, IsOnline = isOnline, LastActiveAt = lastActiveAt };
                await conn.ExecuteAsync(new CommandDefinition(UserQueries.UPDATE_USER_SESSION_ONLINE_STATUS, param, cancellationToken: ct)).ConfigureAwait(false);
                _logger.LogInformation("Updated session online status for UserId: {UserId} IsOnline: {IsOnline}", userId, isOnline);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("UpdateSessionOnlineStatusAsync canceled for UserId: {UserId}", userId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateSessionOnlineStatusAsync failed for UserId: {UserId}", userId);
                throw;
            }
        }
    }
}
