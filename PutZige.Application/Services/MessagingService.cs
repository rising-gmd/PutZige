#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging;
using PutZige.Application.Common;
using PutZige.Application.Common.Constants;
using PutZige.Application.Common.Messages;
using PutZige.Application.DTOs.Messaging;
using PutZige.Application.Interfaces;
using PutZige.Domain.DTOs;
using PutZige.Domain.Entities;
using PutZige.Domain.Interfaces;

namespace PutZige.Application.Services
{
    public class MessagingService : IMessagingService
    {
        private readonly IDapperMessageRepository _dapperMessageRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IUserRepository _userRepository;
        private readonly IConversationRepository _conversationRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<MessagingService> _logger;
        private readonly IRealTimeNotifier _realTimeNotifier;
        private readonly ICurrentUserService _currentUserService;
        private readonly IDateTimeProvider _dateTimeProvider;

        public MessagingService(
            IDapperMessageRepository dapperMessageRepository,
            IMessageRepository messageRepository,
            IUserRepository userRepository,
            IConversationRepository conversationRepository,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IRealTimeNotifier realTimeNotifier,
            ICurrentUserService currentUserService,
            IDateTimeProvider dateTimeProvider,
            ILogger<MessagingService> logger)
        {
            _dapperMessageRepository = dapperMessageRepository ?? throw new ArgumentNullException(nameof(dapperMessageRepository));
            _messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _realTimeNotifier = realTimeNotifier ?? throw new ArgumentNullException(nameof(realTimeNotifier));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ── Conversations ────────────────────────────────────────────────────

        public async Task<ConversationListResponse> GetConversationsAsync(CancellationToken ct = default)
        {
            var userId = _currentUserService.GetUserId();

            var projections = await _dapperMessageRepository
                .GetConversationsForUserAsync(userId, AppConstants.Messaging.ConversationListLimit, ct)
                .ConfigureAwait(false);

            var conversations = projections
                .Select(p => new ConversationDto
                {
                    UserId = p.UserId,
                    ConversationId = p.ConversationId,
                    Username = p.Username,
                    DisplayName = p.DisplayName,
                    ProfilePictureUrl = p.ProfilePictureUrl,
                    IsOnline = p.IsOnline,
                    LastMessage = MapLastMessage(p),
                    UnreadCount = p.UnreadCount,
                    LastActivity = p.LastActivity
                })
                .ToList();

            return new ConversationListResponse
            {
                Conversations = conversations,
                TotalCount = conversations.Count
            };
        }

        // ── Send message ─────────────────────────────────────────────────────

        public async Task<SendMessageResponse> SendMessageAsync(
            Guid conversationId,
            string messageText,
            Guid senderId,
            CancellationToken ct = default)
        {
            var conversation = await _conversationRepository
                .GetByIdWithParticipantsAsync(conversationId, ct)
                .ConfigureAwait(false);
            if (conversation == null)
                throw new KeyNotFoundException("Conversation not found");

            if (!conversation.Participants.Any(p => p.UserId == senderId))
                throw new UnauthorizedAccessException("Not a participant");

            var receiverId = conversation.Participants.First(p => p.UserId != senderId).UserId;

            ValidateMessageRequest(senderId, receiverId, messageText);

            var message = new Message
            {
                ConversationId = conversationId,
                SenderId = senderId,
                ReceiverId = receiverId,
                MessageText = messageText,
                SentAt = _dateTimeProvider.UtcNow
            };

            await _messageRepository.AddAsync(message, ct).ConfigureAwait(false);

            // Update conversation LastActivity
            conversation.LastActivity = message.SentAt;
            _conversationRepository.Update(conversation);

            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Message sent - MessageId: {MessageId} ConversationId: {ConversationId}",
                message.Id, conversationId);

            return _mapper.Map<SendMessageResponse>(message);
        }

        // SendMessageAsAsync removed: use SendMessageAsync (use current user) and let callers pass conversationId

