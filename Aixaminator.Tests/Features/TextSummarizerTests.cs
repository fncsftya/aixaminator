using Aixaminator.Features;

namespace Aixaminator.Tests.Features;

public class TextSummarizerTests
{
    private const string Text =
        "The Roman Empire was one of the largest empires in ancient history. " +
        "It was founded in 27 BC when Augustus became the first emperor. " +
        "Ok. " +
        "In conclusion, the empire significantly shaped European law, language and architecture. " +
        "Many cities still contain Roman roads, aqueducts and amphitheatres today. " +
        "The Western Roman Empire fell in AD 476.";

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_text_has_no_highlights(string text)
    {
        Assert.Empty(TextSummarizer.ExtractKeyHighlights(text));
    }

    [Fact]
    public void Highlights_are_sentences_from_the_text_in_original_order()
    {
        var highlights = TextSummarizer.ExtractKeyHighlights(Text, maxHighlights: 10, maxLength: 10_000);

        Assert.NotEmpty(highlights);
        Assert.All(highlights, h => Assert.Contains(h, Text));
        var positions = highlights.Select(h => Text.IndexOf(h, StringComparison.Ordinal)).ToList();
        Assert.Equal(positions.Order(), positions);
    }

    [Fact]
    public void Very_short_sentences_are_skipped()
    {
        var highlights = TextSummarizer.ExtractKeyHighlights(Text, maxHighlights: 10, maxLength: 10_000);

        Assert.DoesNotContain("Ok.", highlights);
    }

    [Fact]
    public void Number_of_highlights_is_limited()
    {
        var highlights = TextSummarizer.ExtractKeyHighlights(Text, maxHighlights: 2, maxLength: 10_000);

        Assert.True(highlights.Count <= 2);
    }

    [Fact]
    public void Total_length_is_limited()
    {
        var highlights = TextSummarizer.ExtractKeyHighlights(Text, maxHighlights: 10, maxLength: 120);

        Assert.NotEmpty(highlights);
        Assert.True(highlights.Sum(h => h.Length) <= 120);
    }
}
