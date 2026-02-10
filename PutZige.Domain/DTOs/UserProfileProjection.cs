#nullable enable
using System;

namespace PutZige.Domain.DTOs
{
    /// <summary>
    /// Lightweight projection used by Dapper queries to read user profile fields.
    /// </summary>
    public sealed class UserProfileProjection
    {
        public required Guid Id { get; init; }
        public required string Username { get; init; }
        public required string Email { get; init; }
        public string? DisplayName { get; init; }
        public string? Bio { get; init; }
        public string? ProfilePictureUrl { get; init; }
        public required DateTime CreatedAt { get; init; }
        public DateTime? LastSeenAt { get; init; }
    }
}
