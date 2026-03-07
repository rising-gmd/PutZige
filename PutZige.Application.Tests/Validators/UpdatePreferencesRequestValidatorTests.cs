#nullable enable
using System.Threading.Tasks;
using FluentValidation.TestHelper;
using Xunit;
using PutZige.Application.DTOs.Users;
using PutZige.Application.Validators;

namespace PutZige.Application.Tests.Validators
{
    public class UpdatePreferencesRequestValidatorTests
    {
        private readonly UpdatePreferencesRequestValidator _validator = new();

        [Fact]
        public void Validator_Should_Fail_When_Preferences_Null()
        {
            var req = new UpdatePreferencesRequest { Preferences = null };
            var result = _validator.TestValidate(req);
            result.ShouldHaveValidationErrorFor(r => r.Preferences);
        }

        [Fact]
        public void Validator_Should_Pass_For_Theme_And_Language_When_TimeZone_Omitted()
        {
            var req = new UpdatePreferencesRequest
            {
                Preferences = new UserPreferencesPatchDto
                {
                    Theme = "rose",
                    Language = "en"
                }
            };

            var result = _validator.TestValidate(req);
            result.ShouldNotHaveValidationErrorFor(r => r.Preferences!.Theme);
            result.ShouldNotHaveValidationErrorFor(r => r.Preferences!.Language);
            // TimeZone not provided -> no validation executed for TimeZoneId
        }

        [Fact]
        public void Validator_Should_Fail_For_Invalid_Theme()
        {
            var req = new UpdatePreferencesRequest
            {
                Preferences = new UserPreferencesPatchDto
                {
                    Theme = "blue",
                }
            };

            var result = _validator.TestValidate(req);
            result.ShouldHaveValidationErrorFor(r => r.Preferences!.Theme);
        }

        [Fact]
        public void Validator_Should_Fail_For_Invalid_Language()
        {
            var req = new UpdatePreferencesRequest
            {
                Preferences = new UserPreferencesPatchDto
                {
                    Theme = "rose",
                    Language = "eng"
                }
            };

            var result = _validator.TestValidate(req);
            result.ShouldHaveValidationErrorFor(r => r.Preferences!.Language);
        }
    }
}
