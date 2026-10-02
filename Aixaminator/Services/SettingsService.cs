using System.Text.Json;
using Aixaminator.Models;

namespace Aixaminator.Services;

/// <summary>Stores <see cref="ApplicationSettings"/> as JSON in <see cref="IAppPaths.SettingsPath"/>.</summary>
public sealed class SettingsService(IAppPaths paths) : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly SemaphoreSlim _lock = new(1, 1);

    public ApplicationSettings Settings { get; private set; } = new();

    public event EventHandler? SettingsSaved;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        ApplicationSettings? loaded = null;
        if (File.Exists(paths.SettingsPath))
        {
            try
            {
                await using var stream = File.OpenRead(paths.SettingsPath);
                loaded = await JsonSerializer.DeserializeAsync<ApplicationSettings>(stream, JsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                // Keep the broken file so the user can recover anything in it (e.g. API keys).
                File.Move(paths.SettingsPath, paths.SettingsPath + ".invalid", overwrite: true);
            }
        }

        Settings = loaded ?? new ApplicationSettings();
        Settings.Normalise();
        await SaveAsync(cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(paths.SettingsPath)!);

            // Write to a temporary file first so a crash mid-write can't corrupt the settings.
            var temporary = paths.SettingsPath + ".tmp";
            await using (var stream = File.Create(temporary))
            {
                await JsonSerializer.SerializeAsync(stream, Settings, JsonOptions, cancellationToken);
            }
            File.Move(temporary, paths.SettingsPath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }

        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }
}
