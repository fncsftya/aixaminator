namespace Aixaminator.Services;

/// <summary>Locations of the files the application stores.</summary>
public interface IAppPaths
{
    /// <summary>Root directory for all application data.</summary>
    string DataDirectory { get; }

    string DatabasePath { get; }

    string ConnectionString { get; }

    string SettingsPath { get; }

    /// <summary>Directory containing one sub-directory per document with the text of its parts.</summary>
    string DocumentsDirectory { get; }

    string GetDocumentDirectory(Guid documentId);
}

public sealed class AppPaths : IAppPaths
{
    /// <summary>Environment variable that, when set, overrides the data directory.</summary>
    public const string DataDirectoryVariable = "AIXAMINATOR_DATA_DIR";

#if DEBUG
    private const string FolderName = "AixaminatorDebug";
#else
    private const string FolderName = "Aixaminator";
#endif

    public AppPaths(string dataDirectory)
    {
        DataDirectory = Path.GetFullPath(dataDirectory);
    }

    /// <summary>
    /// Uses <see cref="DataDirectoryVariable"/> if set, otherwise the per-user local application data folder
    /// (<c>~/.local/share</c> on Linux, <c>~/Library/Application Support</c> on macOS, <c>%LOCALAPPDATA%</c> on Windows).
    /// </summary>
    public static AppPaths CreateDefault()
    {
        var overridden = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return new AppPaths(overridden);
        }

        var root = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);
        return new AppPaths(Path.Combine(root, FolderName));
    }

    public string DataDirectory { get; }

    public string DatabasePath => Path.Combine(DataDirectory, "aixaminator.db");

    public string ConnectionString => $"Data Source={DatabasePath}";

    public string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public string DocumentsDirectory => Path.Combine(DataDirectory, "documents");

    public string GetDocumentDirectory(Guid documentId) => Path.Combine(DocumentsDirectory, documentId.ToString("N"));

    /// <summary>Creates the data directories; SQLite won't create missing parent directories itself.</summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(DocumentsDirectory);
    }
}
