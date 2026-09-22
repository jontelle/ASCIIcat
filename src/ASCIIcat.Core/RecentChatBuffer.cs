namespace ASCIIcat.Core;

public sealed record RecentChatLine(
    long Sequence,
    DateTimeOffset Timestamp,
    string Channel,
    string Sender,
    string Body);

public sealed class RecentChatBuffer
{
    private readonly object sync = new();
    private readonly List<RecentChatLine> lines = [];
    private long nextSequence;
    private int capacity;
    private TimeSpan retention;

    public RecentChatBuffer(int capacity = 200, TimeSpan? retention = null)
    {
        Configure(capacity, retention ?? TimeSpan.FromMinutes(15));
    }

    public void Configure(int newCapacity, TimeSpan newRetention)
    {
        if (newCapacity is < 20 or > 2000)
            throw new ArgumentOutOfRangeException(nameof(newCapacity));
        if (newRetention < TimeSpan.FromMinutes(1) || newRetention > TimeSpan.FromHours(24))
            throw new ArgumentOutOfRangeException(nameof(newRetention));

        lock (sync)
        {
            capacity = newCapacity;
            retention = newRetention;
            Prune(DateTimeOffset.UtcNow);
        }
    }

    public RecentChatLine Add(DateTimeOffset timestamp, string channel, string sender, string body)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(body);

        lock (sync)
        {
            var line = new RecentChatLine(++nextSequence, timestamp, channel, sender, body);
            lines.Add(line);
            Prune(DateTimeOffset.UtcNow);
            return line;
        }
    }

    public IReadOnlyList<RecentChatLine> Snapshot()
    {
        lock (sync)
        {
            Prune(DateTimeOffset.UtcNow);
            return lines.ToArray();
        }
    }

    public IReadOnlyList<RecentChatLine> SuggestBurst(long anchorSequence, TimeSpan? maximumGap = null)
    {
        var gap = maximumGap ?? TimeSpan.FromSeconds(12);
        lock (sync)
        {
            var anchorIndex = lines.FindIndex(line => line.Sequence == anchorSequence);
            if (anchorIndex < 0)
                return [];

            var start = anchorIndex;
            var end = anchorIndex;
            var anchor = lines[anchorIndex];

            while (start > 0 && Matches(lines[start - 1], lines[start], anchor, gap))
                start--;
            while (end + 1 < lines.Count && Matches(lines[end], lines[end + 1], anchor, gap))
                end++;

            return lines.GetRange(start, end - start + 1).ToArray();
        }
    }

    public void Clear()
    {
        lock (sync)
            lines.Clear();
    }

    private static bool Matches(
        RecentChatLine earlier,
        RecentChatLine later,
        RecentChatLine anchor,
        TimeSpan maximumGap)
    {
        return string.Equals(earlier.Channel, anchor.Channel, StringComparison.Ordinal)
            && string.Equals(earlier.Sender, anchor.Sender, StringComparison.Ordinal)
            && string.Equals(later.Channel, anchor.Channel, StringComparison.Ordinal)
            && string.Equals(later.Sender, anchor.Sender, StringComparison.Ordinal)
            && later.Timestamp >= earlier.Timestamp
            && later.Timestamp - earlier.Timestamp <= maximumGap;
    }

    private void Prune(DateTimeOffset now)
    {
        var oldest = now - retention;
        lines.RemoveAll(line => line.Timestamp < oldest);
        if (lines.Count > capacity)
            lines.RemoveRange(0, lines.Count - capacity);
    }
}
