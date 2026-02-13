#nullable enable
using System;

namespace PutZige.Application.DTOs.Users;

public sealed class UserSearchResultDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeen { get; set; }
    public string? Bio { get; set; }
    public string? JobTitle { get; set; }
}
