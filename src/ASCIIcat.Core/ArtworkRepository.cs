using System.Text.Json;
using System.Text.Json.Serialization;

namespace ASCIIcat.Core;

public sealed class ArtworkLibraryFile
{
    public int SchemaVersion { get; set; } = 1;
    public List<AsciiArtwork> Artworks { get; set; } = [];
}

public sealed class ArtworkRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string filePath;

    public ArtworkRepository(string filePath)
    {
        this.filePath = Path.GetFullPath(filePath);
    }

    public string FilePath => filePath;

    public ArtworkLibraryFile Load()
    {
        if (!File.Exists(filePath))
            return new ArtworkLibraryFile();

        var json = File.ReadAllText(filePath);
        var library = JsonSerializer.Deserialize<ArtworkLibraryFile>(json, JsonOptions)
            ?? new ArtworkLibraryFile();

        if (library.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported ASCIIcat library schema {library.SchemaVersion}.");

        foreach (var artwork in library.Artworks)
        {
            artwork.Lines ??= [];
            artwork.OriginalLines ??= [.. artwork.Lines];
            artwork.Tags ??= [];
        }

        return library;
    }

    public void Save(ArtworkLibraryFile library)
    {
        ArgumentNullException.ThrowIfNull(library);
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("The artwork library path has no directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = filePath + ".tmp";
        var json = JsonSerializer.Serialize(library, JsonOptions);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, filePath, true);
    }

    public string ExportBackup(ArtworkLibraryFile library, DateTimeOffset? now = null)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("The artwork library path has no directory.");
        Directory.CreateDirectory(directory);
        var timestamp = (now ?? DateTimeOffset.Now).ToString("yyyyMMdd-HHmmss");
        var backupPath = Path.Combine(directory, $"ASCIIcat-backup-{timestamp}.json");
        File.WriteAllText(backupPath, JsonSerializer.Serialize(library, JsonOptions));
        return backupPath;
    }
}

