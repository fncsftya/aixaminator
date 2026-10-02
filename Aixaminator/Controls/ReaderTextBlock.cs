using Aixaminator.Converters;
using Aixaminator.Features;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Aixaminator.Controls;

/// <summary>
/// Displays <see cref="ReaderContent"/>: paragraphs separated by blank lines, with highlights drawn behind the text.
/// The text is selectable; <see cref="SelectableTextBlock.SelectionStart"/> and
/// <see cref="SelectableTextBlock.SelectionEnd"/> are offsets into <see cref="ReaderContent.Text"/>.
/// </summary>
public class ReaderTextBlock : SelectableTextBlock
{
    public static readonly StyledProperty<ReaderContent?> SourceProperty =
        AvaloniaProperty.Register<ReaderTextBlock, ReaderContent?>(nameof(Source));

    /// <summary>Text drawn over highlights.</summary>
    public static readonly StyledProperty<IBrush> HighlightForegroundProperty =
        AvaloniaProperty.Register<ReaderTextBlock, IBrush>(nameof(HighlightForeground), Brushes.White);

    public ReaderContent? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public IBrush HighlightForeground
    {
        get => GetValue(HighlightForegroundProperty);
        set => SetValue(HighlightForegroundProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty || change.Property == HighlightForegroundProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        var inlines = new InlineCollection();
        var paragraphs = Source?.Paragraphs ?? [];
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (i > 0)
            {
                // Must match ReaderContent.ParagraphSeparator so selection offsets line up with the content.
                inlines.Add(new Run(ReaderContent.ParagraphSeparator));
            }

            foreach (var segment in paragraphs[i].Segments)
            {
                var run = new Run(segment.Text);
                if (AppConverters.ToBrush(segment.HighlightColour) is { } background)
                {
                    run.Background = background;
                    run.Foreground = HighlightForeground;
                }
                inlines.Add(run);
            }
        }

        Inlines = inlines;
    }
}
