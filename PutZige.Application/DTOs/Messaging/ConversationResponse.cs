#nullable enable
using System;

namespace PutZige.Application.DTOs.Messaging;

/// <summary>
/// Response containing conversation details.
/// </summary>
public sealed record ConversationResponse
{
    public Guid ConversationId { get; init; }
    public bool IsGroup { get; init; }
    public DateTime LastActivity { get; init; }
    public Guid OtherUserId { get; init; }
    public string? OtherUserDisplayName { get; init; }
}
