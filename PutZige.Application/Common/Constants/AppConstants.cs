namespace PutZige.Application.Common.Constants
{
    /// <summary>
    /// Application-wide constants. Never change these without version migration.
    /// </summary>
    public static class AppConstants
    {
        public static class Validation
        {
            // Username
            public const int MinUsernameLength = 3;
            public const int MaxUsernameLength = 50;
            public const string UsernameRegexPattern = "^[a-zA-Z0-9_]+$";

            // Display Name
            public const int MinDisplayNameLength = 2;
            public const int MaxDisplayNameLength = 100;

            // Password
            public const int MinPasswordLength = 8;
            public const int MaxPasswordLength = 128;
            public const string PasswordUppercaseRegex = "[A-Z]";
            public const string PasswordLowercaseRegex = "[a-z]";
            public const string PasswordDigitRegex = "[0-9]";
            public const string PasswordSpecialCharRegex = "[!@#$%^&*()_+\\-=\\[\\]{}|;:,.<>?]";

            // Email
            public const int MaxEmailLength = 255;

            // General
            public const int MaxNameLength = 100;
            public const int MaxPhoneLength = 20;
            public const int MaxUrlLength = 2048;
            public const int MaxShortTextLength = 500;
            public const int MaxLongTextLength = 5000;

            // Theme
            public const int MaxThemeLength = 50;

            // Language
            public const int MaxLanguageLength = 10;
        }

        public static class Security
        {
            public const int BcryptWorkFactor = 12; // Good balance (2^12 = 4096 iterations)
            public const int EmailVerificationTokenExpirationDays = 7;
            public const int PasswordResetTokenExpirationHours = 1;
            public const int RefreshTokenExpirationDays = 30;
            public const int AccessTokenExpirationMinutes = 15;
            public const int MaxLoginAttempts = 5;
            public const int LockoutDurationMinutes = 15;
            public const int LockoutMinutes = 15;
        }

        public static class Http
        {
            public const string ApiVersion = "v1";
            public const string ApiBaseRoute = "api/" + ApiVersion;
            public const string CorsPolicyName = "AllowSpecificOrigins";
        }

        public static class Database
        {
            public const int DefaultCommandTimeoutSeconds = 30;
            public const int MaxRetryAttempts = 3;
            public const int RetryDelayMilliseconds = 1000;
        }

        public static class Pagination
        {
            public const int DefaultPageSize = 20;
            public const int MaxPageSize = 100;
            public const int MinPageSize = 10;
        }

        public static class Messaging
        {
            public const int MaxMessageLength = 4000;
            public const int DefaultPageSize = 50;
            public const int MaxPageSize = 100;
            public const int ConversationListLimit = 100;  // ← add this
        }
    }
}