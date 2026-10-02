namespace Aixaminator.Services;

/// <summary>A file chosen by the user.</summary>
public interface IPickedFile
{
    string Name { get; }

    Task<Stream> OpenReadAsync();
}

public interface IFilePickerService
{
    /// <summary>Lets the user choose a document (PDF, EPUB or text) to import.</summary>
    /// <returns>The chosen file, or null if the user cancelled.</returns>
    Task<IPickedFile?> PickDocumentAsync();
}
