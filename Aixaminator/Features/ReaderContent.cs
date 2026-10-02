using Aixaminator.Data;

namespace Aixaminator.Features;

/// <summary>A run of text within a paragraph, optionally highlighted with a #rrggbb colour.</summary>
public sealed record ReaderSegment(string Text, string? HighlightColour)
{
    public bool IsHighlighted => HighlightColour is not null;
}

/// <summary>A paragraph of the reader, split into segments by highlights.</summary>
/// <param name="Key">Zero-based paragraph index, as used by <see cref="HighlightLocation"/>.</param>
/// <param name="Offset">Offset of the paragraph's first character within <see cref="ReaderContent.Text"/>.</param>
public sealed record ReaderParagraph(int Key, int Offset, string Text, IReadOnlyList<ReaderSegment> Segments)
{
    public int Length => Text.Length;
}

/// <summary>
/// Display model for the text of a document part: its paragraphs, joined by <see cref="ParagraphSeparator"/>
/// into a single flat <see cref="Text"/>, with highlights applied.
/// <para>
/// Offsets into <see cref="Text"/> (e.g. a text selection) can be converted into a <see cref="HighlightLocation"/>,
/// which stays valid regardless of how the paragraphs are laid out.
/// </para>
/// </summary>
public sealed class ReaderContent
{
    public const string ParagraphSeparator = "\n\n";

    public static ReaderContent Empty { get; } = new([]);

    private ReaderContent(IReadOnlyList<ReaderParagraph> paragraphs)
    {
        Paragraphs = paragraphs;
        Text = string.Join(ParagraphSeparator, paragraphs.Select(p => p.Text));
    }

    public IReadOnlyList<ReaderParagraph> Paragraphs { get; }

    public string Text { get; }

    public bool IsEmpty => Paragraphs.Count == 0;

    /// <param name="partText">The raw text of the part.</param>
    /// <param name="highlights">Notes for this part; only highlights with a location are rendered.</param>
    public static ReaderContent Create(string partText, IEnumerable<Note> highlights)
    {
        var texts = ParagraphParser.Parse(partText);
        if (texts.Count == 0)
        {
            return Empty;
        }

        var regions = HighlightRegions.Compute(highlights, key => key >= 0 && key < texts.Count ? texts[key].Length : 0);

        var paragraphs = new List<ReaderParagraph>(texts.Count);
        var offset = 0;
        for (var key = 0; key < texts.Count; key++)
        {
            var text = texts[key];
            var segments = regions.TryGetValue(key, out var paragraphRegions)
                ? Segment(text, paragraphRegions)
                : [new ReaderSegment(text, null)];
            paragraphs.Add(new ReaderParagraph(key, offset, text, segments));
            offset += text.Length + ParagraphSeparator.Length;
        }

        return new ReaderContent(paragraphs);
    }

    /// <summary>
    /// Converts a range of <see cref="Text"/> (in either direction) into a location.
    /// Boundaries falling between paragraphs snap inwards to the nearest paragraph.
    /// </summary>
    /// <returns>The location, or null if the range doesn't cover any paragraph text.</returns>
    public HighlightLocation? GetLocation(int selectionStart, int selectionEnd)
    {
        var (start, end) = Normalise(selectionStart, selectionEnd);
        if (start == end)
        {
            return null;
        }

        var first = Paragraphs.FirstOrDefault(p => start < p.Offset + p.Length);
        var last = Paragraphs.LastOrDefault(p => end > p.Offset);
        if (first is null || last is null)
        {
            return null;
        }

        var startOffset = Math.Max(0, start - first.Offset);
        var endOffset = Math.Min(last.Length, end - last.Offset);
        if (first.Key > last.Key || (first.Key == last.Key && startOffset >= endOffset))
        {
            return null;
        }

        return new HighlightLocation(first.Key, startOffset, last.Key, endOffset);
    }

    /// <summary>Returns the text in the given range of <see cref="Text"/> (in either direction).</summary>
    public string GetText(int selectionStart, int selectionEnd)
    {
        var (start, end) = Normalise(selectionStart, selectionEnd);
        return Text[start..end];
    }

    private (int Start, int End) Normalise(int a, int b)
    {
        var start = Math.Clamp(Math.Min(a, b), 0, Text.Length);
        var end = Math.Clamp(Math.Max(a, b), 0, Text.Length);
        return (start, end);
    }

    private static List<ReaderSegment> Segment(string text, IEnumerable<HighlightRegion> regions)
    {
        var segments = new List<ReaderSegment>();
        var position = 0;
        foreach (var region in regions.OrderBy(r => r.Start))
        {
            if (region.Start > position)
            {
                segments.Add(new ReaderSegment(text[position..region.Start], null));
            }
            segments.Add(new ReaderSegment(text[region.Start..region.End], region.Colour));
            position = region.End;
        }

        if (position < text.Length)
        {
            segments.Add(new ReaderSegment(text[position..], null));
        }

        return segments;
    }
}
