using Aixaminator.Data;
using Aixaminator.Features;

namespace Aixaminator.Tests.Features;

public class ReaderContentTests
{
    private const string TwoParagraphs = "Hello world.\n\nSecond paragraph.";

    private static Note Highlight(int id, string colour, HighlightLocation location) => new()
    {
        Id = id,
        Kind = NoteKind.Highlight,
        Text = "x",
        HighlightColour = colour,
        Location = location,
    };

    [Fact]
    public void Empty_text_produces_empty_content()
    {
        var content = ReaderContent.Create("", []);

        Assert.True(content.IsEmpty);
        Assert.Empty(content.Paragraphs);
        Assert.Equal(string.Empty, content.Text);
    }

    [Fact]
    public void Paragraphs_are_joined_with_a_blank_line()
    {
        var content = ReaderContent.Create(TwoParagraphs, []);

        Assert.False(content.IsEmpty);
        Assert.Equal("Hello world.\n\nSecond paragraph.", content.Text);
        Assert.Equal([0, 1], content.Paragraphs.Select(p => p.Key));
        Assert.Equal([0, 14], content.Paragraphs.Select(p => p.Offset));
    }

    [Fact]
    public void Paragraph_without_highlights_is_a_single_plain_segment()
    {
        var content = ReaderContent.Create(TwoParagraphs, []);

        var segment = Assert.Single(content.Paragraphs[0].Segments);
        Assert.Equal(new ReaderSegment("Hello world.", null), segment);
    }

    [Fact]
    public void Highlights_split_paragraphs_into_segments()
    {
        var content = ReaderContent.Create(
            "This is a test paragraph.\n\nAnother test paragraph.",
            [
                Highlight(1, "#ff0000", new HighlightLocation(0, 0, 0, 4)),
                Highlight(2, "#00ff00", new HighlightLocation(0, 10, 0, 14)),
                Highlight(3, "#ff0000", new HighlightLocation(1, 0, 1, 12)),
            ]);

        Assert.Equal(
            [
                new ReaderSegment("This", "#ff0000"),
                new ReaderSegment(" is a ", null),
                new ReaderSegment("test", "#00ff00"),
                new ReaderSegment(" paragraph.", null),
            ],
            content.Paragraphs[0].Segments);
        Assert.Equal(
            [new ReaderSegment("Another test", "#ff0000"), new ReaderSegment(" paragraph.", null)],
            content.Paragraphs[1].Segments);
    }

    [Fact]
    public void Overlapping_highlights_render_newest_on_top()
    {
        var content = ReaderContent.Create(
            "Rather",
            [
                Highlight(1, "red", new HighlightLocation(0, 0, 0, 3)),
                Highlight(2, "blue", new HighlightLocation(0, 3, 0, 6)),
                Highlight(3, "green", new HighlightLocation(0, 2, 0, 4)),
            ]);

        Assert.Equal(
            [new ReaderSegment("Ra", "red"), new ReaderSegment("th", "green"), new ReaderSegment("er", "blue")],
            content.Paragraphs[0].Segments);
    }

    [Fact]
    public void Selection_within_a_paragraph_maps_to_a_location()
    {
        var content = ReaderContent.Create(TwoParagraphs, []);

        // "Second" in the second paragraph
        Assert.Equal(new HighlightLocation(1, 0, 1, 6), content.GetLocation(14, 20));
        Assert.Equal("Second", content.GetText(14, 20));
    }

    [Fact]
    public void Selection_across_paragraphs_maps_to_start_and_end_keys()
    {
        var content = ReaderContent.Create(TwoParagraphs, []);

        Assert.Equal(new HighlightLocation(0, 6, 1, 6), content.GetLocation(6, 20));
        Assert.Equal("world.\n\nSecond", content.GetText(6, 20));
    }

    [Fact]
    public void Backwards_selection_is_normalised()
    {
        var content = ReaderContent.Create(TwoParagraphs, []);

        Assert.Equal(content.GetLocation(6, 20), content.GetLocation(20, 6));
        Assert.Equal("world.\n\nSecond", content.GetText(20, 6));
    }

    [Fact]
    public void Selection_boundaries_inside_the_separator_snap_to_the_adjacent_paragraphs()
    {
        var content = ReaderContent.Create(TwoParagraphs, []);

        // start in the separator => start of the next paragraph
        Assert.Equal(new HighlightLocation(1, 0, 1, 6), content.GetLocation(13, 20));
        // end in the separator => end of the previous paragraph
        Assert.Equal(new HighlightLocation(0, 6, 0, 12), content.GetLocation(6, 13));
        // end exactly at the start of a paragraph => end of the previous paragraph
        Assert.Equal(new HighlightLocation(0, 6, 0, 12), content.GetLocation(6, 14));
    }

    [Fact]
    public void Empty_or_separator_only_selections_have_no_location()
    {
        var content = ReaderContent.Create(TwoParagraphs, []);

        Assert.Null(content.GetLocation(5, 5));
        Assert.Null(content.GetLocation(12, 14));
        Assert.Null(ReaderContent.Empty.GetLocation(0, 3));
    }

    [Fact]
    public void Out_of_range_selection_is_clamped()
    {
        var content = ReaderContent.Create(TwoParagraphs, []);

        Assert.Equal(new HighlightLocation(1, 7, 1, 17), content.GetLocation(21, 500));
        Assert.Equal("paragraph.", content.GetText(21, 500));
        Assert.Equal(new HighlightLocation(0, 0, 0, 5), content.GetLocation(-4, 5));
    }

    [Fact]
    public void Location_round_trips_into_a_highlight_segment()
    {
        var content = ReaderContent.Create(TwoParagraphs, []);
        var location = content.GetLocation(6, 20)!;

        var highlighted = ReaderContent.Create(TwoParagraphs, [Highlight(1, "#123456", location)]);

        Assert.Equal(
            [new ReaderSegment("Hello ", null), new ReaderSegment("world.", "#123456")],
            highlighted.Paragraphs[0].Segments);
        Assert.Equal(
            [new ReaderSegment("Second", "#123456"), new ReaderSegment(" paragraph.", null)],
            highlighted.Paragraphs[1].Segments);
    }
}
