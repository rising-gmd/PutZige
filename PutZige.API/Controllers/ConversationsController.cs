#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PutZige.Application.Common.Constants;
using PutZige.Application.DTOs.Common;
using PutZige.Application.DTOs.Messaging;
using PutZige.Application.Interfaces;

namespace PutZige.API.Controllers
{
    [Route("api/v1/conversations")]
    [Authorize]
    public sealed class ConversationsController : BaseApiController
    {
        private readonly IMessagingService _messagingService;
        private readonly PutZige.Application.Interfaces.IConversationService _conversationService;
        private readonly ILogger<ConversationsController> _logger;

        public ConversationsController(
            IMessagingService messagingService,
            PutZige.Application.Interfaces.IConversationService conversationService,
            ILogger<ConversationsController> logger)
        {
            _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
            _conversationService = conversationService ?? throw new ArgumentNullException(nameof(conversationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates or retrieves an existing direct conversation with another user.
        /// Call this before sending the first message to a new contact.
        /// </summary>
        /// <param name="request">Request containing the other user's ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Conversation details including conversation ID</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<ConversationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<ConversationDto>>> CreateOrGetConversation(
            [FromBody] CreateConversationRequest request,
            CancellationToken ct)
        {
            var dto = await _conversationService.GetOrCreateDirectConversationAsync(request.OtherUserId, ct).ConfigureAwait(false);
            return Success(dto, ResponseCodes.CONVERSATION_CREATED);
        }

        /// <summary>
        /// Gets conversation details by ID.
        /// </summary>
        [HttpGet("{conversationId}")]
        [ProducesResponseType(typeof(ApiResponse<ConversationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<ConversationResponse>>> GetConversation(
            Guid conversationId,
            CancellationToken ct)
        {
            var response = await _conversationService.GetConversationByIdAsync(conversationId, ct).ConfigureAwait(false);
            return Success(response, ResponseCodes.CONVERSATION_RETRIEVED);
        }

        /// <summary>
        /// Retrieves all conversations for the current authenticated user.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<ConversationListResponse>>> GetAll(CancellationToken ct)
        {
            var response = await _messagingService.GetConversationsAsync(ct);
            return Success(response, ResponseCodes.CONVERSATIONS_RETRIEVED);
        }

        /// <summary>
        /// Retrieves paginated message history for a specific conversation.
        /// </summary>
        [HttpGet("{conversationId}/messages")]
        public async Task<ActionResult<ApiResponse<ConversationHistoryResponse>>> GetMessages(
            Guid conversationId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken ct = default)
        {
            var response = await _messagingService.GetConversationHistoryAsync(
                conversationId,
                pageNumber,
                pageSize,
                ct);

            return Success(response, ResponseCodes.CONVERSATION_RETRIEVED);
        }

        /// <summary>
        /// Marks all messages in the specified conversation as read for the current user.
        /// </summary>
        [HttpPatch("{conversationId}/read")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> MarkAsRead(Guid conversationId, CancellationToken ct = default)
        {
            await _messagingService.MarkConversationAsReadAsync(conversationId, ct).ConfigureAwait(false);
            return NoContent();
        }
    }
}