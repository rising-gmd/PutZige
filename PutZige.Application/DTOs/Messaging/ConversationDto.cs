using System;

namespace PutZige.Application.DTOs.Messaging;

public sealed class ConversationDto
{
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string? ProfilePictureUrl { get; init; }
    public bool IsOnline { get; init; }

    public MessageDto? LastMessage { get; init; }
    public int UnreadCount { get; init; }
    public DateTime? LastActivity { get; init; }
}
