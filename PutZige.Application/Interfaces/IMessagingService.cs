using System;
using System.Threading;
using System.Threading.Tasks;
using PutZige.Application.DTOs.Messaging;

namespace PutZige.Application.Interfaces;

public interface IMessagingService
{
    Task<SendMessageResponse> SendMessageAsync(Guid senderId, Guid receiverId, string messageText, CancellationToken ct = default);
    Task<ConversationHistoryResponse> GetConversationHistoryAsync(Guid userId, Guid otherUserId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<ConversationListResponse> GetConversationsAsync(Guid userId, CancellationToken ct = default);
    Task MarkMessageAsDeliveredAsync(Guid messageId, CancellationToken ct = default);
    Task MarkMessageAsReadAsync(Guid messageId, CancellationToken ct = default);
}
