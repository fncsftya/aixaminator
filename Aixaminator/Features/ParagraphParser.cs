using System.Text;

namespace Aixaminator.Features;

/// <summary>
/// Splits the raw text of a document part into paragraphs.
/// Text extracted from PDFs and similar sources is usually hard-wrapped, so consecutive lines are joined
/// until a blank line or a line ending in terminal punctuation is reached.
/// </summary>
/// <remarks>
/// The index of a paragraph in the result is its "key", which <see cref="Data.HighlightLocation"/> refers to.
/// </remarks>
public static class ParagraphParser
{
    private static readonly string[] SafeLineEndings =
    [
        "ie.", "i.e.", "eg.", "e.g.", "dr.", "mr.", "mrs.", "ms.", "miss.", "rev.", "etc.", "vs.", "st.",
        "cf.", "v.", "viz.", "sc.", "et al.", "ca.",
    ];

    private static readonly char[] TerminatingCharacters = ['.', '!', '?'];

    public static IReadOnlyList<string> Parse(string text)
    {
        var paragraphs = new List<string>();
        var current = new StringBuilder();

        void Flush()
        {
            var paragraph = current.ToString().Trim();
            if (paragraph.Length > 0)
            {
                paragraphs.Add(paragraph);
            }
            current.Clear();
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                Flush();
                continue;
            }

            current.Append(' ').Append(line);
            if (IsTerminated(line))
            {
                Flush();
            }
        }

        Flush();
        return paragraphs;
    }

    private static bool IsTerminated(string line)
    {
        if (SafeLineEndings.Any(ending => line.EndsWith(ending, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Walk back over trailing quotes, brackets etc. to the last meaningful character.
        for (var i = line.Length - 1; i >= 0; i--)
        {
            if (TerminatingCharacters.Contains(line[i]))
            {
                return true;
            }

            if (char.IsLetterOrDigit(line[i]))
            {
                return false;
            }
        }

        // A line made only of punctuation/symbols (e.g. "***") stands alone.
        return true;
    }
}
