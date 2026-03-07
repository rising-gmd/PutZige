#nullable enable
using System;

namespace PutZige.Domain.DTOs
{
    /// <summary>
    /// Lightweight message projection for Dapper queries.
    /// Avoids full entity hydration for read-only operations.
    /// </summary>
    public sealed class MessageProjection
    {
        public required Guid Id { get; init; }
        public required Guid SenderId { get; init; }
        public required Guid ReceiverId { get; init; }
        public required string MessageText { get; init; }
        public required DateTime SentAt { get; init; }
        public DateTime? DeliveredAt { get; init; }
        public DateTime? ReadAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public bool IsDeleted { get; init; }
        
        // Denormalized user data for display
        public string? SenderUsername { get; init; }
        public string? ReceiverUsername { get; init; }
        public bool IsForwarded { get; init; }
        public Guid? ReplyToId { get; init; }
        public string? ReplyToText { get; init; }
        public string? ReplyToSenderName { get; init; }
        public bool IsEdited { get; init; }
        public DateTime? EditedAt { get; init; }
    }
}
