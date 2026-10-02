using System.Text.RegularExpressions;
using Aixaminator.Models.Ai;

namespace Aixaminator.Models;

/// <summary>User settings, persisted as JSON by <see cref="Services.ISettingsService"/>.</summary>
public sealed partial class ApplicationSettings
{
    public const string DefaultBackgroundColor = "#ffffff";
    public const string DefaultForegroundColor = "#000000";
    public const int DefaultFontSize = 16;
    public const string DefaultFontFamily = "Transitional";

    public AiSettings Ai { get; set; } = new();

    /// <summary>Background colour of the reader (#rrggbb).</summary>
    public string BackgroundColor { get; set; } = DefaultBackgroundColor;

    /// <summary>Text colour of the reader (#rrggbb).</summary>
    public string ForegroundColor { get; set; } = DefaultForegroundColor;

    /// <summary>Font size of the reader; one of <see cref="ValidFontSizes"/>.</summary>
    public int FontSize { get; set; } = DefaultFontSize;

    /// <summary>Font style of the reader; a key of <see cref="FontFamilies"/>.</summary>
    public string FontFamily { get; set; } = DefaultFontFamily;

    public static IReadOnlyList<int> ValidFontSizes { get; } = [10, 11, 12, 14, 16, 18, 20, 22, 24, 28, 32, 36, 40, 48, 56, 72];

    /// <summary>
    /// Named font styles mapped to Avalonia font family fallback lists; the first installed font is used.
    /// Based on https://modernfontstacks.com.
    /// </summary>
    public static IReadOnlyDictionary<string, string> FontFamilies { get; } = new Dictionary<string, string>
    {
        { "System UI", "$Default" },
        { "Transitional", "Charter, Bitstream Charter, Sitka Text, Cambria, DejaVu Serif, serif" },
        { "Old Style", "Iowan Old Style, Palatino Linotype, URW Palladio L, P052, DejaVu Serif, serif" },
        { "Humanist", "Seravek, Gill Sans Nova, Ubuntu, Calibri, DejaVu Sans, sans-serif" },
        { "Geometric Humanist", "Avenir, Montserrat, Corbel, URW Gothic, DejaVu Sans, sans-serif" },
        { "Classical Humanist", "Optima, Candara, Noto Sans, DejaVu Sans, sans-serif" },
        { "Neo-Grotesque", "fonts:Inter#Inter, Roboto, Helvetica Neue, Arial Nova, Nimbus Sans, Arial, sans-serif" },
        { "Monospace Slab Serif", "Nimbus Mono PS, Courier New, DejaVu Sans Mono, monospace" },
        { "Monospace Code", "Cascadia Code, Source Code Pro, Menlo, Consolas, DejaVu Sans Mono, monospace" },
        { "Industrial", "Bahnschrift, DIN Alternate, Franklin Gothic Medium, Nimbus Sans Narrow, DejaVu Sans Condensed, sans-serif" },
        { "Rounded Sans", "Hiragino Maru Gothic ProN, Quicksand, Comfortaa, Manjari, Arial Rounded MT, Arial Rounded MT Bold, Calibri, sans-serif" },
        { "Slab Serif", "Rockwell, Rockwell Nova, Roboto Slab, DejaVu Serif, Sitka Small, serif" },
        { "Antique", "Superclarendon, Bookman Old Style, URW Bookman, URW Bookman L, Georgia Pro, Georgia, serif" },
        { "Didone", "Didot, Bodoni MT, Noto Serif Display, URW Palladio L, P052, Sylfaen, serif" },
        { "Handwritten", "Segoe Print, Bradley Hand, Chilanka, TSCu_Comic, casual, cursive" },
    };

    public static bool IsValidColour(string? colour) => colour is not null && HexColour().IsMatch(colour);

    public static bool IsValidFontSize(int size) => ValidFontSizes.Contains(size);

    public static bool IsValidFontFamily(string? fontFamily) => fontFamily is not null && FontFamilies.ContainsKey(fontFamily);

    public ReaderAppearance ToReaderAppearance() =>
        new(FontFamilies.GetValueOrDefault(FontFamily, FontFamilies[DefaultFontFamily]), FontSize, BackgroundColor, ForegroundColor);

    /// <summary>Replaces any invalid values (e.g. from a hand-edited settings file) with defaults.</summary>
    public void Normalise()
    {
        Ai ??= new AiSettings();
        Ai.Normalise();
        if (!IsValidColour(BackgroundColor)) BackgroundColor = DefaultBackgroundColor;
        if (!IsValidColour(ForegroundColor)) ForegroundColor = DefaultForegroundColor;
        if (!IsValidFontSize(FontSize)) FontSize = DefaultFontSize;
        if (!IsValidFontFamily(FontFamily)) FontFamily = DefaultFontFamily;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColour();
}

/// <summary>How the text of a document is displayed.</summary>
/// <param name="FontFamily">Font family fallback list, see <see cref="ApplicationSettings.FontFamilies"/>.</param>
public sealed record ReaderAppearance(string FontFamily, double FontSize, string Background, string Foreground)
{
    public static ReaderAppearance Default { get; } = new ApplicationSettings().ToReaderAppearance();
}
