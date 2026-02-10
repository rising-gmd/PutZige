namespace PutZige.Application.Common.Constants
{
    public static class ResponseCodes
    {
        // Success codes
        public const string EMAIL_VERIFIED = "EMAIL_VERIFIED";
        public const string LOGIN_SUCCESS = "LOGIN_SUCCESS";
        public const string REGISTRATION_SUCCESS = "REGISTRATION_SUCCESS";
        public const string EMAIL_VERIFICATION_SENT = "EMAIL_VERIFICATION_SENT";

        // Auth errors
        public const string INVALID_CREDENTIALS = "INVALID_CREDENTIALS";
        public const string EMAIL_NOT_VERIFIED = "EMAIL_NOT_VERIFIED";
        public const string ACCOUNT_LOCKED = "ACCOUNT_LOCKED";
        public const string TOKEN_EXPIRED = "TOKEN_EXPIRED";
        public const string TOKEN_INVALID = "TOKEN_INVALID";
        public const string EMAIL_ALREADY_EXISTS = "EMAIL_ALREADY_EXISTS";
        public const string USERNAME_TAKEN = "USERNAME_TAKEN";
        public const string ACCOUNT_INACTIVE = "ACCOUNT_INACTIVE";

        // Email verification
        public const string EMAIL_ALREADY_VERIFIED = "EMAIL_ALREADY_VERIFIED";
        public const string LOGOUT_SUCCESS = "AUTH_LOGOUT_SUCCESS";
        public const string NEGOTIATE_SUCCESS = "SIGNALR_NEGOTIATE_SUCCESS";
        public const string TOKEN_REFRESHED = "AUTH_TOKEN_REFRESHED";
        public const string TOO_MANY_RESEND_ATTEMPTS = "TOO_MANY_RESEND_ATTEMPTS";

        // General
        public const string NOT_FOUND = "NOT_FOUND";
        public const string VALIDATION_FAILED = "VALIDATION_FAILED";
        public const string INTERNAL_SERVER_ERROR = "INTERNAL_SERVER_ERROR";
        public const string UNAUTHORIZED = "UNAUTHORIZED";
        public const string FORBIDDEN = "FORBIDDEN";

        // Field-specific validation codes
        public const string EMAIL_REQUIRED = "EMAIL_REQUIRED";
        public const string TOKEN_REQUIRED = "TOKEN_REQUIRED";
        public const string IDENTIFIER_REQUIRED = "IDENTIFIER_REQUIRED";
        public const string PASSWORD_REQUIRED = "PASSWORD_REQUIRED";
        public const string REFRESH_TOKEN_REQUIRED = "REFRESH_TOKEN_REQUIRED";
        public const string USERNAME_REQUIRED = "USERNAME_REQUIRED";
        public const string PLAIN_TEXT_REQUIRED = "PLAIN_TEXT_REQUIRED";
        public const string HASH_REQUIRED = "HASH_REQUIRED";
        public const string SALT_REQUIRED = "SALT_REQUIRED";
        public const string SENDER_ID_REQUIRED = "SENDER_ID_REQUIRED";
        public const string RECEIVER_ID_REQUIRED = "RECEIVER_ID_REQUIRED";
        public const string MESSAGE_TEXT_REQUIRED = "MESSAGE_TEXT_REQUIRED";
        public const string MESSAGE_TOO_LONG = "MESSAGE_TOO_LONG";
        public const string MESSAGE_NOT_FOUND = "MESSAGE_NOT_FOUND";
        public const string PAGE_NUMBER_OUT_OF_RANGE = "PAGE_NUMBER_OUT_OF_RANGE";
        public const string PAGE_SIZE_OUT_OF_RANGE = "PAGE_SIZE_OUT_OF_RANGE";
        public const string JWT_SECRET_NOT_CONFIGURED = "JWT_SECRET_NOT_CONFIGURED";
        public const string JWT_SECRET_TOO_SHORT = "JWT_SECRET_TOO_SHORT";
        public const string PROFILE_RETRIEVED_SUCCESSFULLY = "PROFILE_RETRIEVED_SUCCESSFULLY";
        // Messaging
        public const string MESSAGE_SENT = "MESSAGE_SENT";
        public const string CONVERSATION_RETRIEVED = "CONVERSATION_RETRIEVED";
        public const string USERS_FOUND = "USERS_FOUND";
        public const string CONVERSATIONS_RETRIEVED = "CONVERSATIONS_RETRIEVED";
        public const string MESSAGE_MARKED_AS_READ = "MESSAGE_MARKED_AS_READ";
    }
}
