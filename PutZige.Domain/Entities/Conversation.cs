#nullable enable
using System;
using System.Collections.Generic;

namespace PutZige.Domain.Entities;

/// <summary>
/// Represents a conversation between 2+ users.
/// Supports both 1-on-1 and group chats.
/// </summary>
public class Conversation : BaseEntity
{
    public bool IsGroup { get; set; } = false;
    public DateTime LastActivity { get; set; }

    // Navigation properties
    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
