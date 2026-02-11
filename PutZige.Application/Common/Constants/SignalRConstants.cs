namespace PutZige.Application.Common.Constants
{
    public static class SignalRConstants
    {
        public const string HubRoute = "/api/v1/hubs/chat";

        public static class Events
        {
            public const string ReceiveMessage = "ReceiveMessage";
            public const string MessageSent = "MessageSent";
            public const string Error = "Error";
        }

        public static class Methods
        {
            public const string SendMessage = "SendMessage";
        }
    }
}
