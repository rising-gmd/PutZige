// New file: Application settings wrapper for rate limiting so Application layer can reference settings without depending on Infrastructure.
namespace PutZige.Application.Settings
{
    public class RateLimitSettings
    {
        public const string SectionName = "RateLimitSettings";

        public bool Enabled { get; set; } = true;

        public SlidingWindowPolicySettings GlobalApi { get; set; } = new SlidingWindowPolicySettings();
        public FixedWindowPolicySettings Login { get; set; } = new FixedWindowPolicySettings();
        public FixedWindowPolicySettings RefreshToken { get; set; } = new FixedWindowPolicySettings();
        public FixedWindowPolicySettings Registration { get; set; } = new FixedWindowPolicySettings();
        public FixedWindowPolicySettings EmailResend { get; set; } = new FixedWindowPolicySettings();

        public bool UseDistributedCache { get; set; } = false;
        public string? RedisConnectionString { get; set; }
    }

    public class FixedWindowPolicySettings
    {
        public int PermitLimit { get; set; }
        public int WindowSeconds { get; set; }
    }

    public class SlidingWindowPolicySettings
    {
        public int PermitLimit { get; set; }
        public int WindowSeconds { get; set; }
        public int SegmentsPerWindow { get; set; } = 8;
    }
}
