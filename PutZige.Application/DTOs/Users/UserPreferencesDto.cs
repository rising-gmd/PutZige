#nullable enable
using System.Text.Json.Serialization;

namespace PutZige.Application.DTOs.Users
{
    public sealed class UserPreferencesDto
    {
        public string TimeZoneId { get; init; } = null!;
        public string Theme { get; init; } = null!;
        public string Language { get; init; } = null!;

        [JsonPropertyName("isDarkMode")]
        public bool IsDarkMode { get; set; }
    }
}
