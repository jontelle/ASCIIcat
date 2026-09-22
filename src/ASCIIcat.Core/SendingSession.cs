namespace ASCIIcat.Core;

public sealed class SendingSession
{
    private readonly object sync = new();
    private Guid? artworkId;
    private List<string> lines = [];
    private int nextLineIndex;

    public Guid? ArtworkId
    {
        get { lock (sync) return artworkId; }
    }

    public int NextLineIndex
    {
        get { lock (sync) return nextLineIndex; }
    }

    public int LineCount
    {
        get { lock (sync) return lines.Count; }
    }

    public bool IsComplete
    {
        get { lock (sync) return lines.Count > 0 && nextLineIndex >= lines.Count; }
    }

    public string? CurrentLine
    {
        get
        {
            lock (sync)
                return nextLineIndex < lines.Count ? lines[nextLineIndex] : null;
        }
    }

    public void Start(AsciiArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        lock (sync)
        {
            artworkId = artwork.Id;
            lines = [.. artwork.Lines];
            nextLineIndex = 0;
        }
    }

    public bool ObserveExactChatEcho(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        lock (sync)
        {
            if (nextLineIndex >= lines.Count)
                return false;
            if (!string.Equals(lines[nextLineIndex], body, StringComparison.Ordinal))
                return false;

            nextLineIndex++;
            return true;
        }
    }

    public bool AdvanceManually()
    {
        lock (sync)
        {
            if (nextLineIndex >= lines.Count)
                return false;
            nextLineIndex++;
            return true;
        }
    }

    public void Reset()
    {
        lock (sync)
            nextLineIndex = 0;
    }

    public void Stop()
    {
        lock (sync)
        {
            artworkId = null;
            lines = [];
            nextLineIndex = 0;
        }
    }
}

