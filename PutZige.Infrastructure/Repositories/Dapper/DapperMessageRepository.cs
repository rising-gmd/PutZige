#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
    /// <summary>
    /// High-performance Dapper-based message repository.
    /// Optimized for real-time chat at billion-user scale.
    /// </summary>
    public sealed class DapperMessageRepository : IDapperMessageRepository
    {
        private readonly DapperContext _context;
        private readonly ILogger<DapperMessageRepository> _logger;

        public DapperMessageRepository(DapperContext context, ILogger<DapperMessageRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<ConversationProjection>> GetConversationsForUserAsync(Guid userId, int limit, CancellationToken ct = default)
        {
            if (userId == Guid.Empty) return Array.Empty<ConversationProjection>();

            var conn = _context.GetOpenConnection();

            try
            {
                var param = new { UserId = userId, Limit = limit };
                var result = await conn.QueryAsync<ConversationProjection>(
                    new CommandDefinition(MessageQueries.GET_CONVERSATIONS_FOR_USER, param, cancellationToken: ct))
                    .ConfigureAwait(false);
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("GetConversationsForUserAsync canceled for user {UserId}", userId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetConversationsForUserAsync failed for user {UserId}", userId);
                throw;
            }
        }

        public async Task<(IEnumerable<MessageProjection> Messages, long TotalCount)> GetConversationHistoryAsync(
            Guid userId,
            Guid otherUserId,
            int pageNumber,
            int pageSize,
            CancellationToken ct = default)
        {
            if (userId == Guid.Empty || otherUserId == Guid.Empty)
                return (Array.Empty<MessageProjection>(), 0);

            var conn = _context.GetOpenConnection();

            try
            {
                var offset = (pageNumber - 1) * pageSize;
                var param = new { UserId = userId, OtherUserId = otherUserId, Offset = offset, PageSize = pageSize };

                // Execute both queries in parallel for better performance
                var messagesTask = conn.QueryAsync<MessageProjection>(
                    new CommandDefinition(MessageQueries.GET_CONVERSATION_HISTORY, param, cancellationToken: ct));
                
                var countTask = conn.ExecuteScalarAsync<long>(
                    new CommandDefinition(MessageQueries.GET_CONVERSATION_COUNT, param, cancellationToken: ct));

                await Task.WhenAll(messagesTask, countTask).ConfigureAwait(false);

                return (messagesTask.Result, countTask.Result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("GetConversationHistoryAsync canceled for users {UserId} - {OtherUserId}", userId, otherUserId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetConversationHistoryAsync failed for users {UserId} - {OtherUserId}", userId, otherUserId);
                throw;
            }
        }

        public async Task<long> GetTotalUnreadCountAsync(Guid userId, CancellationToken ct = default)
        {
            if (userId == Guid.Empty) return 0;

            var conn = _context.GetOpenConnection();

            try
            {
                var param = new { UserId = userId };
                return await conn.ExecuteScalarAsync<long>(
                    new CommandDefinition(MessageQueries.GET_TOTAL_UNREAD_COUNT, param, cancellationToken: ct))
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("GetTotalUnreadCountAsync canceled for user {UserId}", userId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTotalUnreadCountAsync failed for user {UserId}", userId);
                throw;
            }
        }

        public async Task<int> MarkConversationAsReadAsync(Guid userId, Guid otherUserId, CancellationToken ct = default)
        {
            if (userId == Guid.Empty || otherUserId == Guid.Empty) return 0;

            var conn = _context.GetOpenConnection();

            try
            {
                var param = new { UserId = userId, OtherUserId = otherUserId, ReadAt = DateTime.UtcNow };
                return await conn.ExecuteAsync(
                    new CommandDefinition(MessageQueries.MARK_CONVERSATION_AS_READ, param, cancellationToken: ct))
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("MarkConversationAsReadAsync canceled for users {UserId} - {OtherUserId}", userId, otherUserId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MarkConversationAsReadAsync failed for users {UserId} - {OtherUserId}", userId, otherUserId);
                throw;
            }
        }
    }
}
