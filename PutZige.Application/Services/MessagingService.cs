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
using PutZige.Domain.Entities;
using PutZige.Domain.Interfaces;

namespace PutZige.Application.Services
{
    public class MessagingService : IMessagingService
    {
        private readonly IDapperMessageRepository? _dapperMessageRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<MessagingService>? _logger;
        private readonly IRealTimeNotifier _realTimeNotifier;
        private readonly ICurrentUserService _currentUserService;
        private readonly IDateTimeProvider _dateTimeProvider;

        public MessagingService(
            IMessageRepository messageRepository,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IRealTimeNotifier realTimeNotifier,
            ICurrentUserService currentUserService,
            IDateTimeProvider dateTimeProvider,
            ILogger<MessagingService>? logger = null,
            IDapperMessageRepository? dapperMessageRepository = null)
        {
            ArgumentNullException.ThrowIfNull(messageRepository);
            ArgumentNullException.ThrowIfNull(userRepository);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(realTimeNotifier);
            ArgumentNullException.ThrowIfNull(currentUserService);
            ArgumentNullException.ThrowIfNull(dateTimeProvider);

            _messageRepository = messageRepository;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _realTimeNotifier = realTimeNotifier;
            _currentUserService = currentUserService;
            _dateTimeProvider = dateTimeProvider;
            _logger = logger;
            _dapperMessageRepository = dapperMessageRepository;
        }

        public async Task<ConversationListResponse> GetConversationsAsync(CancellationToken ct = default)
        {
            var userId = _currentUserService.GetUserId();

            if (_dapperMessageRepository == null)
            {
                return new ConversationListResponse { Conversations = new(), TotalCount = 0 };
            }

            var projections = await _dapperMessageRepository
                .GetConversationsForUserAsync(userId, 100, ct)
                .ConfigureAwait(false);

            var conversations = projections.Select(p => new ConversationDto
            {
                UserId = p.UserId,
                Username = p.Username,
                DisplayName = p.DisplayName,
                ProfilePictureUrl = p.ProfilePictureUrl,
                IsOnline = p.IsOnline,
                LastMessage = MapLastMessage(p),
                UnreadCount = p.UnreadCount,
                LastActivity = p.LastActivity
            }).ToList();

            return new ConversationListResponse
            {
                Conversations = conversations,
                TotalCount = conversations.Count
            };
        }

        public async Task<SendMessageResponse> SendMessageAsync(
            Guid receiverId,
            string messageText,
            CancellationToken ct = default)
        {
            var senderId = _currentUserService.GetUserId();

            ValidateMessageRequest(senderId, receiverId, messageText);

            await ValidateUsersExistAsync(senderId, receiverId, ct);

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                MessageText = messageText,
                SentAt = _dateTimeProvider.UtcNow
            };

            await _messageRepository.AddAsync(message, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            _logger?.LogInformation(
                "Message sent - MessageId: {MessageId} SenderId: {SenderId} ReceiverId: {ReceiverId}",
                message.Id, senderId, receiverId);

            return _mapper.Map<SendMessageResponse>(message);
        }

