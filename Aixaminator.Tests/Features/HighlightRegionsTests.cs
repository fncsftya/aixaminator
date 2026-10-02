using Aixaminator.Data;
using Aixaminator.Features;

namespace Aixaminator.Tests.Features;

public class HighlightRegionsTests
{
    private static Note Highlight(int id, string colour, int startKey, int startOffset, int endKey, int endOffset) => new()
    {
        Id = id,
        Kind = NoteKind.Highlight,
        Text = "highlight",
        HighlightColour = colour,
        Location = new HighlightLocation(startKey, startOffset, endKey, endOffset),
    };

    private static Func<int, int> Lengths(params (int Key, int Length)[] lengths)
    {
        var map = lengths.ToDictionary(l => l.Key, l => l.Length);
        return key => map.TryGetValue(key, out var length) ? length : 0;
    }

    [Fact]
    public void No_notes_produces_no_regions()
    {
        var result = HighlightRegions.Compute([], Lengths());

        Assert.Empty(result);
    }

    [Fact]
    public void Single_note_within_one_paragraph()
    {
        var result = HighlightRegions.Compute([Highlight(1, "red", 1, 0, 1, 5)], Lengths((1, 10)));

        var region = Assert.Single(result);
        Assert.Equal(1, region.Key);
        Assert.Equal([new HighlightRegion(0, 5, "red")], region.Value);
    }

    [Fact]
    public void Single_note_spanning_two_paragraphs()
    {
        var result = HighlightRegions.Compute([Highlight(1, "red", 1, 5, 2, 5)], Lengths((1, 10), (2, 10)));

        Assert.Equal([new HighlightRegion(5, 10, "red")], result[1]);
        Assert.Equal([new HighlightRegion(0, 5, "red")], result[2]);
    }

    [Fact]
    public void Single_note_spanning_three_paragraphs_covers_the_middle_one_entirely()
    {
        var result = HighlightRegions.Compute([Highlight(1, "red", 1, 5, 3, 5)], Lengths((1, 10), (2, 40), (3, 10)));

        Assert.Equal([new HighlightRegion(5, 10, "red")], result[1]);
        Assert.Equal([new HighlightRegion(0, 40, "red")], result[2]);
        Assert.Equal([new HighlightRegion(0, 5, "red")], result[3]);
    }

    [Fact]
    public void Two_separate_notes_in_the_same_paragraph_are_sorted_by_start()
    {
        var result = HighlightRegions.Compute(
            [Highlight(1, "red", 1, 6, 1, 9), Highlight(2, "green", 1, 0, 1, 5)],
            Lengths((1, 10)));

        Assert.Equal([new HighlightRegion(0, 5, "green"), new HighlightRegion(6, 9, "red")], result[1]);
    }

    [Fact]
    public void Newer_note_takes_precedence_where_notes_overlap()
    {
        var result = HighlightRegions.Compute(
            [Highlight(1, "red", 1, 0, 1, 5), Highlight(2, "green", 1, 3, 1, 8)],
            Lengths((1, 10)));

        Assert.Equal([new HighlightRegion(0, 3, "red"), new HighlightRegion(3, 8, "green")], result[1]);
    }

    [Fact]
    public void Older_note_surrounding_a_newer_note_is_split_around_it()
    {
        var result = HighlightRegions.Compute(
            [Highlight(1, "red", 1, 0, 1, 10), Highlight(2, "green", 1, 3, 1, 6)],
            Lengths((1, 10)));

        Assert.Equal(
            [new HighlightRegion(0, 3, "red"), new HighlightRegion(3, 6, "green"), new HighlightRegion(6, 10, "red")],
            result[1]);
    }

    [Fact]
    public void Older_note_inside_a_newer_note_is_hidden()
    {
        var result = HighlightRegions.Compute(
            [Highlight(1, "red", 1, 3, 1, 6), Highlight(2, "green", 1, 0, 1, 10)],
            Lengths((1, 10)));

        Assert.Equal([new HighlightRegion(0, 10, "green")], result[1]);
    }

    [Fact]
    public void Multiple_nested_notes_resolve_in_order_of_recency()
    {
        var result = HighlightRegions.Compute(
            [
                Highlight(1, "red", 0, 0, 0, 20),
                Highlight(2, "green", 0, 2, 0, 18),
                Highlight(3, "blue", 0, 4, 0, 8),
            ],
            Lengths((0, 20)));

        Assert.Equal(
            [
                new HighlightRegion(0, 2, "red"),
                new HighlightRegion(2, 4, "green"),
                new HighlightRegion(4, 8, "blue"),
                new HighlightRegion(8, 18, "green"),
                new HighlightRegion(18, 20, "red"),
            ],
            result[0]);
    }

    [Fact]
    public void Overlap_spanning_paragraphs()
    {
        var result = HighlightRegions.Compute(
            [Highlight(1, "red", 1, 5, 2, 15), Highlight(2, "green", 1, 10, 2, 10)],
            Lengths((1, 20), (2, 20)));

        Assert.Equal([new HighlightRegion(5, 10, "red"), new HighlightRegion(10, 20, "green")], result[1]);
        Assert.Equal([new HighlightRegion(0, 10, "green"), new HighlightRegion(10, 15, "red")], result[2]);
    }

    [Fact]
    public void Overlap_regression_three_notes()
    {
        var result = HighlightRegions.Compute(
            [Highlight(1, "red", 0, 0, 0, 3), Highlight(2, "blue", 0, 3, 0, 6), Highlight(3, "green", 0, 2, 0, 4)],
            Lengths((0, 20)));

        Assert.Equal(
            [new HighlightRegion(0, 2, "red"), new HighlightRegion(2, 4, "green"), new HighlightRegion(4, 6, "blue")],
            result[0]);
    }

    [Fact]
    public void Adjacent_notes_do_not_affect_each_other()
    {
        var result = HighlightRegions.Compute(
            [Highlight(1, "red", 0, 0, 0, 3), Highlight(2, "blue", 0, 3, 0, 6)],
            Lengths((0, 20)));

        Assert.Equal([new HighlightRegion(0, 3, "red"), new HighlightRegion(3, 6, "blue")], result[0]);
    }

    [Fact]
    public void Notes_without_a_location_or_colour_are_ignored()
    {
        var withoutLocation = Highlight(1, "red", 0, 0, 0, 3);
        withoutLocation.Location = null;
        var plainNote = new Note { Id = 2, Kind = NoteKind.Note, Text = "note", Location = new HighlightLocation(0, 0, 0, 5) };

        var result = HighlightRegions.Compute([withoutLocation, plainNote], Lengths((0, 20)));

        Assert.Empty(result);
    }

    [Fact]
    public void Regions_are_clamped_to_the_paragraph_and_empty_regions_dropped()
    {
        // e.g. a stale location pointing past the end of the text
        var result = HighlightRegions.Compute(
            [Highlight(1, "red", 0, 5, 0, 50), Highlight(2, "blue", 3, 0, 3, 4)],
            Lengths((0, 10)));

        var region = Assert.Single(result);
        Assert.Equal([new HighlightRegion(5, 10, "red")], region.Value);
    }
}
