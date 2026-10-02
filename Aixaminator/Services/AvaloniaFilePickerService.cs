using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Aixaminator.Services;

/// <summary>Opens the platform's native file picker through Avalonia's storage provider.</summary>
/// <param name="topLevel">Returns the window to show the picker for.</param>
public sealed class AvaloniaFilePickerService(Func<TopLevel?> topLevel) : IFilePickerService
{
    private static readonly FilePickerFileType Documents = new("Documents")
    {
        Patterns = ["*.pdf", "*.epub", "*.txt", "*.md"],
        MimeTypes = ["application/pdf", "application/epub+zip", "text/plain", "text/markdown"],
        AppleUniformTypeIdentifiers = ["com.adobe.pdf", "org.idpf.epub-container", "public.plain-text"],
    };

    public async Task<IPickedFile?> PickDocumentAsync()
    {
        var storage = topLevel()?.StorageProvider;
        if (storage is not { CanOpen: true })
        {
            return null;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a document",
            AllowMultiple = false,
            FileTypeFilter = [Documents, FilePickerFileTypes.All],
        });

        return files.Count == 0 ? null : new StorageFile(files[0]);
    }

    private sealed class StorageFile(IStorageFile file) : IPickedFile
    {
        public string Name => file.Name;

        public Task<Stream> OpenReadAsync() => file.OpenReadAsync();
    }
}
