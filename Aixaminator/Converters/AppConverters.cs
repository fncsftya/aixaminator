using Aixaminator.Models;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Aixaminator.Converters;

/// <summary>Converters used by the views to turn plain view model values into Avalonia types.</summary>
public static class AppConverters
{
    /// <summary>Converts a #rrggbb colour into a brush; null if the colour is missing or invalid.</summary>
    public static readonly IValueConverter HexToBrush = new FuncValueConverter<string?, IBrush?>(ToBrush);

    /// <summary>Converts a font fallback list (see <see cref="ApplicationSettings.FontFamilies"/>) into a font family.</summary>
    public static readonly IValueConverter ToFontFamily = new FuncValueConverter<string?, FontFamily>(
        names => string.IsNullOrWhiteSpace(names) ? FontFamily.Default : FontFamily.Parse(names));

    public static IBrush? ToBrush(string? hex) =>
        ApplicationSettings.IsValidColour(hex) && Color.TryParse(hex, out var colour) ? new ImmutableSolidColorBrush(colour) : null;
}
