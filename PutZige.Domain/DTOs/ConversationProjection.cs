#nullable enable
using System;

namespace PutZige.Domain.DTOs
{
    /// <summary>
    /// Dapper projection for a single row returned by
    /// <c>MessageQueries.GET_CONVERSATIONS_FOR_USER</c>.
    ///
    /// Column names must match the SQL SELECT aliases exactly
    /// (Dapper maps by name, case-insensitive).
    /// </summary>
    public sealed class ConversationProjection
    {
        // ── Participant ──────────────────────────────────────────────────────
        public Guid UserId { get; init; }
        public string Username { get; init; } = string.Empty;
        public string? DisplayName { get; init; }
        public string? ProfilePictureUrl { get; init; }
        public bool IsOnline { get; init; }

        // ── Last message (all nullable — conversation may have no messages yet) ──
        public Guid? LastMessageId { get; init; }
        public Guid? LastMessageSenderId { get; init; }
        public Guid? LastMessageReceiverId { get; init; }
        public string? LastMessageText { get; init; }
        public DateTime? LastMessageSentAt { get; init; }
        public DateTime? LastMessageDeliveredAt { get; init; }
        public DateTime? LastMessageReadAt { get; init; }

        // ── Metadata ─────────────────────────────────────────────────────────
        public long UnreadCount { get; init; }
        public DateTime LastActivity { get; init; }
    }
}