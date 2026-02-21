#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using PutZige.Application.Interfaces;
using PutZige.Application.DTOs.Messaging;
using PutZige.Application.Common.Constants;
using PutZige.API.Hubs;

namespace PutZige.API.RealTime
{
    public class SignalRNotifier : IRealTimeNotifier
    {
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly PutZige.Application.Interfaces.IConnectionMappingService _connectionMapping;

        public SignalRNotifier(IHubContext<ChatHub> hubContext, PutZige.Application.Interfaces.IConnectionMappingService connectionMapping)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _connectionMapping = connectionMapping ?? throw new ArgumentNullException(nameof(connectionMapping));
        }

        public Task TryNotifyMessageDeliveredAsync(Guid recipientUserId, Guid messageId, DateTime deliveredAt)
        {
            if (_connectionMapping.TryGetConnection(recipientUserId, out var connectionId))
            {
                return _hubContext.Clients.Client(connectionId).SendAsync(SignalRConstants.Events.MessageDelivered, new { MessageId = messageId, DeliveredAt = deliveredAt });
            }

            return Task.CompletedTask;
        }

        public Task TryNotifyMessageReadAsync(Guid recipientUserId, Guid messageId, DateTime readAt)
        {
            if (_connectionMapping.TryGetConnection(recipientUserId, out var connectionId))
            {
                return _hubContext.Clients.Client(connectionId).SendAsync(SignalRConstants.Events.MessageRead, new { MessageId = messageId, ReadAt = readAt });
            }

            return Task.CompletedTask;
        }

        public Task TryNotifyUserOnlineAsync(Guid userId)
        {
            return _hubContext.Clients.All.SendAsync(SignalRConstants.Events.UserOnline, new { UserId = userId, IsOnline = true, LastSeen = DateTime.UtcNow });
        }

        public Task TryNotifyUserOfflineAsync(Guid userId)
        {
            return _hubContext.Clients.All.SendAsync(SignalRConstants.Events.UserOffline, new { UserId = userId, IsOnline = false, LastSeen = DateTime.UtcNow });
        }

        public Task TryNotifyUserTypingAsync(Guid conversationId, Guid userId)
        {
            // No direct Others on IHubContext.Clients; broadcast to all for now. Per-connection targeting uses connection mapping.
            return _hubContext.Clients.All.SendAsync(SignalRConstants.Events.UserTyping, new { UserId = userId, ConversationId = conversationId });
        }

        public Task TryNotifyUserStoppedTypingAsync(Guid conversationId, Guid userId)
        {
            return _hubContext.Clients.All.SendAsync(SignalRConstants.Events.UserStoppedTyping, new { UserId = userId, ConversationId = conversationId });
        }

        public Task TryNotifyConversationCreatedAsync(Guid recipientUserId, ConversationDto conversation)
        {
            if (_connectionMapping.TryGetConnection(recipientUserId, out var connectionId))
                return _hubContext.Clients.Client(connectionId).SendAsync(SignalRConstants.Events.ConversationCreated, conversation);
            return Task.CompletedTask;
        }
    }
}
