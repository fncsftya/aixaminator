using System.Text;
using System.Text.RegularExpressions;

namespace Aixaminator.Features;

/// <summary>
/// Deterministic clean-up applied to text returned by the AI "clean text" action.
/// </summary>
public static partial class TextCleaner
{
    /// <summary>
    /// Joins words hyphenated across line breaks and drops lines that look like
    /// page numbers or running headers/footers (e.g. "12", "12 Chapter One", "Chapter One 12").
    /// Lines are always terminated with <c>\n</c>.
    /// </summary>
    public static string CleanAiOutput(string text)
    {
        var lines = NewLine().Split(text);
        var cleaned = new StringBuilder();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.EndsWith('-') && i + 1 < lines.Length && lines[i + 1].Length > 0 && char.IsLetter(lines[i + 1][0]))
            {
                line = line.TrimEnd('-') + lines[i + 1];
                i++;
            }

            if (LeadingNumber().IsMatch(line) || TrailingNumber().IsMatch(line) || OnlyNumber().IsMatch(line))
            {
                line = string.Empty;
            }

            cleaned.Append(line).Append('\n');
        }

        return cleaned.ToString();
    }

    [GeneratedRegex(@"\r?\n")]
    private static partial Regex NewLine();

    [GeneratedRegex(@"^\d+\D+$")]
    private static partial Regex LeadingNumber();

    [GeneratedRegex(@"^\D+\d+$")]
    private static partial Regex TrailingNumber();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex OnlyNumber();
}
