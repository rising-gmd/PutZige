#nullable enable
using System;
using System.Threading.Tasks;

namespace PutZige.Application.Interfaces
{
    public interface IRealTimeNotifier
    {
        Task TryNotifyMessageDeliveredAsync(Guid recipientUserId, Guid messageId, DateTime deliveredAt);
        Task TryNotifyMessageReadAsync(Guid recipientUserId, Guid messageId, DateTime readAt);
        Task TryNotifyUserOnlineAsync(Guid userId);
        Task TryNotifyUserOfflineAsync(Guid userId);
        Task TryNotifyUserTypingAsync(Guid conversationId, Guid userId);
        Task TryNotifyUserStoppedTypingAsync(Guid conversationId, Guid userId);
    }
}
