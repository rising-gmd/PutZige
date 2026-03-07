#nullable enable
using System;

namespace PutZige.Domain.Entities;

public class MessageAttachment : BaseEntity
{
    public Guid MessageId { get; set; }

    public string Type { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? DurationSecs { get; set; }
    public string? DownloadUrl { get; set; }
    public string? Caption { get; set; }

    // Navigation
    public Message Message { get; set; } = null!;
}
