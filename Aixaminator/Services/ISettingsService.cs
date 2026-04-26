using Aixaminator.Data;

namespace Aixaminator.Services;

public interface ISettingsService
{
    ApplicationSettings Settings { get; set; }
    bool AttemptMigration { get; set; }
    Task Init();
    Task LoadSettings();
    Task SaveSettings();
    void SaveSettingsSync();
}
