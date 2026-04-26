namespace Aixaminator.Data;

public class DocumentPart
{
    public int PartNumber { get; set; }
    public Guid DocumentId { get; set; }

    /// <summary>
    ///  Should the part be displayed in a custom colour?
    /// </summary>
    public bool UseColour { get; set; } = false;
    /// <summary>
    /// The colour to display the part in.
    /// </summary>
    public string? Colour { get; set; }
    /// <summary>
    /// Custom name for the part.
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// Should the part be hidden in the list?
    /// </summary>
    public bool Hidden { get; set; } = false;

    public ICollection<Note> Notes { get; set; } = new List<Note>();
}
