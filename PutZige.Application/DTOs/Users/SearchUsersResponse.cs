#nullable enable
using System.Collections.Generic;

namespace PutZige.Application.DTOs.Users;

public sealed class SearchUsersResponse
{
    public List<UserSearchResultDto> Users { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
