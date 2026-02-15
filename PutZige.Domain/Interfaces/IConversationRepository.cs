#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using PutZige.Domain.Entities;

namespace PutZige.Domain.Interfaces;

public interface IConversationRepository : IRepository<Conversation>
{
    /// <summary>
    /// Get or create a 1-on-1 conversation between two users.
    /// </summary>
    Task<Conversation> GetOrCreateDirectConversationAsync(
        Guid userId1, 
        Guid userId2, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Get conversation by ID with participants eager-loaded.
    /// </summary>
    Task<Conversation?> GetByIdWithParticipantsAsync(
        Guid conversationId, 
        CancellationToken ct = default);
}
