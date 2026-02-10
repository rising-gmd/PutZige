#nullable enable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PutZige.Application.DTOs.Messaging;
using PutZige.Application.Interfaces;
using System;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PutZige.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMessagingService _messagingService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ChatHub>? _logger;
    private readonly IConnectionMappingService _connectionMapping;

    public ChatHub(IMessagingService messagingService, ICurrentUserService currentUserService, IConnectionMappingService connectionMapping, ILogger<ChatHub>? logger = null)
    {
        _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _connectionMapping = connectionMapping ?? throw new ArgumentNullException(nameof(connectionMapping));
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            // Extract userId from Context.User claims (reliable for SignalR)
            var sub = Context.User?.FindFirst("sub")?.Value ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
            {
                _logger?.LogWarning("Connection rejected - No valid user ID in claims: {ConnectionId}", Context.ConnectionId);
                Context.Abort();
                return;
            }

            _connectionMapping.Add(userId, Context.ConnectionId);

            _logger?.LogInformation("User connected - UserId: {UserId}, ConnectionId: {ConnectionId}", userId, Context.ConnectionId);

            // Broadcast user online status
            await Clients.All.SendAsync("UserOnline", new { UserId = userId, IsOnline = true, LastSeen = DateTime.UtcNow });

            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to establish connection - ConnectionId: {ConnectionId}", Context.ConnectionId); // Log error on connection failure
            Context.Abort();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var userId = _currentUserService.TryGetUserId();

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

    public async Task SendMessage(Guid receiverId, string messageText)
    {
        try
        {
            // Extract sender id from SignalR Context.User (works for WebSockets)
            var sub = Context.User?.FindFirst("sub")?.Value ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var senderId))
            {
                throw new InvalidOperationException("User is not authenticated or invalid user ID");
            }

            var response = await _messagingService.SendMessageAsync(senderId, receiverId, messageText);

            if (_connectionMapping.TryGetConnection(receiverId, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync("ReceiveMessage", response);
                await _messagingService.MarkMessageAsDeliveredAsync(response.MessageId);
            }

            await Clients.Caller.SendAsync(PutZige.Application.Common.Constants.SignalRConstants.Events.MessageSent, response);
        }
        catch (KeyNotFoundException ex)
        {
            _logger?.LogWarning(ex, "Resource not found - ConnectionId: {ConnectionId}", Context.ConnectionId);
            throw; // Let SignalR convert to HubException
        }
        catch (ArgumentException ex)
        {
            _logger?.LogWarning(ex, "Invalid argument - ConnectionId: {ConnectionId}", Context.ConnectionId);
            throw; // Let SignalR convert to HubException
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogWarning(ex, "Unauthorized access - ConnectionId: {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", ex.Message);
            Context.Abort();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send message - ConnectionId: {ConnectionId}", Context.ConnectionId);
            throw;
        }
    }

    public async Task StartTyping(Guid conversationId)
    {
        var userId = GetCurrentUserId();
        await Clients.Others.SendAsync("UserTyping", new { UserId = userId, ConversationId = conversationId });
    }

    public async Task StopTyping(Guid conversationId)
    {
        var userId = GetCurrentUserId();
        await Clients.Others.SendAsync("UserStoppedTyping", new { UserId = userId, ConversationId = conversationId });
    }

    public async Task NotifyMessageDelivered(Guid messageId, Guid receiverId, DateTime deliveredAt)
    {
        if (_connectionMapping.TryGetConnection(receiverId, out var connectionId))
        {
            await Clients.Client(connectionId).SendAsync("MessageDelivered", new { MessageId = messageId, DeliveredAt = deliveredAt });
        }
    }

    public async Task NotifyMessageRead(Guid messageId, Guid senderId, DateTime readAt)
    {
        if (_connectionMapping.TryGetConnection(senderId, out var connectionId))
        {
            await Clients.Client(connectionId).SendAsync("MessageRead", new { MessageId = messageId, ReadAt = readAt });
        }
    }

    private Guid GetCurrentUserId()
    {
        var sub = Context.User?.FindFirst("sub")?.Value ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId))
            throw new HubException("Unauthorized");
        return userId;
    }

}
