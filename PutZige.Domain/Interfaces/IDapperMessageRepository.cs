using System.Collections.Generic;
using System.Threading;

namespace PutZige.Domain.Interfaces
{
    public interface IDapperMessageRepository
    {
        Task<IEnumerable<PutZige.Domain.DTOs.ConversationProjection>> GetConversationsForUserAsync(System.Guid userId, int limit, CancellationToken ct = default);
    }
}
