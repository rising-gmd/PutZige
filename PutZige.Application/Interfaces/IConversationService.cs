#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using PutZige.Application.DTOs.Messaging;

namespace PutZige.Application.Interfaces;

/// <summary>
/// Service for conversation management operations.
/// </summary>
public interface IConversationService
{
    /// <summary>
    /// Gets an existing conversation or creates a new one between current user and another user.
    /// </summary>
    /// <param name="otherUserId">The other user's ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Conversation details</returns>
    /// <exception cref="KeyNotFoundException">When other user not found</exception>
    Task<ConversationDto> GetOrCreateDirectConversationAsync(
        Guid otherUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets conversation details by ID (with authorization check).
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Conversation details</returns>
    /// <exception cref="KeyNotFoundException">When conversation not found</exception>
    /// <exception cref="UnauthorizedAccessException">When user is not a participant</exception>
    Task<ConversationResponse> GetConversationByIdAsync(
        Guid conversationId,
        CancellationToken ct = default);
}
