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

        public MessagesController(
            IMessagingService messagingService,
            ILogger<MessagesController> logger)
        {
            _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Sends a message to another user.
        /// </summary>
        [HttpPost]
        [EnableRateLimiting("api-general")]
        public async Task<ActionResult<ApiResponse<SendMessageResponse>>> SendMessage(
            [FromBody] SendMessageRequest request,
            CancellationToken ct)
        {
            var response = await _messagingService.SendMessageAsync(
                request.ReceiverId,
                request.MessageText,
                ct);

            return Success(response, ResponseCodes.MESSAGE_SENT, SuccessMessages.Messaging.MessageSent);
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