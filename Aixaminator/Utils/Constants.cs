namespace Aixaminator.Utils;

public class Constants
{
#if DEBUG
    public static string StorageDir = "AixaminatorDebug";
#else
    public static string StorageDir = "Aixaminator";
#endif

    public static string GetInternalFilepath(params string[] paths)
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            StorageDir,
            Path.Combine(paths)
        );
    }

    public static string GetDocumentBasePath(string name) => GetInternalFilepath("Documents", name);

    public static string DbName = GetInternalFilepath("data.db");
    public static string DbConnection = $"Data Source={DbName}";
}
