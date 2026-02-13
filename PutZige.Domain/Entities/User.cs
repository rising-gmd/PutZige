using System;

namespace PutZige.Domain.Entities
{
    /// <summary>
    /// Represents an application user.
    /// </summary>
    public class User : BaseEntity
    {
        // Authentication
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string PasswordSalt { get; set; } = string.Empty;
        public bool IsEmailVerified { get; set; } = false;
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationTokenExpiry { get; set; }
        public int EmailVerificationSentCount { get; set; } = 0;
        public DateTime? LastEmailVerificationSentAt { get; set; }

        // Profile
        public string Username { get; set; } = null!;
        public string? DisplayName { get; set; }
        public string? Bio { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string? JobTitle { get; set; }

        // Password Reset
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }
        public int PasswordResetAttempts { get; set; } = 0;
        public DateTime? LastPasswordResetAttempt { get; set; }

        // Two-Factor Authentication
        public bool IsTwoFactorEnabled { get; set; } = false;
        public string? TwoFactorSecret { get; set; }
        public string? TwoFactorRecoveryCodes { get; set; }

        // Account Status
        public bool IsActive { get; set; } = true;
        public bool IsLocked { get; set; } = false;
        public DateTime? LockedUntil { get; set; }
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LastFailedLoginAttempt { get; set; }

        // Session & Security
        public DateTime? LastLoginAt { get; set; }
        public string? LastLoginIp { get; set; }

        // Navigation properties to normalized tables
        public UserSettings? Settings { get; set; }
        public UserSession? Session { get; set; }
        public UserRateLimit? RateLimit { get; set; }
        public UserMetadata? Metadata { get; set; }
        
        // Messaging navigation
        public System.Collections.Generic.ICollection<Message>? SentMessages { get; set; }
        public System.Collections.Generic.ICollection<Message>? ReceivedMessages { get; set; }
    }
}

