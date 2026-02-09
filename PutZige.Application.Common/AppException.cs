namespace PutZige.Application.Common
{
    using System;
    using System.Collections.Generic;

    public class AppException : Exception
    {
        public string ResponseCode { get; }
        public Dictionary<string, object>? Metadata { get; }

        public AppException(string responseCode, string? message = null, Dictionary<string, object>? metadata = null)
            : base(message ?? responseCode)
        {
            ResponseCode = responseCode;
            Metadata = metadata;
        }
    }
}
