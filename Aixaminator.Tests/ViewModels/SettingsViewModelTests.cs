using Aixaminator.Models;
using Aixaminator.Models.Ai;
using Aixaminator.Services;
using Aixaminator.Tests.Infrastructure;
using Aixaminator.ViewModels.Home;

namespace Aixaminator.Tests.ViewModels;

public sealed class SettingsViewModelTests : IAsyncLifetime
{
    private readonly TempDirectory _directory = new();
    private readonly FakeAiConnection _ai = new();
    private SettingsService _settings = null!;

    public async ValueTask InitializeAsync()
    {
        _settings = new SettingsService(new AppPaths(_directory.Path));
        await _settings.LoadAsync();
    }

    public ValueTask DisposeAsync()
    {
        _directory.Dispose();
        return ValueTask.CompletedTask;
    }

    private SettingsViewModel CreateViewModel() => new(_settings, _ai);

    private async Task<ApplicationSettings> ReloadAsync()
    {
        var reloaded = new SettingsService(new AppPaths(_directory.Path));
        await reloaded.LoadAsync();
        return reloaded.Settings;
    }

    [Fact]
    public void Shows_the_current_display_settings()
    {
        _settings.Settings.FontSize = 20;
        _settings.Settings.FontFamily = "Humanist";

        var vm = CreateViewModel();

        Assert.Equal(20, vm.FontSize);
        Assert.Equal("Humanist", vm.FontFamily);
        Assert.Equal(ApplicationSettings.DefaultBackgroundColor, vm.BackgroundColor);
        Assert.Equal(ApplicationSettings.ValidFontSizes, vm.FontSizes);
        Assert.Contains("Transitional", vm.FontFamilies);
        Assert.Null(vm.Status);
    }

    [Fact]
    public async Task Changing_a_display_setting_saves_it()
    {
        var vm = CreateViewModel();

        vm.FontSize = 28;
        vm.FontFamily = "Monospace Code";
        await vm.WhenSavedAsync();

        Assert.Equal("Saved!", vm.Status);
        var saved = await ReloadAsync();
        Assert.Equal(28, saved.FontSize);
        Assert.Equal("Monospace Code", saved.FontFamily);
    }

    [Fact]
    public async Task Invalid_colours_are_reported_and_not_saved()
    {
        var vm = CreateViewModel();

        vm.BackgroundColor = "#12345";
        await vm.WhenSavedAsync();

        Assert.Equal("Invalid background colour.", vm.GetErrors(nameof(vm.BackgroundColor)).Single().ErrorMessage);
        Assert.Equal(ApplicationSettings.DefaultBackgroundColor, _settings.Settings.BackgroundColor);

        vm.BackgroundColor = "#123456";
        vm.ForegroundColor = "nope";
        await vm.WhenSavedAsync();

        Assert.False(vm.GetErrors(nameof(vm.BackgroundColor)).Any());
        Assert.Equal("Invalid text colour.", vm.GetErrors(nameof(vm.ForegroundColor)).Single().ErrorMessage);
        Assert.Equal("#123456", (await ReloadAsync()).BackgroundColor);
    }

    [Fact]
    public void Preview_reflects_the_display_settings()
    {
        var vm = CreateViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.FontSize = 32;
        vm.ForegroundColor = "#ff0000";

        Assert.Equal(32, vm.Preview.FontSize);
        Assert.Equal("#ff0000", vm.Preview.Foreground);
        Assert.Contains(nameof(vm.Preview), changed);
    }

    [Fact]
    public void Adding_a_provider_requires_a_provider_and_key()
    {
        var vm = CreateViewModel();

        vm.StartAddingProviderCommand.Execute(null);

        Assert.True(vm.IsAddingProvider);
        Assert.False(vm.AddProviderCommand.CanExecute(null));
        vm.NewProvider = vm.AvailableProviders.Single(p => p.Id == "openai");
        Assert.False(vm.AddProviderCommand.CanExecute(null));
        vm.NewProviderApiKey = "sk-1234567890";
        Assert.True(vm.AddProviderCommand.CanExecute(null));
    }

