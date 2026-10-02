using Aixaminator.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aixaminator.ViewModels;

/// <summary>The main window: hosts the current page and any open dialog.</summary>
public sealed partial class MainWindowViewModel(
    NavigationService navigation,
    DialogService dialogs,
    IAppInitializer initializer) : ViewModelBase
{
    public NavigationService Navigation { get; } = navigation;

    public DialogService Dialogs { get; } = dialogs;

    [ObservableProperty]
    public partial bool IsStarting { get; private set; } = true;

    [ObservableProperty]
    public partial string? StartupError { get; private set; }

    /// <summary>Prepares storage, then shows the home page.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            await initializer.InitializeAsync();
            await Navigation.GoHomeAsync();
        }
        catch (Exception ex)
        {
            StartupError = $"Aixaminator could not start: {ex.Message}";
        }
        finally
        {
            IsStarting = false;
        }
    }
}
