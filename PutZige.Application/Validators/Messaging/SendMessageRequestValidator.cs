using FluentValidation;
using PutZige.Application.Common.Constants;
using PutZige.Application.DTOs.Messaging;

namespace PutZige.Application.Validators.Messaging;

public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
        public SendMessageRequestValidator()
        {
            // Validate either ConversationId (preferred) or ReceiverId for legacy clients
            RuleFor(x => x.ConversationId).NotEmpty().WithName("conversationId").WithMessage("conversationId is required");
            RuleFor(x => x.MessageText)
                .NotEmpty().WithName("messageText").WithMessage(PutZige.Application.Common.Messages.ErrorMessages.Messaging.MessageTextRequired)
                .MaximumLength(AppConstants.Messaging.MaxMessageLength).WithMessage(PutZige.Application.Common.Messages.ErrorMessages.Messaging.MessageTooLong);
        }
}
