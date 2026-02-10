#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging;
using PutZige.Application.Common.Messages;
using PutZige.Application.Common.Constants;
using PutZige.Application.Common;
using PutZige.Application.DTOs.Messaging;
using PutZige.Application.Interfaces;
using PutZige.Domain.Entities;
using PutZige.Domain.Interfaces;

namespace PutZige.Application.Services;

public class MessagingService : IMessagingService
{
        private readonly PutZige.Domain.Interfaces.IDapperMessageRepository? _dapperMessageRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<MessagingService>? _logger;
    private readonly PutZige.Application.Interfaces.IRealTimeNotifier _realTimeNotifier;

    public MessagingService(IMessageRepository messageRepository, IUserRepository userRepository, IUnitOfWork unitOfWork, IMapper mapper, PutZige.Application.Interfaces.IRealTimeNotifier realTimeNotifier, ILogger<MessagingService>? logger = null, PutZige.Domain.Interfaces.IDapperMessageRepository? dapperMessageRepository = null)
    {
        ArgumentNullException.ThrowIfNull(messageRepository);
        ArgumentNullException.ThrowIfNull(userRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(mapper);

        _messageRepository = messageRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _realTimeNotifier = realTimeNotifier ?? throw new ArgumentNullException(nameof(realTimeNotifier));
        _logger = logger;
            _dapperMessageRepository = dapperMessageRepository;
            _logger = logger;
        }

        public async Task<ConversationListResponse> GetConversationsAsync(Guid userId, CancellationToken ct = default)
        {
            if (userId == Guid.Empty) throw new AppException(ResponseCodes.SENDER_ID_REQUIRED, ErrorMessages.Messaging.SenderIdRequired);

            if (_dapperMessageRepository == null)
            {
                return new ConversationListResponse { Conversations = new(), TotalCount = 0 };
            }

            var projections = await _dapperMessageRepository.GetConversationsForUserAsync(userId, 100, ct).ConfigureAwait(false);

            var convs = projections.Select(p => new ConversationDto
            {
                UserId = p.UserId,
                Username = p.Username,
                DisplayName = p.DisplayName,
                ProfilePictureUrl = p.ProfilePictureUrl,
                IsOnline = p.IsOnline,
                LastMessage = p.LastMessageId.HasValue ? new MessageDto
                {
                    Id = p.LastMessageId.Value,
                    SenderId = p.LastMessageSenderId ?? Guid.Empty,
                    ReceiverId = p.LastMessageReceiverId ?? Guid.Empty,
                    MessageText = p.LastMessageText ?? string.Empty,
                    SentAt = p.LastMessageSentAt ?? DateTime.UtcNow,
                    DeliveredAt = p.LastMessageDeliveredAt,
                    ReadAt = p.LastMessageReadAt
                } : null,
                UnreadCount = p.UnreadCount,
                LastActivity = p.LastActivity
            }).ToList();

            return new ConversationListResponse { Conversations = convs, TotalCount = convs.Count };
        }

    public async Task<SendMessageResponse> SendMessageAsync(Guid senderId, Guid receiverId, string messageText, CancellationToken ct = default)
    {
        if (senderId == Guid.Empty) throw new AppException(ResponseCodes.SENDER_ID_REQUIRED, ErrorMessages.Messaging.SenderIdRequired);

        if (receiverId == Guid.Empty) throw new AppException(ResponseCodes.RECEIVER_ID_REQUIRED, ErrorMessages.Messaging.ReceiverIdRequired);

        if (string.IsNullOrWhiteSpace(messageText)) throw new AppException(ResponseCodes.MESSAGE_TEXT_REQUIRED, ErrorMessages.Messaging.MessageTextRequired);

        if (messageText.Length > AppConstants.Messaging.MaxMessageLength) throw new AppException(ResponseCodes.MESSAGE_TOO_LONG, ErrorMessages.Messaging.MessageTooLong);
        if (senderId == Guid.Empty) throw new AppException(ResponseCodes.SENDER_ID_REQUIRED, ErrorMessages.Messaging.SenderIdRequired);
        if (receiverId == Guid.Empty) throw new AppException(ResponseCodes.RECEIVER_ID_REQUIRED, ErrorMessages.Messaging.ReceiverIdRequired);
        if (string.IsNullOrWhiteSpace(messageText)) throw new AppException(ResponseCodes.MESSAGE_TEXT_REQUIRED, ErrorMessages.Messaging.MessageTextRequired);
        if (messageText.Length > AppConstants.Messaging.MaxMessageLength) throw new AppException(ResponseCodes.MESSAGE_TOO_LONG, ErrorMessages.Messaging.MessageTooLong);

        // Validate sender exists
        var sender = await _userRepository.GetByIdAsync(senderId, ct);

        if (sender == null) throw new KeyNotFoundException(ErrorMessages.Messaging.SenderNotFound);

        // Validate receiver exists
        var receiver = await _userRepository.GetByIdAsync(receiverId, ct);

        if (receiver == null) throw new KeyNotFoundException(ErrorMessages.Messaging.ReceiverNotFound);

        var message = new Message
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            MessageText = messageText,
            SentAt = DateTime.UtcNow
        };

        await _messageRepository.AddAsync(message, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        _logger?.LogInformation("Message sent - MessageId: {MessageId} SenderId: {SenderId} ReceiverId: {ReceiverId}", message.Id, senderId, receiverId);

        return _mapper.Map<SendMessageResponse>(message);
    }

    public async Task<ConversationHistoryResponse> GetConversationHistoryAsync(Guid userId, Guid otherUserId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        if (userId == Guid.Empty) throw new AppException(ResponseCodes.SENDER_ID_REQUIRED, ErrorMessages.Messaging.SenderIdRequired);
        if (otherUserId == Guid.Empty) throw new AppException(ResponseCodes.RECEIVER_ID_REQUIRED, ErrorMessages.Messaging.ReceiverIdRequired);
        if (pageNumber <= 0) throw new ArgumentOutOfRangeException(nameof(pageNumber), ErrorMessages.Messaging.PageNumberOutOfRange);
        if (pageSize <= 0 || pageSize > AppConstants.Messaging.MaxPageSize) throw new ArgumentOutOfRangeException(nameof(pageSize), ErrorMessages.Messaging.PageSizeOutOfRange);
        if (userId == Guid.Empty) throw new AppException(ResponseCodes.SENDER_ID_REQUIRED, ErrorMessages.Messaging.SenderIdRequired);
        if (otherUserId == Guid.Empty) throw new AppException(ResponseCodes.RECEIVER_ID_REQUIRED, ErrorMessages.Messaging.ReceiverIdRequired);

        var (messages, totalCount) = await _messageRepository.GetConversationAsync(userId, otherUserId, pageNumber, pageSize, ct);

        var dtos = messages.Select(m => _mapper.Map<MessageDto>(m)).ToList();

        return new ConversationHistoryResponse
        {
            Messages = dtos,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task MarkMessageAsDeliveredAsync(Guid messageId, CancellationToken ct = default)
    {
        if (messageId == Guid.Empty) throw new AppException(ResponseCodes.MESSAGE_NOT_FOUND, ErrorMessages.Messaging.MessageNotFound);

        var message = await _messageRepository.GetByIdAsync(messageId, ct);
        if (message == null) throw new KeyNotFoundException(ErrorMessages.Messaging.MessageNotFound);

        message.DeliveredAt = DateTime.UtcNow;
        await _messageRepository.UpdateAsync(message, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger?.LogInformation("Message delivered - MessageId: {MessageId}", messageId);

        // Notify sender via real-time notifier
        await _realTimeNotifier.TryNotifyMessageDeliveredAsync(message.SenderId, messageId, message.DeliveredAt ?? DateTime.UtcNow);
    }

    public async Task MarkMessageAsReadAsync(Guid messageId, CancellationToken ct = default)
    {
        if (messageId == Guid.Empty) throw new AppException(ResponseCodes.MESSAGE_NOT_FOUND, ErrorMessages.Messaging.MessageNotFound);

        var message = await _messageRepository.GetByIdAsync(messageId, ct);
        if (message == null) throw new KeyNotFoundException(ErrorMessages.Messaging.MessageNotFound);

        message.ReadAt = DateTime.UtcNow;
        await _messageRepository.UpdateAsync(message, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger?.LogInformation("Message read - MessageId: {MessageId}", messageId);

        await _realTimeNotifier.TryNotifyMessageReadAsync(message.SenderId, messageId, message.ReadAt ?? DateTime.UtcNow);
    }
}
