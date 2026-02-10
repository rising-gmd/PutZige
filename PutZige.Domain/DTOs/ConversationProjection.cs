#nullable enable
using System;

namespace PutZige.Domain.DTOs
{
    public sealed class ConversationProjection
    {
        public required Guid UserId { get; init; }
        public required string Username { get; init; }
        public string? DisplayName { get; init; }
        public string? ProfilePictureUrl { get; init; }
        public bool IsOnline { get; init; }

        // Last message
        public Guid? LastMessageId { get; init; }
        public Guid? LastMessageSenderId { get; init; }
        public Guid? LastMessageReceiverId { get; init; }
        public string? LastMessageText { get; init; }
        public DateTime? LastMessageSentAt { get; init; }
        public DateTime? LastMessageDeliveredAt { get; init; }
        public DateTime? LastMessageReadAt { get; init; }
        public int UnreadCount { get; init; }
        public DateTime? LastActivity { get; init; }
    }
}