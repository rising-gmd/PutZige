#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace PutZige.Domain.Models;

/// <summary>
/// Generic paginated result container for billion-scale queries.
/// </summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public long TotalCount { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public static PagedResult<T> Create(
        IEnumerable<T> items,
        int pageNumber,
        int pageSize,
        long totalCount)
    {
        return new PagedResult<T>
        {
            Items = items.ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public static PagedResult<T> Empty(int pageNumber, int pageSize)
    {
        return new PagedResult<T>
        {
            Items = Array.Empty<T>(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = 0
        };
    }
}
