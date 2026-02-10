#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PutZige.Application.DTOs.Messaging;
using PutZige.Application.Interfaces;
using PutZige.Application.Common.Messages;
using PutZige.Application.DTOs.Common;
using PutZige.Application.Common.Constants;
using Microsoft.AspNetCore.RateLimiting;

namespace PutZige.API.Controllers;

[Route("api/v1/messages")]
[Authorize]
public sealed class MessagesController : BaseApiController
{
    private readonly IMessagingService _messagingService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(IMessagingService messagingService, ICurrentUserService currentUserService, ILogger<MessagesController> logger)
    {
        _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    [EnableRateLimiting("api-general")]
        public async Task<ActionResult<ApiResponse<SendMessageResponse>>> SendMessage([FromBody] SendMessageRequest request, CancellationToken ct)
        {
            var userId = _currentUserService.GetUserId();
            var response = await _messagingService.SendMessageAsync(userId, request.ReceiverId, request.MessageText, ct);
            return Success(response, ResponseCodes.MESSAGE_SENT, SuccessMessages.Messaging.MessageSent);
        }

    [HttpGet("conversation/{otherUserId}")]
    [EnableRateLimiting("api-general")]
        public async Task<ActionResult<ApiResponse<ConversationHistoryResponse>>> GetConversation(Guid otherUserId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        {
            var userId = _currentUserService.GetUserId();
            var response = await _messagingService.GetConversationHistoryAsync(userId, otherUserId, pageNumber, pageSize, ct);
            return Success(response, ResponseCodes.CONVERSATION_RETRIEVED);
        }

    [HttpPatch("{messageId}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> MarkAsRead(Guid messageId, CancellationToken ct = default)
        {
            await _messagingService.MarkMessageAsReadAsync(messageId, ct);
            return NoContent();
        }
}
