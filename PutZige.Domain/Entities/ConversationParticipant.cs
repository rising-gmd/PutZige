#nullable enable
using System;

namespace PutZige.Domain.Entities;

/// <summary>
/// Many-to-many junction between Conversation and User.
/// Stores per-user conversation settings.
/// </summary>
public class ConversationParticipant : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }

    // Per-user settings
    public bool IsPinned { get; set; } = false;
    public bool IsMuted { get; set; } = false;
    public DateTime? LastReadAt { get; set; }

    // Navigation properties
    public Conversation Conversation { get; set; } = null!;
    public User User { get; set; } = null!;
}
