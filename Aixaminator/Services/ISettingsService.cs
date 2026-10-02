using Aixaminator.Models;

namespace Aixaminator.Services;

public interface ISettingsService
{
    /// <summary>The current settings. Mutate them, then call <see cref="SaveAsync"/> to persist the changes.</summary>
    ApplicationSettings Settings { get; }

    /// <summary>Raised after the settings have been saved.</summary>
    event EventHandler? SettingsSaved;

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}