        public async Task<ConversationHistoryResponse> GetConversationHistoryAsync(
            Guid otherUserId,
            int pageNumber,
            int pageSize,
            CancellationToken ct = default)
        {
            var userId = _currentUserService.GetUserId();

            ValidateConversationHistoryRequest(otherUserId, pageNumber, pageSize);

            var (messages, totalCount) = await _messageRepository
                .GetConversationAsync(userId, otherUserId, pageNumber, pageSize, ct);

            if (totalCount == 0)
            {
                throw new AppException(ResponseCodes.NOT_FOUND, "Conversation not found");
            }

            var messageDtos = messages.Select(m => _mapper.Map<MessageDto>(m)).ToList();

            return new ConversationHistoryResponse
            {
                Messages = messageDtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task MarkMessageAsDeliveredAsync(Guid messageId, CancellationToken ct = default)
        {
            if (messageId == Guid.Empty)
            {
                throw new AppException(ResponseCodes.MESSAGE_NOT_FOUND, ErrorMessages.Messaging.MessageNotFound);
            }

            var message = await _messageRepository.GetByIdAsync(messageId, ct);
            if (message == null)
            {
                throw new KeyNotFoundException(ErrorMessages.Messaging.MessageNotFound);
            }

            message.DeliveredAt = _dateTimeProvider.UtcNow;
            await _messageRepository.UpdateAsync(message, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            _logger?.LogInformation("Message delivered - MessageId: {MessageId}", messageId);

            await _realTimeNotifier.TryNotifyMessageDeliveredAsync(
                message.SenderId,
                messageId,
                message.DeliveredAt.Value);
        }

        public async Task MarkMessageAsReadAsync(Guid messageId, CancellationToken ct = default)
        {
            if (messageId == Guid.Empty)
            {
                throw new AppException(ResponseCodes.MESSAGE_NOT_FOUND, ErrorMessages.Messaging.MessageNotFound);
            }

            var message = await _messageRepository.GetByIdAsync(messageId, ct);
            if (message == null)
            {
                throw new KeyNotFoundException(ErrorMessages.Messaging.MessageNotFound);
            }

            message.ReadAt = _dateTimeProvider.UtcNow;
            await _messageRepository.UpdateAsync(message, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            _logger?.LogInformation("Message read - MessageId: {MessageId}", messageId);

            await _realTimeNotifier.TryNotifyMessageReadAsync(
                message.SenderId,
                messageId,
                message.ReadAt.Value);
        }

        // Private helper methods

        private static void ValidateMessageRequest(Guid senderId, Guid receiverId, string messageText)
        {
            if (senderId == Guid.Empty)
            {
                throw new AppException(ResponseCodes.SENDER_ID_REQUIRED, ErrorMessages.Messaging.SenderIdRequired);
            }

            if (receiverId == Guid.Empty)
            {
                throw new AppException(ResponseCodes.RECEIVER_ID_REQUIRED, ErrorMessages.Messaging.ReceiverIdRequired);
            }

            if (string.IsNullOrWhiteSpace(messageText))
            {
                throw new AppException(ResponseCodes.MESSAGE_TEXT_REQUIRED, ErrorMessages.Messaging.MessageTextRequired);
            }

            if (messageText.Length > AppConstants.Messaging.MaxMessageLength)
            {
                throw new AppException(ResponseCodes.MESSAGE_TOO_LONG, ErrorMessages.Messaging.MessageTooLong);
            }
        }

        private async Task ValidateUsersExistAsync(Guid senderId, Guid receiverId, CancellationToken ct)
        {
            var sender = await _userRepository.GetByIdAsync(senderId, ct);
            if (sender == null)
            {
                throw new KeyNotFoundException(ErrorMessages.Messaging.SenderNotFound);
            }

            var receiver = await _userRepository.GetByIdAsync(receiverId, ct);
            if (receiver == null)
            {
                throw new KeyNotFoundException(ErrorMessages.Messaging.ReceiverNotFound);
            }
        }

        private static void ValidateConversationHistoryRequest(Guid otherUserId, int pageNumber, int pageSize)
        {
            if (otherUserId == Guid.Empty)
            {
                throw new AppException(ResponseCodes.RECEIVER_ID_REQUIRED, ErrorMessages.Messaging.ReceiverIdRequired);
            }

            if (pageNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pageNumber),
                    ErrorMessages.Messaging.PageNumberOutOfRange);
            }

            if (pageSize <= 0 || pageSize > AppConstants.Messaging.MaxPageSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pageSize),
                    ErrorMessages.Messaging.PageSizeOutOfRange);
            }
        }

        private MessageDto? MapLastMessage(dynamic projection)
        {
            if (!projection.LastMessageId.HasValue)
            {
                return null;
            }

            return new MessageDto
            {
                Id = projection.LastMessageId.Value,
                SenderId = projection.LastMessageSenderId ?? Guid.Empty,
                ReceiverId = projection.LastMessageReceiverId ?? Guid.Empty,
                MessageText = projection.LastMessageText ?? string.Empty,
                SentAt = projection.LastMessageSentAt ?? _dateTimeProvider.UtcNow,
                DeliveredAt = projection.LastMessageDeliveredAt,
                ReadAt = projection.LastMessageReadAt
            };
        }
    }
}