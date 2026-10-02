using Aixaminator.Models;
using Aixaminator.Models.Ai;
using Aixaminator.Services;
using Aixaminator.Tests.Infrastructure;

namespace Aixaminator.Tests.Services;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly TempDirectory _directory = new();
    private readonly AppPaths _paths;

    public SettingsServiceTests()
    {
        _paths = new AppPaths(_directory.Path);
    }

    public void Dispose() => _directory.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Settings_have_defaults_before_loading()
    {
        var service = new SettingsService(_paths);

        Assert.Equal(ApplicationSettings.DefaultFontSize, service.Settings.FontSize);
    }

    [Fact]
    public async Task Loading_without_a_file_creates_one_with_defaults()
    {
        var service = new SettingsService(_paths);

        await service.LoadAsync(Token);

        Assert.True(File.Exists(_paths.SettingsPath));
        Assert.Equal(ApplicationSettings.DefaultFontFamily, service.Settings.FontFamily);
    }

    [Fact]
    public async Task Saved_settings_are_loaded_again()
    {
        var first = new SettingsService(_paths);
        await first.LoadAsync(Token);
        first.Settings.FontSize = 24;
        first.Settings.BackgroundColor = "#123456";
        first.Settings.Ai.AddProvider(new AiProvider { Name = "google", ApiKey = "secret" });
        await first.SaveAsync(Token);

        var second = new SettingsService(_paths);
        await second.LoadAsync(Token);

        Assert.Equal(24, second.Settings.FontSize);
        Assert.Equal("#123456", second.Settings.BackgroundColor);
        var provider = Assert.Single(second.Settings.Ai.AiProviders);
        Assert.Equal("secret", provider.ApiKey);
        Assert.Equal("google", second.Settings.Ai.ActionProviderMap[AiAction.QuizGeneration]);
    }

    [Fact]
    public async Task Invalid_values_are_normalised_on_load()
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.SettingsPath, """{ "FontSize": 13, "ForegroundColor": "#00ff00" }""", Token);
        var service = new SettingsService(_paths);

        await service.LoadAsync(Token);

        Assert.Equal(ApplicationSettings.DefaultFontSize, service.Settings.FontSize);
        Assert.Equal("#00ff00", service.Settings.ForegroundColor);
    }

    [Fact]
    public async Task A_corrupt_file_is_set_aside_and_defaults_are_used()
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.SettingsPath, "{ not json", Token);
        var service = new SettingsService(_paths);

        await service.LoadAsync(Token);

        Assert.Equal(ApplicationSettings.DefaultFontSize, service.Settings.FontSize);
        Assert.Equal("{ not json", await File.ReadAllTextAsync(_paths.SettingsPath + ".invalid", Token));
    }

    [Fact]
    public async Task Saving_raises_the_saved_event()
    {
        var service = new SettingsService(_paths);
        var raised = 0;
        service.SettingsSaved += (_, _) => raised++;

        await service.SaveAsync(Token);

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task Concurrent_saves_do_not_corrupt_the_file()
    {
        var service = new SettingsService(_paths);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => service.SaveAsync(Token)));

        var reloaded = new SettingsService(_paths);
        await reloaded.LoadAsync(Token);
        Assert.False(File.Exists(_paths.SettingsPath + ".invalid"));
    }
}
