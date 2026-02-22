#nullable enable
using FluentValidation;
using PutZige.Application.DTOs.Users;
using System.Linq;

namespace PutZige.Application.Validators
{
    public class UpdatePreferencesRequestValidator : AbstractValidator<UpdatePreferencesRequest>
    {
        private static readonly string[] AllowedThemes = new[] { "system", "light", "dark" };

        public UpdatePreferencesRequestValidator()
        {
            RuleFor(x => x.Preferences).NotNull().WithMessage("preferences is required");

            When(x => x.Preferences != null, () =>
            {
                RuleFor(x => x.Preferences!.TimeZoneId)
                    .Must(BeValidTimeZone).When(p => !string.IsNullOrWhiteSpace(p.Preferences!.TimeZoneId))
                    .WithMessage("timeZoneId must be a valid IANA timezone");

                RuleFor(x => x.Preferences!.Theme)
                    .Must(t => t == null || AllowedThemes.Contains(t))
                    .WithMessage("theme must be one of: system, light, dark");

                RuleFor(x => x.Preferences!.Language)
                    .Must(l => l == null || (l.Length == 2 && l.All(char.IsLetter)))
                    .WithMessage("language must be a 2-letter ISO 639-1 code");
            });
        }

        private static bool BeValidTimeZone(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            try
            {
                // On Linux containers this expects IANA names
                var tz = System.TimeZoneInfo.FindSystemTimeZoneById(id);
                return tz != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
