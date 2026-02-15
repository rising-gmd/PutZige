#nullable enable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PutZige.Application.Interfaces;
using PutZige.Domain.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PutZige.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMessagingService _messagingService;
    private readonly ILogger<ChatHub>? _logger;
    private readonly IConnectionMappingService _connectionMapping;
    private readonly IConversationRepository _conversationRepository;

    public ChatHub(
        IMessagingService messagingService,
        IConnectionMappingService connectionMapping,
        IConversationRepository conversationRepository,
        ILogger<ChatHub>? logger = null)
    {
        _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
        _connectionMapping = connectionMapping ?? throw new ArgumentNullException(nameof(connectionMapping));
        _conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = GetUserIdFromContext();
            if (userId == null)
            {
                _logger?.LogWarning("Connection rejected - No valid user ID in claims: {ConnectionId}", Context.ConnectionId);
                Context.Abort();
                return;
            }

            _connectionMapping.Add(userId.Value, Context.ConnectionId);

            _logger?.LogInformation("User connected - UserId: {UserId}, ConnectionId: {ConnectionId}", userId.Value, Context.ConnectionId);

            await Clients.All.SendAsync("UserOnline", new { UserId = userId.Value, IsOnline = true, LastSeen = DateTime.UtcNow });

            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to establish connection - ConnectionId: {ConnectionId}", Context.ConnectionId);
            Context.Abort();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var userId = GetUserIdFromContext();

            if (userId.HasValue)
            {
                _connectionMapping.Remove(userId.Value);

                _logger?.LogInformation("User disconnected - UserId: {UserId}, ConnectionId: {ConnectionId}", userId.Value, Context.ConnectionId);

                await Clients.All.SendAsync("UserOffline", new { UserId = userId.Value, IsOnline = false, LastSeen = DateTime.UtcNow });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error while handling disconnect for ConnectionId: {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(Guid conversationId, string messageText)
    {
        var ct = Context.ConnectionAborted;
        try
        {
            var senderId = GetCurrentUserId();

            var response = await _messagingService.SendMessageAsync(conversationId, messageText, senderId, ct).ConfigureAwait(false);

            var conversation = await _conversationRepository.GetByIdWithParticipantsAsync(conversationId, ct).ConfigureAwait(false);
            if (conversation != null)
            {
                foreach (var participant in conversation.Participants)
                {
                    if (participant.UserId == senderId) continue;

                    if (_connectionMapping.TryGetConnection(participant.UserId, out var connectionId))
                    {
                        await Clients.Client(connectionId).SendAsync("ReceiveMessage", response, ct).ConfigureAwait(false);
                        try
                        {
                            await _messagingService.MarkMessageAsDeliveredAsync(response.MessageId, ct).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to mark message delivered for MessageId: {MessageId}", response.MessageId);
                        }
                    }
                }
            }

            await Clients.Caller.SendAsync(PutZige.Application.Common.Constants.SignalRConstants.Events.MessageSent, response, ct).ConfigureAwait(false);
        }
        catch (KeyNotFoundException ex)
        {
            _logger?.LogWarning(ex, "Resource not found - ConnectionId: {ConnectionId}", Context.ConnectionId);
            throw new HubException(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger?.LogWarning(ex, "Invalid argument - ConnectionId: {ConnectionId}", Context.ConnectionId);
            throw new HubException(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.LogWarning(ex, "Unauthorized access - ConnectionId: {ConnectionId}", Context.ConnectionId);
            throw new HubException("Not authorized");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send message - ConnectionId: {ConnectionId}", Context.ConnectionId);
            throw new HubException(ex.Message);
        }
    }

    public async Task StartTyping(Guid conversationId)
    {
        var ct = Context.ConnectionAborted;
        var senderId = GetCurrentUserId();
        var conversation = await _conversationRepository.GetByIdWithParticipantsAsync(conversationId, ct).ConfigureAwait(false);
        if (conversation == null) return;

        foreach (var participant in conversation.Participants)
        {
            if (participant.UserId == senderId) continue;
            if (_connectionMapping.TryGetConnection(participant.UserId, out var connectionId))
                await Clients.Client(connectionId).SendAsync("UserTyping", new { UserId = senderId, ConversationId = conversationId }, ct).ConfigureAwait(false);
        }
    }

    public async Task StopTyping(Guid conversationId)
    {
        var ct = Context.ConnectionAborted;
        var senderId = GetCurrentUserId();
        var conversation = await _conversationRepository.GetByIdWithParticipantsAsync(conversationId, ct).ConfigureAwait(false);
        if (conversation == null) return;

        foreach (var participant in conversation.Participants)
        {
            if (participant.UserId == senderId) continue;
            if (_connectionMapping.TryGetConnection(participant.UserId, out var connectionId))
                await Clients.Client(connectionId).SendAsync("UserStoppedTyping", new { UserId = senderId, ConversationId = conversationId }, ct).ConfigureAwait(false);
        }
    }

    public async Task NotifyMessageDelivered(Guid messageId, Guid receiverId, DateTime deliveredAt)
    {
        if (_connectionMapping.TryGetConnection(receiverId, out var connectionId))
            await Clients.Client(connectionId).SendAsync("MessageDelivered", new { MessageId = messageId, DeliveredAt = deliveredAt }).ConfigureAwait(false);
    }

    public async Task NotifyMessageRead(Guid messageId, Guid senderId, DateTime readAt)
    {
        if (_connectionMapping.TryGetConnection(senderId, out var connectionId))
            await Clients.Client(connectionId).SendAsync("MessageRead", new { MessageId = messageId, ReadAt = readAt }).ConfigureAwait(false);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private Guid? GetUserIdFromContext()
    {
        var sub = Context.User?.FindFirst("sub")?.Value
               ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(sub, out var userId) ? userId : null;
    }

    private Guid GetCurrentUserId()
    {
        return GetUserIdFromContext() ?? throw new HubException("Unauthorized");
    }
}