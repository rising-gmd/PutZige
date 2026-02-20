using System;
using System.Text.Json.Serialization;
namespace PutZige.Application.DTOs.Messaging;

public record SendMessageResponse
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
    public string MessageText { get; init; }

    [JsonPropertyName("sentAt")]
    public DateTime SentAt { get; init; }
}