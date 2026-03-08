#nullable enable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PutZige.Application.Common.Constants;
using PutZige.Application.Common.Messages;
using PutZige.Application.DTOs.Common;
using PutZige.Application.DTOs.Messaging;
using PutZige.Application.Interfaces;

namespace PutZige.API.Controllers
{
    [Route("api/v1/messages")]
    [Authorize]
    public sealed class MessagesController : BaseApiController
    {
        private readonly IMessagingService _messagingService;
        private readonly ILogger<MessagesController> _logger;
        private readonly PutZige.Application.Interfaces.ICurrentUserService _currentUserService;

        public MessagesController(
            IMessagingService messagingService,
            PutZige.Application.Interfaces.ICurrentUserService currentUserService,
            ILogger<MessagesController> logger)
        {
            _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Edit an existing message's text. Only the original sender may edit.
        /// </summary>
        [HttpPut("{messageId}")]
        [EnableRateLimiting("api-general")]
        [ProducesResponseType(typeof(ApiResponse<MessageDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<MessageDto>>> EditMessage(
            Guid messageId,
            [FromBody] EditMessageRequest request,
            CancellationToken ct)
        {
            var result = await _messagingService.EditMessageAsync(messageId, request.MessageText, ct).ConfigureAwait(false);
            return Success(result, ResponseCodes.SUCCESS);
        }

        /// <summary>
        /// Sends a message to another user.
        /// </summary>
        [HttpPost]
        [EnableRateLimiting("api-general")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<ActionResult<ApiResponse<SendMessageResponse>>> SendMessage(
            [FromBody] SendMessageRequest request,
            CancellationToken ct)
        {
            // Require explicit ConversationId
            if (request.ConversationId == Guid.Empty)
                return BadRequest("ConversationId is required");

            var senderId = _currentUserService.GetUserId();
            var response = await _messagingService.SendMessageAsync(request.ConversationId, request.MessageText, senderId, ct);

            return Created(response, ResponseCodes.MESSAGE_SENT, SuccessMessages.Messaging.MessageSent);
        }

        /// <summary>
        /// Marks a message as read.
        /// </summary>
        [HttpPatch("{messageId}/read")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> MarkAsRead(Guid messageId, CancellationToken ct = default)
        {
            await _messagingService.MarkMessageAsReadAsync(messageId, ct);
            return NoContent();
        }
    }
}