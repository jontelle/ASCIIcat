using System.Text;

namespace ASCIIcat.Core;

public sealed record LineDiagnostics(
    int LineNumber,
    int Utf16Length,
    int Utf8Bytes,
    int LeadingSpaces,
    int TrailingSpaces,
    bool HasTabs,
    bool HasNonBreakingSpaces,
    bool HasIdeographicSpaces,
    bool HasZeroWidthCharacters,
    bool HasOtherControls);

public static class ExactText
{
    public static List<string> SplitLines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = new List<string>();
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('\r' or '\n'))
                continue;

            lines.Add(text[start..i]);
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                i++;

            start = i + 1;
        }

        lines.Add(text[start..]);
        return lines;
    }

    public static string JoinLines(IEnumerable<string> lines, string newline = "\r\n")
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(newline);
        return string.Join(newline, lines);
    }

    public static string ExpandTabs(string text, int tabWidth)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (tabWidth is < 1 or > 16)
            throw new ArgumentOutOfRangeException(nameof(tabWidth));

        var output = new StringBuilder(text.Length);
        var column = 0;

        foreach (var c in text)
        {
            switch (c)
            {
                case '\t':
                    var spaces = tabWidth - (column % tabWidth);
                    output.Append(' ', spaces);
                    column += spaces;
                    break;
                case '\r':
                    output.Append(c);
                    column = 0;
                    break;
                case '\n':
                    output.Append(c);
                    column = 0;
                    break;
                default:
                    output.Append(c);
                    column++;
                    break;
            }
        }

        return output.ToString();
    }

    public static string ShowInvisibles(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var output = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            output.Append(c switch
            {
                ' ' => '·',
                '\t' => '⇥',
                '\u00A0' => '⍽',
                '\u3000' => '□',
                '\u200B' => "⟦ZWSP⟧",
                '\u200C' => "⟦ZWNJ⟧",
                '\u200D' => "⟦ZWJ⟧",
                '\u2060' => "⟦WJ⟧",
                '\r' => string.Empty,
                '\n' => "↵\n",
                _ => c.ToString(),
            });
        }

        return output.ToString();
    }

    public static LineDiagnostics Diagnose(string line, int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(line);
        var leading = line.TakeWhile(c => c == ' ').Count();
        var trailing = line.Reverse().TakeWhile(c => c == ' ').Count();

        return new LineDiagnostics(
            lineNumber,
            line.Length,
            Encoding.UTF8.GetByteCount(line),
            leading,
            trailing,
            line.Contains('\t'),
            line.Contains('\u00A0'),
            line.Contains('\u3000'),
            line.Any(c => c is '\u200B' or '\u200C' or '\u200D' or '\u2060'),
            line.Any(c => char.IsControl(c) && c != '\t'));
    }

    public static bool ExactLinesEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}

