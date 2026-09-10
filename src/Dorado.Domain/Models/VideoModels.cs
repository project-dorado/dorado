using System;

namespace Dorado.Domain.Models;

public class Video
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public int? Year { get; set; }
    public string Genre { get; set; } = string.Empty;
    public string? ArtworkUri { get; set; }
    public int PlayCount { get; set; }
    public DateTime? LastPlayedAtUtc { get; set; }
    public long SizeBytes { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Photo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public DateTime? TakenDate { get; set; }
    public long SizeBytes { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}
