using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using Aixaminator.Models;
using Aixaminator.Models.Ai;
using Aixaminator.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aixaminator.ViewModels.Home;

/// <summary>An AI provider choice in a drop-down. <see cref="None"/> represents "no provider".</summary>
public sealed record ProviderOption(string Id, string DisplayName)
{
    public static ProviderOption None { get; } = new(string.Empty, "Select Provider");

    public static ProviderOption For(string id) => new(id, AiSettings.DisplayName(id));
}

/// <summary>The "Settings" tab. Valid changes are saved automatically.</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IAiConnection _ai;
    private readonly bool _initialised;
    private Task _pendingSave = Task.CompletedTask;

    public SettingsViewModel(ISettingsService settingsService, IAiConnection ai)
    {
        _settingsService = settingsService;
        _ai = ai;

        BackgroundColor = Settings.BackgroundColor;
        ForegroundColor = Settings.ForegroundColor;
        FontFamily = Settings.FontFamily;
        FontSize = Settings.FontSize;
        Settings.Ai.Normalise();
        RebuildProviders();
        _initialised = true;
    }

    private ApplicationSettings Settings => _settingsService.Settings;

    /// <summary>"Saving...", "Saved!" or an error, once something has been changed.</summary>
    [ObservableProperty]
    public partial string? Status { get; private set; }

    // ----- Display -----

    public IReadOnlyList<string> FontFamilies { get; } = [.. ApplicationSettings.FontFamilies.Keys];

    public IReadOnlyList<int> FontSizes { get; } = ApplicationSettings.ValidFontSizes;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(SettingsViewModel), nameof(ValidateBackgroundColor))]
    [NotifyPropertyChangedFor(nameof(Preview))]
    public partial string BackgroundColor { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(SettingsViewModel), nameof(ValidateForegroundColor))]
    [NotifyPropertyChangedFor(nameof(Preview))]
    public partial string ForegroundColor { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    public partial string FontFamily { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    public partial int FontSize { get; set; }

    /// <summary>How the reader will look with the current values.</summary>
    public ReaderAppearance Preview => Settings.ToReaderAppearance();

    partial void OnBackgroundColorChanged(string value) => Apply(ApplicationSettings.IsValidColour(value), s => s.BackgroundColor = value);

    partial void OnForegroundColorChanged(string value) => Apply(ApplicationSettings.IsValidColour(value), s => s.ForegroundColor = value);

    partial void OnFontFamilyChanged(string value) => Apply(ApplicationSettings.IsValidFontFamily(value), s => s.FontFamily = value);

    partial void OnFontSizeChanged(int value) => Apply(ApplicationSettings.IsValidFontSize(value), s => s.FontSize = value);

    private void Apply(bool isValid, Action<ApplicationSettings> apply)
    {
        if (!_initialised || !isValid)
        {
            return;
        }

        apply(Settings);
        QueueSave();
    }

    public static ValidationResult? ValidateBackgroundColor(string colour, ValidationContext context) =>
        ApplicationSettings.IsValidColour(colour) ? ValidationResult.Success : new ValidationResult("Invalid background colour.");

    public static ValidationResult? ValidateForegroundColor(string colour, ValidationContext context) =>
        ApplicationSettings.IsValidColour(colour) ? ValidationResult.Success : new ValidationResult("Invalid text colour.");

    // ----- AI providers -----

    public ObservableCollection<ProviderViewModel> Providers { get; } = [];

    public bool HasProviders => Providers.Count > 0;

    public ObservableCollection<ActionMappingViewModel> Actions { get; } = [];

    public IReadOnlyList<string> UnavailableActions { get; } = AiAction.Unavailable;

    public IReadOnlyList<ProviderOption> AvailableProviders { get; } = [.. AiSettings.AvailableProviders.Select(ProviderOption.For)];

    [ObservableProperty]
    public partial bool IsAddingProvider { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddProviderCommand))]
    public partial ProviderOption? NewProvider { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddProviderCommand))]
    public partial string NewProviderApiKey { get; set; } = string.Empty;

    [RelayCommand]
    private void StartAddingProvider() => IsAddingProvider = true;

    [RelayCommand]
    private void CancelAddingProvider()
    {
        IsAddingProvider = false;
        NewProvider = null;
        NewProviderApiKey = string.Empty;
    }

    private bool CanAddProvider() => NewProvider is { Id.Length: > 0 } && !string.IsNullOrWhiteSpace(NewProviderApiKey);

    [RelayCommand(CanExecute = nameof(CanAddProvider))]
    private void AddProvider()
    {
        Settings.Ai.AddProvider(new AiProvider { Name = NewProvider!.Id, ApiKey = NewProviderApiKey.Trim() });
        CancelAddingProvider();
        RebuildProviders();
        QueueSave();
    }

    internal void RemoveProvider(ProviderViewModel provider)
    {
        Settings.Ai.RemoveProvider(provider.Name);
        RebuildProviders();
        QueueSave();
    }

    internal Task<bool> TestConnectionAsync(ProviderViewModel provider) =>
        _ai.TestConnectionAsync(provider.Name, Settings.Ai.AiProviders.First(p => p.Name == provider.Name).ApiKey);

    private void RebuildProviders()
    {
        Providers.Clear();
        foreach (var provider in Settings.Ai.AiProviders)
        {
            Providers.Add(new ProviderViewModel(this, provider));
        }
        OnPropertyChanged(nameof(HasProviders));

        IReadOnlyList<ProviderOption> options = [ProviderOption.None, .. Settings.Ai.AiProviders.Select(p => ProviderOption.For(p.Name))];
        Actions.Clear();
        foreach (var action in AiAction.All)
        {
            Actions.Add(new ActionMappingViewModel(this, Settings.Ai, action, options));
        }
    }

    // ----- Saving -----

    /// <summary>Completes once all changes made so far have been saved.</summary>
    public Task WhenSavedAsync() => _pendingSave;

    internal void QueueSave() => _pendingSave = SaveAfterAsync(_pendingSave);

    private async Task SaveAfterAsync(Task previous)
    {
        await previous;
        Status = "Saving...";
        try
        {
            await _settingsService.SaveAsync();
            Status = "Saved!";
        }
        catch (Exception)
        {
            Status = "Failed to save settings.";
        }
    }
}

