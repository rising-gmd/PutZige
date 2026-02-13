#nullable enable
using System.Collections.Generic;

namespace PutZige.Domain.Models;

/// <summary>
/// Cursor-based pagination for infinite scroll (better performance at scale).
/// </summary>
public sealed class CursorPagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
    public int PageSize { get; init; }
}
