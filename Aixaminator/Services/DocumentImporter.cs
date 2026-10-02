using Aixaminator.Features;
using Importers;

namespace Aixaminator.Services;

/// <summary>Text read from a file or web page, ready to become a document.</summary>
public sealed record ImportedText(string SuggestedName, string Text);

/// <summary>An import failed for a reason that can be shown to the user.</summary>
public sealed class DocumentImportException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>Reads the text of documents from files and URLs.</summary>
public interface IDocumentImporter
{
    /// <summary>Reads a PDF, EPUB or plain text file.</summary>
    /// <exception cref="DocumentImportException">The file can't be imported.</exception>
    Task<ImportedText> ReadFileAsync(string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Reads a web page. Only Wikipedia articles are supported.</summary>
    /// <exception cref="DocumentImportException">The page can't be imported.</exception>
    Task<ImportedText> ReadUrlAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>Fetches the text of Wikipedia articles.</summary>
public interface IWikipediaClient
{
    Task<string> GetTextAsync(string url, CancellationToken cancellationToken);
}

public sealed class WikipediaClient : IWikipediaClient
{
    public Task<string> GetTextAsync(string url, CancellationToken cancellationToken) =>
        new WikiReader().ExtractTextFromUrlAsync(url).WaitAsync(cancellationToken);
}

public sealed class DocumentImporter(IWikipediaClient wikipedia) : IDocumentImporter
{
    public const long MaxFileSize = 300L * 1024 * 1024;

    public async Task<ImportedText> ReadFileAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await CopyWithLimitAsync(content, buffer, cancellationToken);
        buffer.Position = 0;

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        string text;
        if (extension == ".pdf" || new PDFReader().IsPdfFile(buffer))
        {
            text = Parse(() => new PDFReader().ExtractText(buffer), "The chosen file is not a valid PDF file.");
        }
        else if (extension == ".epub")
        {
            text = Parse(() => new EpubReader().ExtractText(buffer), "The chosen file is not a valid EPUB file.");
        }
        else
        {
            if (await BinaryContentDetector.IsBinaryAsync(buffer, cancellationToken))
            {
                throw new DocumentImportException("The chosen file appears to contain binary content and cannot be processed as text.");
            }

            using var reader = new StreamReader(buffer, leaveOpen: true);
            text = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DocumentImportException("No text could be found in the chosen file.");
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        return new ImportedText(string.IsNullOrWhiteSpace(name) ? "Untitled document" : name, text);
    }

    public async Task<ImportedText> ReadUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!IsValidUrl(url))
        {
            throw new DocumentImportException("Please enter a valid URL.");
        }

        if (!IsWikipediaUrl(url))
        {
            throw new DocumentImportException("Only Wikipedia URLs are currently supported.");
        }

        string text;
        try
        {
            text = await wikipedia.GetTextAsync(url, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DocumentImportException($"The page could not be loaded: {ex.Message}", ex);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DocumentImportException("No text could be found on the page.");
        }

        var title = Uri.UnescapeDataString(new Uri(url).Segments.LastOrDefault()?.Trim('/') ?? string.Empty).Replace('_', ' ');
        return new ImportedText(string.IsNullOrWhiteSpace(title) ? "Wikipedia Page" : title, text);
    }

    public static bool IsValidUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static bool IsWikipediaUrl(string? url) =>
        IsValidUrl(url)
        && new Uri(url!).Host is var host
        && (host.Equals("wikipedia.org", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".wikipedia.org", StringComparison.OrdinalIgnoreCase));

    private static string Parse(Func<string> parse, string failureMessage)
    {
        try
        {
            return parse();
        }
        catch (Exception ex)
        {
            throw new DocumentImportException(failureMessage, ex);
        }
    }

    private static async Task CopyWithLimitAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxFileSize)
            {
                throw new DocumentImportException("The chosen file is too large to import (the limit is 300 MB).");
            }
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }
}