        // ── Conversation history ─────────────────────────────────────────────

        public async Task<ConversationHistoryResponse> GetConversationHistoryAsync(
            Guid conversationId,
            int pageNumber,
            int pageSize,
            CancellationToken ct = default)
        {
            var userId = _currentUserService.GetUserId();

            // Verify user is participant
            var conversation = await _conversationRepository.GetByIdWithParticipantsAsync(conversationId, ct).ConfigureAwait(false);
            if (conversation == null || !conversation.Participants.Any(p => p.UserId == userId))
                throw new UnauthorizedAccessException("Not authorized");

            ValidateConversationHistoryRequest(conversationId, pageNumber, pageSize);

            var (projections, totalCount) = await _dapperMessageRepository
                .GetConversationHistoryAsync(conversationId, pageNumber, pageSize, ct)
                .ConfigureAwait(false);

            var messageDtos = projections
                .Select(p => new MessageDto
                {
                    Id = p.Id,
                    SenderId = p.SenderId,
                    SenderUsername = p.SenderUsername ?? string.Empty,
                    ReceiverId = p.ReceiverId,
                    ReceiverUsername = p.ReceiverUsername ?? string.Empty,
                    MessageText = p.MessageText,
                    SentAt = p.SentAt,
                    DeliveredAt = p.DeliveredAt,
                    ReadAt = p.ReadAt,
                    IsForwarded = p.IsForwarded,
                    IsEdited = p.IsEdited,
                    EditedAt = p.EditedAt,
                    IsDeleted = p.IsDeleted,
                    ReplyToId = p.ReplyToId,
                    ReplyToText = p.ReplyToText,
                    ReplyToSenderName = p.ReplyToSenderName
                })
                .ToList();

            return new ConversationHistoryResponse
            {
                Messages = messageDtos,
                TotalCount = (int)totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        // ── Delivery / read receipts ─────────────────────────────────────────

        public async Task MarkMessageAsDeliveredAsync(Guid messageId, CancellationToken ct = default)
        {
            var message = await GetMessageOrThrowAsync(messageId, ct).ConfigureAwait(false);

            message.DeliveredAt = _dateTimeProvider.UtcNow;
            await _messageRepository.UpdateAsync(message, ct).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation("Message delivered - MessageId: {MessageId}", messageId);

            await _realTimeNotifier
                .TryNotifyMessageDeliveredAsync(message.SenderId, messageId, message.DeliveredAt.Value)
                .ConfigureAwait(false);
        }

        public async Task MarkMessageAsReadAsync(Guid messageId, CancellationToken ct = default)
        {
            var message = await GetMessageOrThrowAsync(messageId, ct).ConfigureAwait(false);

            message.ReadAt = _dateTimeProvider.UtcNow;
            await _messageRepository.UpdateAsync(message, ct).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation("Message read - MessageId: {MessageId}", messageId);

            await _realTimeNotifier
                .TryNotifyMessageReadAsync(message.SenderId, messageId, message.ReadAt.Value)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Marks all messages in a direct 1-on-1 conversation as read for the current user.
        /// </summary>
        public async Task MarkConversationAsReadAsync(Guid conversationId, CancellationToken ct = default)
        {
            var currentUserId = _currentUserService.GetUserId();

            var conversation = await _conversationRepository
                .GetByIdWithParticipantsAsync(conversationId, ct)
                .ConfigureAwait(false);

            if (conversation == null)
                throw new KeyNotFoundException("Conversation not found");

            if (!conversation.Participants.Any(p => p.UserId == currentUserId))
                throw new UnauthorizedAccessException("Not a participant");

            var otherParticipant = conversation.Participants.First(p => p.UserId != currentUserId);
            var otherUserId = otherParticipant.UserId;

            var rowsUpdated = await _dapperMessageRepository
                .MarkConversationAsReadAsync(currentUserId, otherUserId, ct)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Marked conversation as read - ConversationId: {ConversationId} RowsUpdated: {RowsUpdated}",
                conversationId, rowsUpdated);
        }

        // ── Private helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Fetches a message by id, throwing typed exceptions on empty id or missing record.
        /// Extracted to avoid duplicate guard logic across Delivered/Read methods.
        /// </summary>
        private async Task<Message> GetMessageOrThrowAsync(Guid messageId, CancellationToken ct)
        {
            if (messageId == Guid.Empty)
                throw new AppException(ResponseCodes.MESSAGE_NOT_FOUND, ErrorMessages.Messaging.MessageNotFound);

            var message = await _messageRepository.GetByIdAsync(messageId, ct).ConfigureAwait(false);
            if (message is null)
                throw new KeyNotFoundException(ErrorMessages.Messaging.MessageNotFound);

            return message;
        }

        private static void ValidateMessageRequest(Guid senderId, Guid receiverId, string messageText)
        {
            if (senderId == Guid.Empty)
                throw new AppException(ResponseCodes.SENDER_ID_REQUIRED, ErrorMessages.Messaging.SenderIdRequired);

            if (receiverId == Guid.Empty)
                throw new AppException(ResponseCodes.RECEIVER_ID_REQUIRED, ErrorMessages.Messaging.ReceiverIdRequired);

            if (string.IsNullOrWhiteSpace(messageText))
                throw new AppException(ResponseCodes.MESSAGE_TEXT_REQUIRED, ErrorMessages.Messaging.MessageTextRequired);

            if (messageText.Length > AppConstants.Messaging.MaxMessageLength)
                throw new AppException(ResponseCodes.MESSAGE_TOO_LONG, ErrorMessages.Messaging.MessageTooLong);
        }

        /// <summary>
        /// Validates both users exist in parallel — avoids two sequential DB round-trips.
        /// </summary>
        private async Task ValidateUsersExistAsync(Guid senderId, Guid receiverId, CancellationToken ct)
        {
            var senderTask = _userRepository.GetByIdAsync(senderId, ct);
            var receiverTask = _userRepository.GetByIdAsync(receiverId, ct);

            await Task.WhenAll(senderTask, receiverTask).ConfigureAwait(false);

            if (senderTask.Result is null) throw new KeyNotFoundException(ErrorMessages.Messaging.SenderNotFound);
            if (receiverTask.Result is null) throw new KeyNotFoundException(ErrorMessages.Messaging.ReceiverNotFound);
        }

        private static void ValidateConversationHistoryRequest(Guid otherUserId, int pageNumber, int pageSize)
        {
            if (otherUserId == Guid.Empty)
                throw new AppException(ResponseCodes.RECEIVER_ID_REQUIRED, ErrorMessages.Messaging.ReceiverIdRequired);

            if (pageNumber <= 0)
                throw new ArgumentOutOfRangeException(nameof(pageNumber), ErrorMessages.Messaging.PageNumberOutOfRange);

            if (pageSize <= 0 || pageSize > AppConstants.Messaging.MaxPageSize)
                throw new ArgumentOutOfRangeException(nameof(pageSize), ErrorMessages.Messaging.PageSizeOutOfRange);
        }

        /// <summary>
        /// Maps last-message fields from a strongly-typed <see cref="ConversationProjection"/>.
        /// Previously used <c>dynamic</c> which bypassed compile-time checks entirely.
        /// </summary>
        private static MessageDto? MapLastMessage(ConversationProjection p)
        {
            if (p.LastMessageId is null) return null;

            return new MessageDto
            {
                Id = p.LastMessageId.Value,
                SenderId = p.LastMessageSenderId ?? Guid.Empty,
                ReceiverId = p.LastMessageReceiverId ?? Guid.Empty,
                MessageText = p.LastMessageText ?? string.Empty,
                SentAt = p.LastMessageSentAt ?? DateTime.UtcNow,
                DeliveredAt = p.LastMessageDeliveredAt,
                ReadAt = p.LastMessageReadAt
            };
        }
    }
}