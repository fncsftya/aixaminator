namespace Aixaminator.Data;

/// <summary>
/// A document that has been imported into the library.
/// The text of each part is stored on disk (see <see cref="Services.IDocumentContentStore"/>);
/// everything else lives in the database.
/// </summary>
public class Document
{
    public Guid Id { get; set; }

    /// <summary>Name shown to the user. It can be changed.</summary>
    public required string Name { get; set; }

    /// <summary>Optional summary, used to give the AI more context when generating quizzes.</summary>
    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<DocumentPart> Parts { get; set; } = [];

    public List<Note> Notes { get; set; } = [];
}
