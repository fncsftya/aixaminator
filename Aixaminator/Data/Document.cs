using System.ComponentModel.DataAnnotations.Schema;

namespace Aixaminator.Data;

public class Document
{
    public Guid Id { get; set; }
    // Name is the name shown to the user. It can be changed.
    public string Name { get; set; }
    // Path is the name of the directory in the Documents folder.
    // It must not change.
    // TODO probably would make more sense to just use the id as the path
    // ie. remove this completely
    public string Path { get; set; }
    public string? Description { get; set; }
    public List<int> PartsRead { get; set; } = new();

    public ICollection<DocumentPart> Parts { get; set; } = new List<DocumentPart>();
    public ICollection<Note> Notes { get; set; } = new List<Note>();

    [NotMapped]
    public string FullPath => Utils.Constants.GetDocumentBasePath(Path);

    public bool HasRead(int part) => PartsRead.Contains(part);
}
