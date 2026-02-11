using System;
using System.Collections.Generic;
using System.Text;

namespace PutZige.Application.Common.Users
{
    /// <summary>
    /// DTO for individual user in search results.
    /// </summary>
    public sealed class UserSearchDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public string? Bio { get; set; }
        public string? ProfilePictureUrl { get; set; }
    }
}
