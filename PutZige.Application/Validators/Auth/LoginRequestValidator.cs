#nullable enable
using FluentValidation;
using PutZige.Application.DTOs.Auth;
using PutZige.Application.Common.Messages;

namespace PutZige.Application.Validators.Auth
{
    public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.Identifier)
                .NotEmpty().WithMessage(ErrorMessages.Validation.IdentifierRequired)
                .WithName("identifier");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage(ErrorMessages.Validation.PasswordRequired)
                .WithName("password");
        }
    }
}
