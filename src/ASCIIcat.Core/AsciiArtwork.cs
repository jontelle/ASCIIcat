namespace ASCIIcat.Core;

public enum ArtworkSourceKind
{
    Manual,
    Clipboard,
    Chat,
    TextFile,
}

public sealed class AsciiArtwork
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Untitled artwork";
    public List<string> Lines { get; set; } = [];
    public List<string> OriginalLines { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public bool IsFavorite { get; set; }
    public ArtworkSourceKind SourceKind { get; set; } = ArtworkSourceKind.Manual;
    public string? SourceNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string ToText(string newline = "\r\n") => string.Join(newline, Lines);

    public AsciiArtwork Clone()
    {
        return new AsciiArtwork
        {
            Id = Id,
            Name = Name,
            Lines = [.. Lines],
            OriginalLines = [.. OriginalLines],
            Tags = [.. Tags],
            IsFavorite = IsFavorite,
            SourceKind = SourceKind,
            SourceNote = SourceNote,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        };
    }
}

