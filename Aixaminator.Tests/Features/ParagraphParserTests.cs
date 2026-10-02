using Aixaminator.Features;

namespace Aixaminator.Tests.Features;

public class ParagraphParserTests
{
    [Fact]
    public void Empty_text_has_no_paragraphs()
    {
        Assert.Empty(ParagraphParser.Parse(""));
        Assert.Empty(ParagraphParser.Parse("  \n \n\t"));
    }

    [Fact]
    public void Blank_lines_separate_paragraphs()
    {
        var result = ParagraphParser.Parse("First paragraph\n\nSecond paragraph");

        Assert.Equal(["First paragraph", "Second paragraph"], result);
    }

    [Fact]
    public void Wrapped_lines_are_joined_into_one_paragraph()
    {
        var result = ParagraphParser.Parse("This sentence was\nhard wrapped across\nseveral lines.");

        Assert.Equal(["This sentence was hard wrapped across several lines."], result);
    }

    [Fact]
    public void A_line_ending_in_terminal_punctuation_ends_the_paragraph()
    {
        var result = ParagraphParser.Parse("Is this a question?\nYes it is!\nThe end.");

        Assert.Equal(["Is this a question?", "Yes it is!", "The end."], result);
    }

    [Fact]
    public void Trailing_quotes_after_terminal_punctuation_still_end_the_paragraph()
    {
        var result = ParagraphParser.Parse("He said \"hello.\"\nNext line");

        Assert.Equal(["He said \"hello.\"", "Next line"], result);
    }

    [Theory]
    [InlineData("See the work of Dr.")]
    [InlineData("for example, e.g.")]
    [InlineData("apples, pears etc.")]
    [InlineData("Smith et al.")]
    public void Common_abbreviations_do_not_end_the_paragraph(string line)
    {
        var result = ParagraphParser.Parse($"{line}\ncontinued here.");

        Assert.Equal([$"{line} continued here."], result);
    }

    [Fact]
    public void Windows_line_endings_are_supported()
    {
        var result = ParagraphParser.Parse("One\r\ntwo.\r\n\r\nThree");

        Assert.Equal(["One two.", "Three"], result);
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed()
    {
        var result = ParagraphParser.Parse("   indented line   \n   and more   ");

        Assert.Equal(["indented line and more"], result);
    }
}
