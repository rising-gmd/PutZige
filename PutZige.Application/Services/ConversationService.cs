#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PutZige.Application.DTOs.Messaging;
using PutZige.Application.Interfaces;
using PutZige.Application.Common.Messages;
using PutZige.Domain.Interfaces;

namespace PutZige.Application.Services;

/// <summary>
/// Service for managing conversations between users.
/// Handles conversation creation, retrieval, and participant validation.
/// </summary>
public sealed class ConversationService : IConversationService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRealTimeNotifier _realTimeNotifier;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IRealTimeNotifier realTimeNotifier,
        ILogger<ConversationService> logger)
    {
        _conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _realTimeNotifier = realTimeNotifier ?? throw new ArgumentNullException(nameof(realTimeNotifier));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ConversationDto> GetOrCreateDirectConversationAsync(
        Guid otherUserId,
        CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.GetUserId();

        // Validate: Can't create conversation with self
        if (currentUserId == otherUserId)
            throw new ArgumentException("Cannot create conversation with yourself");

        // Validate other user exists
        var otherUser = await _userRepository.GetByIdAsync(otherUserId, ct).ConfigureAwait(false);
        if (otherUser == null)
            throw new KeyNotFoundException(ErrorMessages.Users.UserNotFound);

        // Get or create conversation
        var conversation = await _conversationRepository
            .GetOrCreateDirectConversationAsync(currentUserId, otherUserId, ct)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Conversation retrieved/created - ConversationId: {ConversationId}, Users: {User1}, {User2}",
            conversation.Id, currentUserId, otherUserId);

        var dto = new ConversationDto
        {
            ConversationId = conversation.Id,
            UserId = otherUser.Id,
            Username = otherUser.Username,
            DisplayName = otherUser.DisplayName,
            ProfilePictureUrl = otherUser.ProfilePictureUrl,
            IsOnline = false,
            LastMessage = null,
            UnreadCount = 0,
            LastActivity = conversation.LastActivity
        };

        await _realTimeNotifier.TryNotifyConversationCreatedAsync(otherUserId, dto).ConfigureAwait(false);
        return dto;
    }

    public async Task<ConversationResponse> GetConversationByIdAsync(
        Guid conversationId,
        CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.GetUserId();

        var conversation = await _conversationRepository
            .GetByIdWithParticipantsAsync(conversationId, ct)
            .ConfigureAwait(false);

        if (conversation == null)
            throw new KeyNotFoundException("Conversation not found");

        // Verify current user is a participant
        if (!conversation.Participants.Any(p => p.UserId == currentUserId))
            throw new UnauthorizedAccessException("Not a participant in this conversation");

        var otherParticipant = conversation.Participants
            .FirstOrDefault(p => p.UserId != currentUserId);

        return new ConversationResponse
        {
            ConversationId = conversation.Id,
            IsGroup = conversation.IsGroup,
            LastActivity = conversation.LastActivity,
            OtherUserId = otherParticipant?.UserId ?? Guid.Empty,
            OtherUserDisplayName = otherParticipant?.User?.DisplayName ?? otherParticipant?.User?.Username
        };
    }
}