    [Fact]
    public async Task Adding_a_provider_lists_it_and_maps_the_actions()
    {
        var vm = CreateViewModel();
        vm.StartAddingProviderCommand.Execute(null);
        vm.NewProvider = vm.AvailableProviders.Single(p => p.Id == "openai");
        vm.NewProviderApiKey = "sk-1234567890";

        vm.AddProviderCommand.Execute(null);
        await vm.WhenSavedAsync();

        Assert.False(vm.IsAddingProvider);
        var provider = Assert.Single(vm.Providers);
        Assert.Equal("OpenAI", provider.DisplayName);
        Assert.Equal("sk-1****7890", provider.MaskedApiKey);
        Assert.All(vm.Actions, action =>
        {
            Assert.Equal("openai", action.SelectedProvider.Id);
            Assert.Equal("gpt-4o-mini", action.SelectedModel?.Id);
        });
        Assert.Equal("openai", (await ReloadAsync()).Ai.ActionProviderMap[AiAction.QuizGeneration]);
        Assert.Null(vm.NewProvider);
        Assert.Equal(string.Empty, vm.NewProviderApiKey);
    }

    [Fact]
    public void Cancelling_adding_a_provider_discards_the_input()
    {
        var vm = CreateViewModel();
        vm.StartAddingProviderCommand.Execute(null);
        vm.NewProviderApiKey = "abc";

        vm.CancelAddingProviderCommand.Execute(null);

        Assert.False(vm.IsAddingProvider);
        Assert.Equal(string.Empty, vm.NewProviderApiKey);
        Assert.Empty(vm.Providers);
    }

    [Theory]
    [InlineData("short", "****")]
    [InlineData("12345678", "****")]
    [InlineData("abcdefghijkl", "abcd****ijkl")]
    public void Api_keys_are_masked(string key, string expected)
    {
        Assert.Equal(expected, ProviderViewModel.Mask(key));
    }

    [Fact]
    public async Task Removing_a_provider_clears_its_actions()
    {
        _settings.Settings.Ai.AddProvider(new AiProvider { Name = "google", ApiKey = "key" });
        var vm = CreateViewModel();

        vm.Providers[0].RemoveCommand.Execute(null);
        await vm.WhenSavedAsync();

        Assert.Empty(vm.Providers);
        Assert.All(vm.Actions, a => Assert.Equal(string.Empty, a.SelectedProvider.Id));
        Assert.Empty((await ReloadAsync()).Ai.AiProviders);
    }

    [Fact]
    public async Task Connections_can_be_checked()
    {
        _settings.Settings.Ai.AddProvider(new AiProvider { Name = "google", ApiKey = "good" });
        _settings.Settings.Ai.AddProvider(new AiProvider { Name = "openai", ApiKey = "bad" });
        _ai.Test = (_, key) => Task.FromResult(key == "good");
        var vm = CreateViewModel();

        Assert.Equal("Check Connection", vm.Providers[0].ConnectionStatus);
        await vm.Providers[0].TestConnectionCommand.ExecuteAsync(null);
        await vm.Providers[1].TestConnectionCommand.ExecuteAsync(null);

        Assert.Equal("Connected!", vm.Providers[0].ConnectionStatus);
        Assert.Equal("Failed", vm.Providers[1].ConnectionStatus);
    }

    [Fact]
    public async Task Changing_the_provider_of_an_action_selects_its_default_model()
    {
        _settings.Settings.Ai.AddProvider(new AiProvider { Name = "google", ApiKey = "g" });
        _settings.Settings.Ai.AddProvider(new AiProvider { Name = "openai", ApiKey = "o" });
        var vm = CreateViewModel();
        var quiz = vm.Actions.Single(a => a.Action == AiAction.QuizGeneration);

        Assert.Equal(["", "google", "openai"], quiz.ProviderOptions.Select(p => p.Id));
        quiz.SelectedProvider = quiz.ProviderOptions.Single(p => p.Id == "openai");
        await vm.WhenSavedAsync();

        Assert.Equal(["gpt-4o-mini", "gpt-4o"], quiz.ModelOptions.Select(m => m.Id));
        Assert.Equal("gpt-4o-mini", quiz.SelectedModel?.Id);

        quiz.SelectedModel = quiz.ModelOptions.Single(m => m.Id == "gpt-4o");
        await vm.WhenSavedAsync();

        var saved = await ReloadAsync();
        Assert.Equal("openai", saved.Ai.ActionProviderMap[AiAction.QuizGeneration]);
        Assert.Equal("gpt-4o", saved.Ai.ActionModelMap[AiAction.QuizGeneration]);
    }

    [Fact]
    public void Unavailable_actions_are_listed_as_placeholders()
    {
        var vm = CreateViewModel();

        Assert.Equal(AiAction.Unavailable, vm.UnavailableActions);
    }
}
