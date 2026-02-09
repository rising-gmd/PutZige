using FluentValidation;
using PutZige.Application.DTOs.Auth;

namespace PutZige.Application.Validators;

public sealed class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
        public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithName("token");
    }
}
