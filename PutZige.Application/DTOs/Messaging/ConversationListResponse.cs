using System.Collections.Generic;

namespace PutZige.Application.DTOs.Messaging;

public sealed class ConversationListResponse
{
    public List<ConversationDto> Conversations { get; init; } = new();
    public int TotalCount { get; init; }
}
