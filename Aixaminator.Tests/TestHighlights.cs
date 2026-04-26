using Aixaminator.Features;
using Aixaminator.Data;
using Xunit;
using Microsoft.Maui.Controls;

namespace Aixaminator.Tests;

public class TestHighlights
{
    private (int, int, string) CreateRegion(int start, int end, string? colour = null) => (Start: start, End: end, Colour: colour);

    [Fact]
    public void TestRebuildNoteRegions_NoNotes_ReturnsEmpty()
    {
        var notes = new List<Note>();
        var paragraphLengths = new Dictionary<int, int>();

        var result = Documents.RebuildNoteRegions(notes, paragraphLengths);

        Assert.Empty(result);
    }

    [Fact]
    public void TestRebuildNoteRegions_SingleNote_ReturnsCorrectRegion()
    {
        var notes = new List<Note>
        {
            new Note
            {
                Id = 1,
                Location = new HighlightLocation
                {
                    StartKey = 1,
                    EndKey = 1,
                    StartOffset = 0,
                    EndOffset = 5
                }
            }
        };
        var paragraphLengths = new Dictionary<int, int>
        {
            { 1, 10 }
        };

        var result = Documents.RebuildNoteRegions(notes, paragraphLengths);

        Assert.Single(result);
        Assert.Equal(CreateRegion(0, 5), result[1].First());
    }

    [Fact]
    public void TestRebuildNoteRegions_SingleNote_SpansTwoParagraphs()
    {
        var notes = new List<Note>
        {
            new Note
            {
                Id = 1,
                Location = new HighlightLocation
                {
                    StartKey = 1,
                    EndKey = 2,
                    StartOffset = 5,
                    EndOffset = 5
                }
            }
        };

        var paragraphLengths = new Dictionary<int, int>
        {
            { 1, 10 },
            { 2, 10 }
        };

        var result = Documents.RebuildNoteRegions(notes, paragraphLengths);

        Assert.Equal(CreateRegion(5, 10), result[1].First());
        Assert.Equal(CreateRegion(0, 5), result[2].First());
    }

    [Fact]
    public void TestRebuildNoteRegions_SingleNote_SpansThreeParagraphs()
    {
        var notes = new List<Note>
        {
            new Note
            {
                Id = 1,
                Location = new HighlightLocation
                {
                    StartKey = 1,
                    EndKey = 3,
                    StartOffset = 5,
                    EndOffset = 5
                }
            }
        };

        var paragraphLengths = new Dictionary<int, int>
        {
            { 1, 10 },
            { 2, 40 },
            { 3, 10 }
        };

        var result = Documents.RebuildNoteRegions(notes, paragraphLengths);

        Assert.Equal(CreateRegion(5, 10), result[1].First());
        Assert.Equal(CreateRegion(0, 40), result[2].First());
        Assert.Equal(CreateRegion(0, 5), result[3].First());
    }

    [Fact]
    public void TestRebuildNoteRegions_TwoNotes_SameParagraph()
    {
        var notes = new List<Note>
        {
            new Note
            {
                Id = 1,
                Location = new HighlightLocation
                {
                    StartKey = 1,
                    EndKey = 1,
                    StartOffset = 0,
                    EndOffset = 5
                }
            },
            new Note
            {
                Id = 2,
                Location = new HighlightLocation
                {
                    StartKey = 1,
                    EndKey = 1,
                    StartOffset = 10,
                    EndOffset = 15
                }
            }
        };
        var paragraphLengths = new Dictionary<int, int>
        {
            { 1, 20 }
        };
        var result = Documents.RebuildNoteRegions(notes, paragraphLengths);
        Assert.Equal([CreateRegion(10, 15), CreateRegion(0, 5)], result[1]);
    }

    [Fact]
    public void TestRebuildNoteRegions_TwoNotes_SameParagraph_WithOverlap()
    {
        var notes = new List<Note>
        {
            new Note
            {
                Id = 1,
                Location = new HighlightLocation
                {
                    StartKey = 1,
                    EndKey = 1,
                    StartOffset = 5,
                    EndOffset = 12
                }
            },
            new Note
            {
                Id = 2,
                Location = new HighlightLocation
                {
                    StartKey = 1,
                    EndKey = 1,
                    StartOffset = 10,
                    EndOffset = 15
                }
            }
        };
        var paragraphLengths = new Dictionary<int, int>
        {
            { 1, 20 }
        };
        var result = Documents.RebuildNoteRegions(notes, paragraphLengths);
        Assert.Equal([CreateRegion(10, 15), CreateRegion(5, 10)], result[1]);
    }

