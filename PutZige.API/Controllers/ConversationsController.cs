#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PutZige.Application.Interfaces;
using System.Linq;
using PutZige.Application.DTOs.Common;
using PutZige.Application.Common.Constants;

namespace PutZige.API.Controllers
{
    [Route("api/v1/conversations")]
    [Authorize]
    public sealed class ConversationsController : BaseApiController
    {
        private readonly IMessagingService _messagingService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ConversationsController> _logger;

        public ConversationsController(IMessagingService messagingService, ICurrentUserService currentUserService, ILogger<ConversationsController> logger)
        {
            _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<object>>> GetAll(CancellationToken ct)
        {
            var userId = _currentUserService.GetUserId();

            var list = await _messagingService.GetConversationsAsync(userId, ct).ConfigureAwait(false);

            var conversations = list.Conversations.Select(c => new
            {
                userId = c.UserId,
                username = c.Username,
                displayName = c.DisplayName,
                jobTitle = (string?)null,
                profilePictureUrl = c.ProfilePictureUrl,
                isOnline = c.IsOnline,
                lastMessage = c.LastMessage == null ? null : new
                {
                    id = c.LastMessage.Id,
                    senderId = c.LastMessage.SenderId,
                    receiverId = c.LastMessage.ReceiverId,
                    messageText = c.LastMessage.MessageText,
                    sentAt = c.LastMessage.SentAt.ToString("o"),
                    deliveredAt = c.LastMessage.DeliveredAt.HasValue ? c.LastMessage.DeliveredAt.Value.ToString("o") : null,
                    readAt = c.LastMessage.ReadAt.HasValue ? c.LastMessage.ReadAt.Value.ToString("o") : null
                },
                unreadCount = c.UnreadCount,
                lastActivity = c.LastActivity.HasValue ? c.LastActivity.Value.ToString("o") : null
            }).ToArray();

            var response = (object)new { conversations, totalCount = list.TotalCount };

            return Success(response, ResponseCodes.CONVERSATIONS_RETRIEVED);
        }

        [HttpGet("{conversationId}/messages")]
        public async Task<ActionResult<ApiResponse<object>>> GetMessages(Guid conversationId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        {
            var userId = _currentUserService.GetUserId();

            if (conversationId == Guid.Empty) return BadRequestError<object>(ResponseCodes.VALIDATION_FAILED, "Conversation id is required");

            var history = await _messagingService.GetConversationHistoryAsync(userId, conversationId, pageNumber, pageSize, ct).ConfigureAwait(false);

            if (history == null || history.Messages == null || history.TotalCount == 0 && (history.Messages?.Count ?? 0) == 0)
            {
                // If no messages found return 404
                return NotFoundError<object>(ResponseCodes.NOT_FOUND, "Conversation not found");
            }

            var messages = history.Messages.Select(m => new
            {
                id = m.Id,
                senderId = m.SenderId,
                receiverId = m.ReceiverId,
                messageText = m.MessageText,
                sentAt = m.SentAt.ToString("o"),
                deliveredAt = m.DeliveredAt.HasValue ? m.DeliveredAt.Value.ToString("o") : null,
                readAt = m.ReadAt.HasValue ? m.ReadAt.Value.ToString("o") : null
            }).ToArray();

            var response = (object)new
            {
                messages,
                totalCount = history.TotalCount,
                pageNumber = history.PageNumber,
                pageSize = history.PageSize
            };

            return Success(response, ResponseCodes.CONVERSATION_RETRIEVED);
        }
    }
}
