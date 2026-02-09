// Update validator to use Application.Settings RateLimitSettings
using FluentValidation;
using PutZige.Application.Settings;

namespace PutZige.Application.Validators
{
    public class RateLimitSettingsValidator : AbstractValidator<RateLimitSettings>
    {
        public RateLimitSettingsValidator()
        {
            RuleFor(x => x.Enabled).NotNull();

            RuleFor(x => x.GlobalApi).NotNull().SetValidator(new SlidingWindowPolicySettingsValidator());
            RuleFor(x => x.Login).NotNull().SetValidator(new FixedWindowPolicySettingsValidator());
            RuleFor(x => x.RefreshToken).NotNull().SetValidator(new FixedWindowPolicySettingsValidator());
            RuleFor(x => x.Registration).NotNull().SetValidator(new FixedWindowPolicySettingsValidator());
            RuleFor(x => x.EmailResend).NotNull().SetValidator(new FixedWindowPolicySettingsValidator());

            RuleFor(x => x.UseDistributedCache).NotNull();
            When(x => x.UseDistributedCache, () =>
            {
                RuleFor(x => x.RedisConnectionString).NotEmpty();
            });
        }
    }

    internal class FixedWindowPolicySettingsValidator : AbstractValidator<FixedWindowPolicySettings>
    {
        public FixedWindowPolicySettingsValidator()
        {
            RuleFor(x => x.PermitLimit).GreaterThan(0).LessThanOrEqualTo(10000);
            RuleFor(x => x.WindowSeconds).GreaterThan(0).LessThanOrEqualTo(86400);
        }
    }

    internal class SlidingWindowPolicySettingsValidator : AbstractValidator<SlidingWindowPolicySettings>
    {
        public SlidingWindowPolicySettingsValidator()
        {
            RuleFor(x => x.PermitLimit).GreaterThan(0).LessThanOrEqualTo(10000);
            RuleFor(x => x.WindowSeconds).GreaterThan(0).LessThanOrEqualTo(86400);
            RuleFor(x => x.SegmentsPerWindow).GreaterThanOrEqualTo(2).LessThanOrEqualTo(100);
        }
    }
}
