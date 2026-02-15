#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PutZige.Domain.Entities;
using PutZige.Domain.Interfaces;
using PutZige.Infrastructure.Data;

namespace PutZige.Infrastructure.Repositories;

public class ConversationRepository : Repository<Conversation>, IConversationRepository
{
    public ConversationRepository(AppDbContext context) : base(context) { }

    public async Task<Conversation> GetOrCreateDirectConversationAsync(
        Guid userId1, 
        Guid userId2, 
        CancellationToken ct = default)
    {
        // Find existing conversation between these two users
        var existing = await _dbSet
            .Include(c => c.Participants)
            .Where(c => !c.IsGroup && !c.IsDeleted)
            .Where(c => c.Participants.Count == 2)
            .Where(c => c.Participants.Any(p => p.UserId == userId1))
            .Where(c => c.Participants.Any(p => p.UserId == userId2))
            .FirstOrDefaultAsync(ct);

        if (existing != null)
            return existing;

        // Create new conversation
        var conversation = new Conversation
        {
            IsGroup = false,
            LastActivity = DateTime.UtcNow,
            Participants = new[]
            {
                new ConversationParticipant { UserId = userId1 },
                new ConversationParticipant { UserId = userId2 }
            }
        };

        await _dbSet.AddAsync(conversation, ct);
        await _context.SaveChangesAsync(ct);

        return conversation;
    }

    public async Task<Conversation?> GetByIdWithParticipantsAsync(
        Guid conversationId, 
        CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(c => c.Id == conversationId && !c.IsDeleted, ct)
            .ConfigureAwait(false);
    }
}
