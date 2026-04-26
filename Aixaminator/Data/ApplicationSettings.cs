using Aixaminator.Data.Ai;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Aixaminator.Data;

public class ApplicationSettings
{
    [Required]
    public AiSettings Ai { get; set; } = new();

    [Required]
    [CustomValidation(typeof(ApplicationSettings), nameof(ValidateBackgroundColor))]
    public string BackgroundColor { get; set; } = "#ffffff";

    [Required]
    [CustomValidation(typeof(ApplicationSettings), nameof(ValidateForegroundColor))]
    public string ForegroundColor { get; set; } = "#000000";

    [Required]
    [CustomValidation(typeof(ApplicationSettings), nameof(ValidateFontSize))]
    public int FontSize { get; set; } = 16;

    public static int[] ValidFontSizes = [10, 11, 12, 14, 16, 18, 20, 22, 24, 28, 32, 26, 40, 48, 56, 72];

    [Required]
    public string FontFamily { get; set; } = "Transitional";

    public static Dictionary<string, string> FontFamilies = new()
    {
        { "System UI", "system-ui, sans-serif" },
        { "Transitional", "Charter, 'Bitstream Charter', 'Sitka Text', Cambria, serif" },
        { "Old Style", "font-family: 'Iowan Old Style', 'Palatino Linotype', 'URW Palladio L', P052, serif" },
        { "Humanist", "Seravek, 'Gill Sans Nova', Ubuntu, Calibri, 'DejaVu Sans', source-sans-pro, sans-serif" },
        { "Geometric Humanist", "Avenir, Montserrat, Corbel, 'URW Gothic', source-sans-pro, sans-serif" },
        { "Classical Humanist", "Optima, Candara, 'Noto Sans', source-sans-pro, sans-serif" },
        { "Neo-Grotesque", "Inter, Roboto, 'Helvetica Neue', 'Arial Nova', 'Nimbus Sans', Arial, sans-serif" },
        { "Monospace Slab Serif", "'Nimbus Mono PS', 'Courier New', monospace" },
        { "Monospace Code", "ui-monospace, 'Cascadia Code', 'Source Code Pro', Menlo, Consolas, 'DejaVu Sans Mono', monospace" },
        { "Industrial", "Bahnschrift, 'DIN Alternate', 'Franklin Gothic Medium', 'Nimbus Sans Narrow', sans-serif-condensed, sans-serif" },
        { "Rounded Sans", "ui-rounded, 'Hiragino Maru Gothic ProN', Quicksand, Comfortaa, Manjari, 'Arial Rounded MT', 'Arial Rounded MT Bold', Calibri, source-sans-pro, sans-serif" },
        { "Slab Serif", "Rockwell, 'Rockwell Nova', 'Roboto Slab', 'DejaVu Serif', 'Sitka Small', serif" },
        { "Antique", "Superclarendon, 'Bookman Old Style', 'URW Bookman', 'URW Bookman L', 'Georgia Pro', Georgia, serif" },
        { "Didone", "Didot, 'Bodoni MT', 'Noto Serif Display', 'URW Palladio L', P052, Sylfaen, serif" },
        { "Handwritten", "'Segoe Print', 'Bradley Hand', Chilanka, TSCu_Comic, casual, cursive" },
    };

    private static ValidationResult? ValidateColour(string kind, string colour, ValidationContext context)
    {
        if (!Regex.IsMatch(colour, "^#[0-9a-fA-F]{6}$"))
        {
            return new ValidationResult($"Invalid {kind} colour.");
        }
        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateBackgroundColor(string colour, ValidationContext context)
    {
        return ValidateColour("background", colour, context);
    }

    public static ValidationResult? ValidateForegroundColor(string colour, ValidationContext context)
    {
        return ValidateColour("text", colour, context);
    }

    public static ValidationResult? ValidateFontFamily(string fontFamily, ValidationContext context)
    {
        if (!FontFamilies.ContainsKey(fontFamily))
        {
            return new ValidationResult("Invalid font family.");
        }
        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateFontSize(int fontSize, ValidationContext context)
    {
        if (!ValidFontSizes.Contains(fontSize))
        {
            return new ValidationResult("Invalid font size.");
        }
        return ValidationResult.Success;
    }

    [JsonIgnore]
    public string ReaderStyle => $"font-family:{FontFamilies[FontFamily]};font-size:{FontSize}pt;background-color:{BackgroundColor};color:{ForegroundColor}";
}
