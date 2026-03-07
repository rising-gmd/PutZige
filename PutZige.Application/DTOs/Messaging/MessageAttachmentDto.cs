using System;

namespace PutZige.Application.DTOs.Messaging;

public class MessageAttachmentDto
{
    public Guid Id { get; set; }
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
}