    [Fact]
    public void TestRebuildNoteRegions_TwoNotes_OneParagraph_EarlierSurroundsLater()
    {
        var notes = new List<Note>
        {
            new Note
            {
                Id = 1,
                Location = new HighlightLocation
                {
                    StartKey = 1,
                    EndKey = 1,
                    StartOffset = 7,
                    EndOffset = 17
                }
            },
            new Note
            {
                Id = 2,
                Location = new HighlightLocation
                {
                    StartKey = 1,
                    EndKey = 1,
                    StartOffset = 10,
                    EndOffset = 15
                }
            }
        };
        var paragraphLengths = new Dictionary<int, int>
        {
            { 1, 20 }
        };
        var result = Documents.RebuildNoteRegions(notes, paragraphLengths);
        Assert.Equal([CreateRegion(10, 15), CreateRegion(7, 10), CreateRegion(15, 17)], result[1]);
    }

    [Fact]
    public void TestRebuildNoteRegions_ThreeNotes_OneParagraph_MultipleSurrounds()
    {
        var notes = new List<Note>
        {
            new Note
            {
                Id = 1,
                Location = new HighlightLocation
                {
                    StartKey = 1,
                    EndKey = 1,
                    StartOffset = 0,
                    EndOffset = 28
                }
            },
            new Note
            {
                Id = 2,
                Location = new HighlightLocation
                {
                    StartKey = 1,
                    EndKey = 1,
                    StartOffset = 10,
                    EndOffset = 15
                }
            },
            new Note
            {
                Id = 3,
                Location = new HighlightLocation
                {
                    StartKey = 1,
                    EndKey = 1,
                    StartOffset = 20,
                    EndOffset = 25
                }
            }
        };
        var paragraphLengths = new Dictionary<int, int>
        {
            { 1, 30 }
        };
        var result = Documents.RebuildNoteRegions(notes, paragraphLengths);
        Assert.Equal([CreateRegion(20, 25), CreateRegion(10, 15), CreateRegion(0, 10), CreateRegion(15, 20), CreateRegion(25, 28)], result[1]);
    }

    [Fact]
    public void TestRebuildNoteRegions_OverlapSpansParagraphs()
    {
        var notes = new List<Note>
        {
            new Note
            {
                Id = 1,
                HighlightColour = "red",
                Location = new HighlightLocation
                {
                    StartKey = 1,
                    EndKey = 2,
                    StartOffset = 5,
                    EndOffset = 15
                }
            },
            new Note
            {
                Id = 2,
                HighlightColour = "green",
                Location = new HighlightLocation
                {
                    StartKey = 1,
                    EndKey = 2,
                    StartOffset = 10,
                    EndOffset = 10
                }
            }
        };
        var paragraphLengths = new Dictionary<int, int>
        {
            { 1, 20 },
            { 2, 20 }
        };

        var result = Documents.RebuildNoteRegions(notes, paragraphLengths);
        List<(int Start, int End, string Colour)> expectedRegion1 = [CreateRegion(10, 20, "green"), CreateRegion(5, 10, "red")];
        List<(int Start, int End, string Colour)> expectedRegion2 = [CreateRegion(0, 10, "green"), CreateRegion(10, 15, "red")];
        Assert.Equal(expectedRegion1, result[1]);
        Assert.Equal(expectedRegion2, result[2]);

        var regions = new Dictionary<int, List<(int, int, string)>>
        {
            { 1, expectedRegion1 },
            { 2, expectedRegion2 },
        };
        List<string> paragraphs = ["", "aaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbb"];
        var outputParagraphs = Documents.ApplyRegionsToParagraphs(paragraphs, regions);

        Assert.Equal($"""aaaaa{HighlightStart("red")}aaaaa{HighlightEnd()}{HighlightStart("green")}aaaaaaaaaa{HighlightEnd()}""", outputParagraphs[1]);
        Assert.Equal($"""{HighlightStart("green")}bbbbbbbbbb{HighlightEnd()}{HighlightStart("red")}bbbbb{HighlightEnd()}bbbbb""", outputParagraphs[2]);
    }

