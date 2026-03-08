using System.Text.Json.Serialization;

namespace PutZige.Application.DTOs.Messaging;

public record EditMessageRequest
{
    [JsonPropertyName("messageText")]
    public string MessageText { get; init; } = string.Empty;
}
