using System.Text;

namespace Aixaminator.Services;

/// <summary>Stores the text of document parts; kept out of the database so queries stay light.</summary>
public interface IDocumentContentStore
{
    /// <returns>The part's text, or an empty string if it doesn't exist.</returns>
    Task<string> ReadPartAsync(Guid documentId, int partNumber, CancellationToken cancellationToken = default);

    Task WritePartAsync(Guid documentId, int partNumber, string text, CancellationToken cancellationToken = default);

    void DeleteDocument(Guid documentId);
}

/// <summary>Stores each part as <c>{documents}/{documentId}/{partNumber}.txt</c>.</summary>
public sealed class FileDocumentContentStore(IAppPaths paths) : IDocumentContentStore
{
    public async Task<string> ReadPartAsync(Guid documentId, int partNumber, CancellationToken cancellationToken = default)
    {
        var path = PartPath(documentId, partNumber);
        return File.Exists(path) ? await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken) : string.Empty;
    }

    public async Task WritePartAsync(Guid documentId, int partNumber, string text, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.GetDocumentDirectory(documentId));
        await File.WriteAllTextAsync(PartPath(documentId, partNumber), text, Encoding.UTF8, cancellationToken);
    }

    public void DeleteDocument(Guid documentId)
    {
        var directory = paths.GetDocumentDirectory(documentId);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private string PartPath(Guid documentId, int partNumber) =>
        Path.Combine(paths.GetDocumentDirectory(documentId), $"{partNumber}.txt");
}
