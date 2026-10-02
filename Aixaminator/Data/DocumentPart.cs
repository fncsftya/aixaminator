namespace Aixaminator.Data;

/// <summary>
/// A section of a document. Every part of a document has a row, created when the document is imported.
/// </summary>
public class DocumentPart
{
    public Guid DocumentId { get; set; }

    /// <summary>Zero-based index of the part within the document.</summary>
    public int PartNumber { get; set; }

    /// <summary>Custom name for the part. When empty, <see cref="DisplayName"/> falls back to "Part N".</summary>
    public string? Name { get; set; }

    /// <summary>Should the part be displayed with a custom colour?</summary>
    public bool UseColour { get; set; }

    /// <summary>The colour (#rrggbb) to display the part with.</summary>
    public string? Colour { get; set; }

    /// <summary>Should the part be hidden from the list of parts?</summary>
    public bool Hidden { get; set; }

    /// <summary>Has the user finished reading this part?</summary>
    public bool IsRead { get; set; }

    public string DisplayName => DefaultName(PartNumber, Name);

    public static string DefaultName(int partNumber, string? customName = null) =>
        string.IsNullOrWhiteSpace(customName) ? $"Part {partNumber + 1}" : customName;
}
