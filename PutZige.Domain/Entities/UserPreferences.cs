namespace PutZige.Domain.Entities
{
    public sealed record UserPreferences(
        string TimeZoneId = "UTC",
        string Theme = "system",
        string Language = "en",
        bool IsDarkMode = false
    );
}
