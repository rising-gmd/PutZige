#nullable enable

namespace PutZige.Application.DTOs.Users
{
    public sealed class UserPreferencesDto
    {
        public string TimeZoneId { get; init; } = null!;
        public string Theme { get; init; } = null!;
        public string Language { get; init; } = null!;
    }
}
