namespace PutZige.Application.Common.Messages
{
    /// <summary>
    /// Centralized success messages for user-facing responses.
    /// </summary>
    public static class SuccessMessages
    {
        public static class Authentication
        {
            public const string RegistrationSuccessful = "Registration successful. Please check your email to verify your account.";
            public const string LoginSuccessful = "Login successful.";
            public const string EmailVerified = "Email verified successfully";
            public const string VerificationEmailSent = "Verification email sent successfully";
            public const string PasswordResetEmailSent = "Password reset instructions have been sent to your email.";
            public const string PasswordResetSuccessful = "Your password has been reset successfully.";
            public const string TokenRefreshed = "Token refreshed successfully.";
        }

        public static class General
        {
            public const string OperationSuccessful = "Operation completed successfully.";
        }

        public static class Messaging
        {
            public const string MessageSent = "Message sent successfully";
            public const string MessageMarkedAsRead = "Message marked as read";
            public const string ConversationCreated = "Conversation created successfully";
        }

        public static class UserProfile
        {
            public const string ProfileUpdated = "User profile updated successfully.";
            public const string ProfileRetrieved = "User profile retrieved successfully.";
        }
    }
}
