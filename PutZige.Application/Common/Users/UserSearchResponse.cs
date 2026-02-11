namespace PutZige.Application.Common.Users
{
    /// <summary>
    /// Response containing search results for users.
    /// </summary>
    public sealed class UserSearchResponse
    {
        public UserSearchDto[] Users { get; set; } = Array.Empty<UserSearchDto>();
        public int TotalCount { get; set; }
    }
}
