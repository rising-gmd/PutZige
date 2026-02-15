using System;

namespace PutZige.Application.DTOs.Messaging;

public sealed class ConversationDto
{
    // Conversation identifier for explicit conversation model
    public Guid ConversationId { get; init; }
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string? ProfilePictureUrl { get; init; }
    public bool IsOnline { get; init; }

    public MessageDto? LastMessage { get; init; }
    public long UnreadCount { get; init; }
    public DateTime? LastActivity { get; init; }
}
