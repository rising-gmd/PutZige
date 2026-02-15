#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using PutZige.Application.DTOs.Messaging;

namespace PutZige.Application.Interfaces
{
    /// <summary>
    /// Service contract for messaging operations between users.
    /// </summary>
    public interface IMessagingService
    {
        /// <summary>
        /// Sends a message from the current authenticated user to another user.
        /// </summary>
        /// <param name="receiverId">Recipient user identifier</param>
        /// <param name="messageText">Message content (max 2000 characters)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Sent message details</returns>
        /// <exception cref="AppException">When validation fails or users not found</exception>
        /// <exception cref="KeyNotFoundException">When sender or receiver not found</exception>
        Task<SendMessageResponse> SendMessageAsync(Guid conversationId, string messageText, Guid senderId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves paginated message history between the current user and another user.
        /// </summary>
        /// <param name="otherUserId">The other user's identifier</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Number of messages per page (max 100)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Paginated conversation history</returns>
        /// <exception cref="AppException">When validation fails</exception>
        /// <exception cref="ArgumentOutOfRangeException">When page parameters invalid</exception>
        Task<ConversationHistoryResponse> GetConversationHistoryAsync(Guid conversationId, int pageNumber, int pageSize, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all conversations for the current authenticated user.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>List of conversations with latest message and unread count</returns>
        Task<ConversationListResponse> GetConversationsAsync(CancellationToken ct = default);

        /// <summary>
        /// Marks a message as delivered (system use only).
        /// </summary>
        /// <param name="messageId">Message identifier</param>
        /// <param name="ct">Cancellation token</param>
        /// <exception cref="AppException">When message not found</exception>
        /// <exception cref="KeyNotFoundException">When message doesn't exist</exception>
        Task MarkMessageAsDeliveredAsync(Guid messageId, CancellationToken ct = default);

        /// <summary>
        /// Marks a message as read by the current user.
        /// </summary>
        /// <param name="messageId">Message identifier</param>
        /// <param name="ct">Cancellation token</param>
        /// <exception cref="AppException">When message not found</exception>
        /// <exception cref="KeyNotFoundException">When message doesn't exist</exception>
        Task MarkMessageAsReadAsync(Guid messageId, CancellationToken ct = default);
    }
}