using Aixaminator.Models;

namespace Aixaminator.Tests.Models;

public class ApplicationSettingsTests
{
    [Theory]
    [InlineData("#ffffff", true)]
    [InlineData("#A1b2C3", true)]
    [InlineData("ffffff", false)]
    [InlineData("#fff", false)]
    [InlineData("#gggggg", false)]
    [InlineData(null, false)]
    public void Colours_must_be_six_digit_hex(string? colour, bool valid)
    {
        Assert.Equal(valid, ApplicationSettings.IsValidColour(colour));
    }

    [Fact]
    public void Font_sizes_are_listed_in_ascending_order()
    {
        Assert.Equal(ApplicationSettings.ValidFontSizes.Order(), ApplicationSettings.ValidFontSizes);
    }

    [Fact]
    public void Defaults_are_valid()
    {
        var settings = new ApplicationSettings();

        Assert.True(ApplicationSettings.IsValidColour(settings.BackgroundColor));
        Assert.True(ApplicationSettings.IsValidColour(settings.ForegroundColor));
        Assert.True(ApplicationSettings.IsValidFontSize(settings.FontSize));
        Assert.True(ApplicationSettings.IsValidFontFamily(settings.FontFamily));
    }

    [Fact]
    public void Normalise_replaces_invalid_values_with_defaults()
    {
        var settings = new ApplicationSettings
        {
            BackgroundColor = "blue",
            ForegroundColor = "#00ff00",
            FontSize = 13,
            FontFamily = "Comic Sans",
            Ai = null!,
        };

        settings.Normalise();

        Assert.Equal(ApplicationSettings.DefaultBackgroundColor, settings.BackgroundColor);
        Assert.Equal("#00ff00", settings.ForegroundColor);
        Assert.Equal(ApplicationSettings.DefaultFontSize, settings.FontSize);
        Assert.Equal(ApplicationSettings.DefaultFontFamily, settings.FontFamily);
        Assert.NotNull(settings.Ai);
    }

    [Fact]
    public void Reader_appearance_uses_the_font_stack_for_the_selected_family()
    {
        var settings = new ApplicationSettings { FontFamily = "Monospace Code", FontSize = 20, BackgroundColor = "#111111", ForegroundColor = "#eeeeee" };

        var appearance = settings.ToReaderAppearance();

        Assert.Equal(ApplicationSettings.FontFamilies["Monospace Code"], appearance.FontFamily);
        Assert.Equal(20, appearance.FontSize);
        Assert.Equal("#111111", appearance.Background);
        Assert.Equal("#eeeeee", appearance.Foreground);
    }
}
