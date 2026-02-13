#nullable enable
namespace PutZige.Application.DTOs.Users;

public sealed class SearchUsersRequest
{
    public string Query { get; set; } = string.Empty;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
