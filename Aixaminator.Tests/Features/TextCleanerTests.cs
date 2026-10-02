using Aixaminator.Features;

namespace Aixaminator.Tests.Features;

public class TextCleanerTests
{
    private static string[] Lines(string text) => text.Split('\n');

    [Fact]
    public void Plain_text_is_preserved()
    {
        var result = TextCleaner.CleanAiOutput("First line.\n\nSecond line.");

        Assert.Equal(["First line.", "", "Second line.", ""], Lines(result));
    }

    [Fact]
    public void Hyphenated_words_split_across_lines_are_joined()
    {
        var result = TextCleaner.CleanAiOutput("An extra-\nordinary result.");

        Assert.Equal(["An extraordinary result.", ""], Lines(result));
    }

    [Fact]
    public void Hyphen_followed_by_a_non_letter_is_not_joined()
    {
        var result = TextCleaner.CleanAiOutput("A list -\n- item");

        Assert.Equal(["A list -", "- item", ""], Lines(result));
    }

    [Theory]
    [InlineData("42")]
    [InlineData("12 Chapter Title")]
    [InlineData("Chapter Title 12")]
    public void Page_numbers_and_running_headers_are_removed(string noise)
    {
        var result = TextCleaner.CleanAiOutput($"Before.\n{noise}\nAfter.");

        Assert.Equal(["Before.", "", "After.", ""], Lines(result));
    }

    [Fact]
    public void Windows_line_endings_are_normalised()
    {
        var result = TextCleaner.CleanAiOutput("One\r\nTwo");

        Assert.Equal(["One", "Two", ""], Lines(result));
    }
}
