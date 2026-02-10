#nullable enable
using System;

namespace PutZige.Application.DTOs.SignalR
{
    public sealed class SignalRNegotiateResponse
    {
        public string ConnectionToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }
}
