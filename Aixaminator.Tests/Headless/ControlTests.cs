using Aixaminator.Behaviors;
using Aixaminator.Controls;
using Aixaminator.Converters;
using Aixaminator.Data;
using Aixaminator.Features;
using Aixaminator.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

namespace Aixaminator.Tests.Headless;

[Collection(HeadlessCollection.Name)]
public class ControlTests
{
    private static ReaderContent Content(string text, params Note[] highlights) => ReaderContent.Create(text, highlights);

    private static Note Highlight(string colour, HighlightLocation location) => new()
    {
        Id = 1, Kind = NoteKind.Highlight, Text = "x", HighlightColour = colour, Location = location,
    };

    [AvaloniaFact]
    public void Reader_text_block_shows_paragraphs_separated_by_blank_lines()
    {
        var block = new ReaderTextBlock { Source = Content("First paragraph.\n\nSecond paragraph.") };

        Assert.Equal("First paragraph.\n\nSecond paragraph.", block.Inlines!.Text);
    }

    [AvaloniaFact]
    public void Reader_text_block_draws_highlights_behind_the_text()
    {
        var block = new ReaderTextBlock
        {
            Source = Content("Hello world.", Highlight("#ff0000", new HighlightLocation(0, 6, 0, 11))),
        };

        var runs = block.Inlines!.OfType<Run>().ToList();
        Assert.Equal(["Hello ", "world", "."], runs.Select(r => r.Text));
        var highlighted = Assert.IsAssignableFrom<ISolidColorBrush>(runs[1].Background);
        Assert.Equal(Color.Parse("#ff0000"), highlighted.Color);
        Assert.Same(Brushes.White, runs[1].Foreground);
        Assert.Null(runs[0].Background);
    }

    [AvaloniaFact]
    public void Reader_text_block_rebuilds_when_the_content_changes()
    {
        var block = new ReaderTextBlock { Source = Content("One.") };

        block.Source = Content("Two.");

        Assert.Equal("Two.", block.Inlines!.Text);

        block.Source = null;
        Assert.Empty(block.Inlines!);
    }

    [AvaloniaFact]
    public void Reader_selection_offsets_match_the_content()
    {
        var content = Content("Hello world.\n\nSecond paragraph.");
        var block = new ReaderTextBlock { Source = content, Width = 400 };
        block.ShowInWindow();

        block.SelectionStart = 14;
        block.SelectionEnd = 20;

        Assert.Equal(content.GetText(14, 20), block.SelectedText);
        Assert.Equal("Second", block.SelectedText);
    }

    [AvaloniaFact]
    public void Reader_text_block_uses_the_selectable_text_block_theme()
    {
        var block = new ReaderTextBlock { Source = Content("Text.") };
        block.ShowInWindow();

        // The theme provides the selection brush; without it selections would be invisible.
        Assert.NotNull(block.SelectionBrush);
    }

    [AvaloniaTheory]
    [InlineData("#00ff00", true)]
    [InlineData("green", false)]
    [InlineData(null, false)]
    public void Hex_colours_convert_to_brushes(string? hex, bool converts)
    {
        var brush = AppConverters.HexToBrush.Convert(hex, typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture);

        if (converts)
        {
            Assert.Equal(Color.Parse(hex!), Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color);
        }
        else
        {
            Assert.Null(brush);
        }
    }

    [AvaloniaFact]
    public void Every_reader_font_style_converts_to_a_font_family()
    {
        foreach (var (name, stack) in ApplicationSettings.FontFamilies)
        {
            var family = Assert.IsType<FontFamily>(
                AppConverters.ToFontFamily.Convert(stack, typeof(FontFamily), null, System.Globalization.CultureInfo.InvariantCulture));
            Assert.NotEmpty(family.FamilyNames);

            // Laying out text in each style must work, falling back to the default font when needed.
            var text = new TextBlock { Text = name, FontFamily = family };
            text.ShowInWindow(400, 100).Close();
            Assert.True(text.DesiredSize.Width > 0, name);
        }
    }

    [AvaloniaFact]
    public void Scroll_reset_returns_a_scroll_viewer_to_the_top_when_its_trigger_changes()
    {
        var scroller = new ScrollViewer
        {
            Height = 100,
            Content = new Border { Height = 1000 },
        };
        scroller.ShowInWindow(300, 100);
        scroller.Offset = new Vector(0, 500);
        Assert.Equal(500, scroller.Offset.Y);

        ScrollReset.SetTrigger(scroller, 1);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void Scroll_reset_moves_a_text_box_caret_to_the_start()
    {
        var textBox = new TextBox { Text = "line\nline\nline", AcceptsReturn = true };
        textBox.ShowInWindow(300, 100);
        textBox.CaretIndex = textBox.Text.Length;

        ScrollReset.SetTrigger(textBox, "part 2");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, textBox.CaretIndex);
    }

    [AvaloniaFact]
    public void Icons_and_styles_are_available()
    {
        foreach (var key in new[] { "Icon.Circle", "Icon.CheckCircle", "Icon.Gear", "Icon.Highlighter", "Icon.Close", "Icon.Note", "Icon.Question", "Icon.EyeSlash" })
        {
            Assert.True(Application.Current!.TryGetResource(key, null, out var icon), key);
            Assert.IsAssignableFrom<Geometry>(icon);
        }
    }
}