/// <summary>A configured AI provider in the settings list.</summary>
public sealed partial class ProviderViewModel(SettingsViewModel owner, AiProvider provider) : ViewModelBase
{
    public string Name => provider.Name;

    public string DisplayName => AiSettings.DisplayName(provider.Name);

    public string MaskedApiKey => Mask(provider.ApiKey);

    [ObservableProperty]
    public partial string ConnectionStatus { get; private set; } = "Check Connection";

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        ConnectionStatus = "Checking...";
        ConnectionStatus = await owner.TestConnectionAsync(this) ? "Connected!" : "Failed";
    }

    [RelayCommand]
    private void Remove() => owner.RemoveProvider(this);

    /// <summary>Shows only the start and end of a key, e.g. <c>sk-1****7890</c>.</summary>
    public static string Mask(string apiKey) =>
        apiKey.Length <= 8 ? "****" : $"{apiKey[..4]}****{apiKey[^4..]}";
}

/// <summary>Which provider and model is used for one <see cref="AiAction"/>.</summary>
public sealed partial class ActionMappingViewModel : ViewModelBase
{
    private readonly SettingsViewModel _owner;
    private readonly AiSettings _settings;
    private readonly bool _initialised;

    public ActionMappingViewModel(SettingsViewModel owner, AiSettings settings, string action, IReadOnlyList<ProviderOption> providerOptions)
    {
        _owner = owner;
        _settings = settings;
        Action = action;
        ProviderOptions = providerOptions;

        var providerId = settings.ActionProviderMap.GetValueOrDefault(action) ?? string.Empty;
        SelectedProvider = providerOptions.FirstOrDefault(p => p.Id == providerId) ?? ProviderOption.None;
        ModelOptions = AiSettings.ModelsFor(SelectedProvider.Id, action);
        SelectedModel = ModelOptions.FirstOrDefault(m => m.Id == settings.ActionModelMap.GetValueOrDefault(action));
        _initialised = true;
    }

    public string Action { get; }

    public IReadOnlyList<ProviderOption> ProviderOptions { get; }

    [ObservableProperty]
    public partial ProviderOption SelectedProvider { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<AiModel> ModelOptions { get; private set; }

    [ObservableProperty]
    public partial AiModel? SelectedModel { get; set; }

    partial void OnSelectedProviderChanged(ProviderOption value)
    {
        if (!_initialised)
        {
            return;
        }

        // A drop-down may briefly report no selection while its items change; keep the mapping.
        value ??= ProviderOption.None;
        _settings.SetActionProvider(Action, value.Id);
        ModelOptions = AiSettings.ModelsFor(value.Id, Action);
        SelectedModel = ModelOptions.FirstOrDefault(m => m.Id == _settings.ActionModelMap[Action]);
        _owner.QueueSave();
    }

    partial void OnSelectedModelChanged(AiModel? value)
    {
        if (!_initialised || value is null || _settings.ActionModelMap.GetValueOrDefault(Action) == value.Id)
        {
            return;
        }

        _settings.SetActionModel(Action, value.Id);
        _owner.QueueSave();
    }
}
