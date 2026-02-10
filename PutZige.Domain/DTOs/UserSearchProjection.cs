#nullable enable
using System;

namespace PutZige.Domain.DTOs
{
    /// <summary>
    /// Lightweight projection used by Dapper queries to read user search results.
    /// </summary>
    public sealed class UserSearchProjection
    {
        public required Guid Id { get; init; }
        public required string Username { get; init; }
        public string? DisplayName { get; init; }
        public required string Email { get; init; }
        public string? JobTitle { get; init; }
        public string? Bio { get; init; }
        public string? ProfilePictureUrl { get; init; }
    }
}