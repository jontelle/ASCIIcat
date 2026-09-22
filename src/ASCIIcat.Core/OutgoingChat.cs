using System.Text;

namespace ASCIIcat.Core;

public enum ChatDestination
{
    Echo,
    Party,
    Say,
    FreeCompany,
    Alliance,
    Yell,
    Shout,
}

public sealed record OutgoingChatLine(int ArtworkLineIndex, string Body, string Command, int Utf8Bytes);

public static class OutgoingChat
{
    public const int MaximumCommandBytes = 500;

    public static IReadOnlyList<OutgoingChatLine> Build(IReadOnlyList<string> lines, ChatDestination destination)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var prefix = destination switch
        {
            ChatDestination.Echo => "/echo ",
            ChatDestination.Party => "/p ",
            ChatDestination.Say => "/s ",
            ChatDestination.FreeCompany => "/fc ",
            ChatDestination.Alliance => "/a ",
            ChatDestination.Yell => "/y ",
            ChatDestination.Shout => "/sh ",
            _ => throw new ArgumentOutOfRangeException(nameof(destination)),
        };

        var result = new List<OutgoingChatLine>(lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.Length == 0)
                continue;

            // FFXIV's chat command parser trims ordinary spaces at message edges.
            // NBSP renders as a space in chat but survives that parser, so protect
            // only the edge padding that carries ASCII-art alignment information.
            var command = prefix + ProtectEdgeSpaces(line);
            result.Add(new OutgoingChatLine(index, line, command, Encoding.UTF8.GetByteCount(command)));
        }

        return result;
    }

    public static string ProtectEdgeSpaces(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Length == 0)
            return line;

        var firstNonSpace = 0;
        while (firstNonSpace < line.Length && line[firstNonSpace] == ' ')
            firstNonSpace++;

        var lastNonSpace = line.Length - 1;
        while (lastNonSpace >= firstNonSpace && line[lastNonSpace] == ' ')
            lastNonSpace--;

        if (firstNonSpace == 0 && lastNonSpace == line.Length - 1)
            return line;

        var protectedLine = line.ToCharArray();
        for (var index = 0; index < firstNonSpace; index++)
            protectedLine[index] = '\u00A0';
        for (var index = lastNonSpace + 1; index < protectedLine.Length; index++)
            protectedLine[index] = '\u00A0';

        return new string(protectedLine);
    }
}
