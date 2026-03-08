using System;
using System.Collections.Generic;

namespace PutZige.Application.DTOs.Messaging;

public record MessageDto
{
    public Guid Id { get; init; }
    public Guid SenderId { get; init; }
    public string SenderUsername { get; init; } = string.Empty;
    public Guid ReceiverId { get; init; }
    public string ReceiverUsername { get; init; } = string.Empty;
    public string MessageText { get; init; } = string.Empty;
    public DateTime SentAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public DateTime? ReadAt { get; init; }
    public bool IsRead => ReadAt.HasValue;
    public bool IsForwarded { get; init; }
    public Guid? ReplyToId { get; init; }
    public string? ReplyToText { get; init; }
    public string? ReplyToSenderName { get; init; }
    public bool IsEdited { get; init; }
    public DateTime? EditedAt { get; init; }
    public bool IsDeleted { get; init; }
    public bool IsStarred { get; init; }
    public List<MessageAttachmentDto> Attachments { get; init; } = new();
}
