#nullable enable
using System;
using System.Text.Json.Serialization;

namespace PutZige.Application.DTOs.Messaging;

public record ReceiveMessagePayload
{
    [JsonPropertyName("messageId")]
    public Guid MessageId { get; init; }

    [JsonPropertyName("conversationId")]
    public Guid ConversationId { get; init; }

    [JsonPropertyName("senderId")]
    public Guid SenderId { get; init; }

    [JsonPropertyName("receiverId")]
    public Guid ReceiverId { get; init; }

    [JsonPropertyName("messageText")]
    public string MessageText { get; init; } = string.Empty;

    [JsonPropertyName("sentAt")]
    public DateTime SentAt { get; init; }

    [JsonPropertyName("unreadCount")]
    public int UnreadCount { get; init; }
}
