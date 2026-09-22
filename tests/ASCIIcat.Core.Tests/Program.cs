using ASCIIcat.Core;

var tests = new (string Name, Action Run)[]
{
    ("split preserves mixed newlines and final blank line", SplitPreservesLines),
    ("unicode and whitespace repository round trip is exact", RepositoryRoundTrip),
    ("tab expansion uses tab stops", TabExpansion),
    ("recent burst preserves chronological order", RecentBurst),
    ("sending session advances only on exact ordinal match", SendingSessionExactness),
    ("outgoing chat preserves line text behind a safe channel prefix", OutgoingChatPreservesText),
    ("outgoing chat skips blank lines without reordering", OutgoingChatSkipsBlanks),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{test.Name}: {exception.Message}");
        Console.WriteLine($"FAIL  {test.Name}: {exception.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"{failures.Count} ASCIIcat core test(s) failed.");
    return 1;
}

Console.WriteLine($"All {tests.Length} ASCIIcat core tests passed.");
return 0;

static void SplitPreservesLines()
{
    var lines = ExactText.SplitLines("  cat  \r\n\tface\n\u00A0tail\r");
    Equal(4, lines.Count);
    Equal("  cat  ", lines[0]);
    Equal("\tface", lines[1]);
    Equal("\u00A0tail", lines[2]);
    Equal(string.Empty, lines[3]);
}

static void RepositoryRoundTrip()
{
    var root = Path.Combine(Path.GetTempPath(), "ASCIIcat-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var repository = new ArtworkRepository(Path.Combine(root, "artworks.json"));
        var expected = new[] { "  /\\_/\\  ", "\t( o.o )", "\u00A0\u3000\u200B", string.Empty };
        var library = new ArtworkLibraryFile
        {
            Artworks =
            [
                new AsciiArtwork
                {
                    Name = "Exact cat",
                    Lines = [.. expected],
                    OriginalLines = [.. expected],
                },
            ],
        };

        repository.Save(library);
        var actual = repository.Load().Artworks.Single().Lines;
        True(ExactText.ExactLinesEqual(expected, actual), "The saved lines changed.");
    }
    finally
    {
        Directory.Delete(root, true);
    }
}

static void TabExpansion()
{
    Equal("a   b", ExactText.ExpandTabs("a\tb", 4));
    Equal("abcd    e", ExactText.ExpandTabs("abcd\te", 4));
}

static void RecentBurst()
{
    var buffer = new RecentChatBuffer(20, TimeSpan.FromHours(1));
    var now = DateTimeOffset.UtcNow;
    buffer.Add(now.AddSeconds(-1), "Say", "Other", "do not include");
    var first = buffer.Add(now, "Say", "Mew Mew", "one");
    var second = buffer.Add(now.AddSeconds(2), "Say", "Mew Mew", "two");
    buffer.Add(now.AddSeconds(3), "Say", "Other", "interruption");

    var burst = buffer.SuggestBurst(second.Sequence);
    Equal(2, burst.Count);
    Equal(first.Sequence, burst[0].Sequence);
    Equal(second.Sequence, burst[1].Sequence);
}

static void SendingSessionExactness()
{
    var session = new SendingSession();
    session.Start(new AsciiArtwork { Lines = ["  cat", "tail  "] });
    True(!session.ObserveExactChatEcho("cat"), "Trimmed text must not match.");
    Equal(0, session.NextLineIndex);
    True(session.ObserveExactChatEcho("  cat"), "Exact first line should match.");
    Equal(1, session.NextLineIndex);
    True(!session.ObserveExactChatEcho("tail"), "Missing trailing spaces must not match.");
    True(session.ObserveExactChatEcho("tail  "), "Exact second line should match.");
    True(session.IsComplete, "Session should be complete.");
}

static void OutgoingChatPreservesText()
{
    var plan = OutgoingChat.Build(["  /\\_/\\  "], ChatDestination.Party);
    Equal(1, plan.Count);
    Equal("  /\\_/\\  ", plan[0].Body);
    Equal("/p \u00A0\u00A0/\\_/\\\u00A0\u00A0", plan[0].Command);
    Equal("\u00A0\u00A0", OutgoingChat.ProtectEdgeSpaces("  "));
    Equal("a  b", OutgoingChat.ProtectEdgeSpaces("a  b"));
}

static void OutgoingChatSkipsBlanks()
{
    var plan = OutgoingChat.Build(["first", string.Empty, "third"], ChatDestination.Echo);
    Equal(2, plan.Count);
    Equal(0, plan[0].ArtworkLineIndex);
    Equal(2, plan[1].ArtworkLineIndex);
    Equal("/echo third", plan[1].Command);
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
}

static void True(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
