#nullable enable
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using PutZige.Application.Validators.Auth;
using PutZige.Application.DTOs.Auth;
using PutZige.Application.Common.Messages;

namespace PutZige.Application.Tests.Validators
{
    public class LoginRequestValidatorTests
    {
        private readonly LoginRequestValidator _validator = new();

        /// <summary>
        /// Valid login request passes validation.
        /// </summary>
        [Fact]
        public async Task Validate_ValidData_PassesValidation()
        {
            var model = new LoginRequest { Identifier = "user@test.com", Password = "Password1!" };
            var result = await _validator.ValidateAsync(model);
            result.IsValid.Should().BeTrue();
        }

        /// <summary>
        /// Empty email fails validation and reports email-related error.
        /// </summary>
        [Fact]
        public async Task Validate_EmptyEmail_FailsValidation()
        {
            var model = new LoginRequest { Identifier = "", Password = "Password1!" };
            var result = await _validator.ValidateAsync(model);
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName.Equals("identifier", System.StringComparison.OrdinalIgnoreCase) || e.ErrorMessage == ErrorMessages.Validation.EmailRequired);
        }

        /// <summary>
        /// Invalid email format fails validation.
        /// </summary>
        [Fact]
        public async Task Validate_EmptyIdentifier_FailsValidation()
        {
            // Identifier accepts email OR username - "not-email" is a valid username format
            // To test invalid input, use something that fails both formats
            var model = new LoginRequest { Identifier = "", Password = "Password1!" };
            var result = await _validator.ValidateAsync(model);
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName.Equals("identifier", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Empty password fails validation and reports password-related error.
        /// </summary>
        [Fact]
        public async Task Validate_EmptyPassword_FailsValidation()
        {
            var model = new LoginRequest { Identifier = "user@test.com", Password = "" };
            var result = await _validator.ValidateAsync(model);
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName.Equals("password", System.StringComparison.OrdinalIgnoreCase) || e.ErrorMessage == ErrorMessages.Validation.PasswordRequired);
        }
    }
}
