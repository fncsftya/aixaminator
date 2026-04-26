using Aixaminator.Data;
using Aixaminator.Utils;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Threading;

namespace Aixaminator.Services;

public class SettingsService : ISettingsService
{
    private readonly SemaphoreSlim _saveSemaphore = new SemaphoreSlim(1, 1);
    
    public ApplicationSettings Settings { get; set; }
    public bool AttemptMigration { get; set; } = true;

    public async Task Init()
    {
        if (Settings is null)
        {
            await LoadSettings();
        }
    }

    private string SettingsPath => Constants.GetInternalFilepath("settings.json");

    public async Task LoadSettings()
    {
        Debug.Assert(Settings is null);
        if (File.Exists(SettingsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(SettingsPath);
                if (json is null)
                {
                    Settings = new ApplicationSettings();
                    await SaveSettings();
                }
                else
                {
                    var loadedSettings = JsonConvert.DeserializeObject<ApplicationSettings>(json);
                    if (loadedSettings is null)
                    {
                        Settings = new ApplicationSettings();
                        await SaveSettings();
                    }
                    else
                    {
                        Settings = loadedSettings;
                    }
                }
            }
            catch (Exception ex)
            {
                // Create default settings if loading failed
                Settings = new ApplicationSettings();
                Console.WriteLine($"Failed to load settings: {ex.Message}");
                await SaveSettings();
            }
        }
        else
        {
            Settings = new ApplicationSettings();
            await SaveSettings();
        }
    }

    public async Task SaveSettings()
    {
        Debug.Assert(Settings is not null);
        var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
        
        // Use semaphore to ensure only one save operation happens at a time
        await _saveSemaphore.WaitAsync();
        try
        {
            await File.WriteAllTextAsync(SettingsPath, json);
        }
        finally
        {
            _saveSemaphore.Release();
        }
    }

    public void SaveSettingsSync()
    {
        Debug.Assert(Settings is not null);
        var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
        
        // Use semaphore to ensure only one save operation happens at a time
        _saveSemaphore.Wait();
        try
        {
            File.WriteAllText(SettingsPath, json);
        }
        finally
        {
            _saveSemaphore.Release();
        }
    }
}