    [Fact]
    public void TestRebuildNoteRegions_OverlapRegression()
    {
        var notes = new List<Note>
        {
            new Note
            {
                Id = 1,
                HighlightColour = "red",
                Location = new HighlightLocation
                {
                    StartKey = 0,
                    EndKey = 0,
                    StartOffset = 0,
                    EndOffset = 3
                }
            },
            new Note
            {
                Id = 2,
                HighlightColour = "blue",
                Location = new HighlightLocation
                {
                    StartKey = 0,
                    EndKey = 0,
                    StartOffset = 3,
                    EndOffset = 6
                }
            },
            new Note
            {
                Id = 3,
                HighlightColour = "green",
                Location = new HighlightLocation
                {
                    StartKey = 0,
                    EndKey = 0,
                    StartOffset = 2,
                    EndOffset = 4
                }
            }
        };
        var paragraphLengths = new Dictionary<int, int>
        {
            { 0, 20 }
        };

        var result = Documents.RebuildNoteRegions(notes, paragraphLengths);
        List<(int Start, int End, string Colour)> expectedRegions = [CreateRegion(2, 4, "green"), CreateRegion(4, 6, "blue"), CreateRegion(0, 2, "red")];
        Assert.Equal(expectedRegions, result[0]);

        var regions = new Dictionary<int, List<(int, int, string)>>
        {
            { 0, expectedRegions },
        };
        List<string> paragraphs = ["Rather"];
        var outputParagraphs = Documents.ApplyRegionsToParagraphs(paragraphs, regions);
        Assert.Equal($"""{HighlightStart("red")}Ra{HighlightEnd()}{HighlightStart("green")}th{HighlightEnd()}{HighlightStart("blue")}er{HighlightEnd()}""", outputParagraphs[0]);
    }

    [Fact]
    public void TestRebuildNoteRegions_HighlightAdjacent()
    {
        var notes = new List<Note>
        {
            new Note
            {
                Id = 1,
                HighlightColour = "red",
                Location = new HighlightLocation
                {
                    StartKey = 0,
                    EndKey = 0,
                    StartOffset = 0,
                    EndOffset = 3
                }
            },
            new Note
            {
                Id = 2,
                HighlightColour = "blue",
                Location = new HighlightLocation
                {
                    StartKey = 0,
                    EndKey = 0,
                    StartOffset = 3,
                    EndOffset = 6
                }
            }
        };
        var paragraphLengths = new Dictionary<int, int>
        {
            { 0, 20 }
        };

        var result = Documents.RebuildNoteRegions(notes, paragraphLengths);
        List<(int Start, int End, string Colour)> expectedRegions = [CreateRegion(3, 6, "blue"), CreateRegion(0, 3, "red")];
        Assert.Equal(expectedRegions, result[0]);

        var regions = new Dictionary<int, List<(int, int, string)>>
        {
            { 0, expectedRegions },
        };
        List<string> paragraphs = ["Rather"];
        var outputParagraphs = Documents.ApplyRegionsToParagraphs(paragraphs, regions);
        Assert.Equal($"""{HighlightStart("red")}Rat{HighlightEnd()}{HighlightStart("blue")}her{HighlightEnd()}""", outputParagraphs[0]);
    }

    private string HighlightStart(string colour) => $"<span class=\"highlight-text {colour}\">";
    private string HighlightEnd() => "</span>";

    [Fact]
    public void TestApplyRegionsToParagraphs()
    {
        var regions = new Dictionary<int, List<(int, int, string)>>
        {
            { 0, new List<(int, int, string)> { CreateRegion(0, 4, "red"), CreateRegion(10, 14, "green") } },
            { 1, new List<(int, int, string)> { CreateRegion(0, 12, "red") } }
        };

        List<string> paragraphs = ["This is a test paragraph.", "Another test paragraph."];

        var result = Documents.ApplyRegionsToParagraphs(paragraphs, regions);

        Assert.Equal($"""{HighlightStart("red")}This{HighlightEnd()} is a {HighlightStart("green")}test{HighlightEnd()} paragraph.""", result[0]);
        Assert.Equal($"""{HighlightStart("red")}Another test{HighlightEnd()} paragraph.""", result[1]);
    }

    [Fact]
    public void TestApplyRegionsToParagraphs_OverlapLeft()
    {
        var regions = new Dictionary<int, List<(int, int, string)>>
        {
            { 0, new List<(int, int, string)> { CreateRegion(10, 24, "green"), CreateRegion(0, 10, "red") } },
        };

        List<string> paragraphs = ["This is a test paragraph."];
        var result = Documents.ApplyRegionsToParagraphs(paragraphs, regions);
        Assert.Equal($"""{HighlightStart("red")}This is a {HighlightEnd()}{HighlightStart("green")}test paragraph{HighlightEnd()}.""", result[0]);
    }
}
