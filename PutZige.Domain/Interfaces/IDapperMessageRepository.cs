using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PutZige.Domain.Interfaces
{
    /// <summary>
    /// High-performance Dapper-based message repository for read operations.
    /// Optimized for real-time chat at scale.
    /// </summary>
    public interface IDapperMessageRepository
    {
        /// <summary>
        /// Get conversation list for a user with last message and unread counts.
        /// </summary>
        Task<IEnumerable<PutZige.Domain.DTOs.ConversationProjection>> GetConversationsForUserAsync(Guid userId, int limit, CancellationToken ct = default);

        /// <summary>
        /// Get paginated conversation history between two users.
        /// </summary>
        // New API: conversation-based history
        Task<(IEnumerable<PutZige.Domain.DTOs.MessageProjection> Messages, long TotalCount)> GetConversationHistoryAsync(
            Guid conversationId, 
            int pageNumber, 
            int pageSize, 
            CancellationToken ct = default);

        // (legacy user-to-user overload removed)

        /// <summary>
        /// Get total unread message count for a user across all conversations.
        /// </summary>
        Task<long> GetTotalUnreadCountAsync(Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Get unread message count for a specific receiver in a specific conversation.
        /// </summary>
        Task<int> GetUnreadCountForConversationAsync(Guid conversationId, Guid receiverId, CancellationToken ct = default);

        /// <summary>
        /// Mark all messages from a sender as read (bulk operation).
        /// </summary>
        Task<int> MarkConversationAsReadAsync(Guid userId, Guid otherUserId, CancellationToken ct = default);
    }
}
