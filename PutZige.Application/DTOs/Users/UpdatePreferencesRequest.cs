#nullable enable
using System.Text.Json.Serialization;

namespace PutZige.Application.DTOs.Users
{
    public class UpdatePreferencesRequest
    {
        [JsonPropertyName("preferences")]
        public UserPreferencesPatchDto? Preferences { get; set; }
    }

    public class UserPreferencesPatchDto
    {
        [JsonPropertyName("timeZoneId")]
        public string? TimeZoneId { get; set; }

        [JsonPropertyName("theme")]
        public string? Theme { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("isDarkMode")]
        public bool? IsDarkMode { get; set; }
    }
}
