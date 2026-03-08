namespace PutZige.Application.Common.Constants
{
    public static class SignalRConstants
    {
        public const string HubRoute = "/api/v1/hubs/chat";

        public static class Events
        {
            // Server -> Client events
            public const string UserOnline = "UserOnline";
            public const string UserOffline = "UserOffline";
            public const string ReceiveMessage = "ReceiveMessage";
            public const string MessageSent = "MessageSent";
            public const string MessageDelivered = "MessageDelivered";
            public const string MessageRead = "MessageRead";
            public const string MessageEdited = "MessageEdited";
            public const string UserTyping = "UserTyping";
            public const string UserStoppedTyping = "UserStoppedTyping";
            public const string ConversationCreated = "ConversationCreated";
            public const string Error = "Error";
        }

        public static class Methods
        {
            public const string SendMessage = "SendMessage";
        }

        public static class ErrorMessages
        {
            public const string Unauthorized = "Not authorized";
            public const string InvalidUserId = "User is not authenticated or invalid user ID";
        }
    }
}
