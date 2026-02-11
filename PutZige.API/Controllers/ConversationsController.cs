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
        private readonly ILogger<ConversationsController> _logger;

        public ConversationsController(
            IMessagingService messagingService,
            ILogger<ConversationsController> logger)
        {
            _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
    }
}