#nullable enable
using System;
using System.Threading.Tasks;
using PutZige.Application.Interfaces;
using PutZige.Application.DTOs.Messaging;

namespace PutZige.Application.Services
{
    internal sealed class NoOpRealTimeNotifier : IRealTimeNotifier
    {
        public Task TryNotifyMessageDeliveredAsync(Guid recipientUserId, Guid messageId, DateTime deliveredAt) => Task.CompletedTask;
        public Task TryNotifyMessageReadAsync(Guid recipientUserId, Guid messageId, DateTime readAt) => Task.CompletedTask;
        public Task TryNotifyUserOnlineAsync(Guid userId) => Task.CompletedTask;
        public Task TryNotifyUserOfflineAsync(Guid userId) => Task.CompletedTask;
        public Task TryNotifyUserTypingAsync(Guid conversationId, Guid userId) => Task.CompletedTask;
        public Task TryNotifyUserStoppedTypingAsync(Guid conversationId, Guid userId) => Task.CompletedTask;
        public Task TryNotifyConversationCreatedAsync(Guid recipientUserId, ConversationDto conversation) => Task.CompletedTask;
    }
}
