using FluentValidation;
using PutZige.Application.DTOs.Auth;

namespace PutZige.Application.Validators;

public sealed class ResendVerificationRequestValidator : AbstractValidator<ResendVerificationRequest>
{
    public ResendVerificationRequestValidator()
        {
            RuleFor(x => x.Token)
                .NotEmpty().WithName("token").WithMessage(PutZige.Application.Common.Messages.ErrorMessages.Validation.TokenRequired);
        }
}
