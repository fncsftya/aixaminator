using System.ComponentModel.DataAnnotations.Schema;

namespace Aixaminator.Data;

public class Note
{
    public int Id { get; set; }
    public Guid DocumentId { get; set; }
    public int? DocumentPartNumber { get; set; }
    /// <summary>
    /// Text taken from the highlight.
    /// Null for highlights (which use Text instead).
    /// Maybe null for questions or notes (when applied generally to a document or part).
    /// </summary>
    public string? Context { get; set; }
    /// <summary>
    /// Core text of the note.
    /// Will be the text from the highlight for highlights.
    /// Will be the question for questions.
    /// Will be the user-entered text for notes.
    /// </summary>
    public string Text { get; set; }
    /// <summary>
    /// Only used for questions. Null for notes and highlights.
    /// </summary>
    public string? Answer { get; set; }
    public string? HighlightColour { get; set; }
    public HighlightLocation? Location { get; set; }

    public Document Document { get; set; }
    public DocumentPart? Part { get; set; }

    [NotMapped]
    public bool IsHighlight => !string.IsNullOrWhiteSpace(HighlightColour);
    [NotMapped]
    public bool IsQuestion => !string.IsNullOrWhiteSpace(Answer);
}
