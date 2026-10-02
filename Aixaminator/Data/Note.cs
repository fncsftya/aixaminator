namespace Aixaminator.Data;

public enum NoteKind
{
    /// <summary>Free-form text written by the user.</summary>
    Note = 0,

    /// <summary>A question (<see cref="Note.Text"/>) and its <see cref="Note.Answer"/>.</summary>
    Question = 1,

    /// <summary>A coloured highlight of some text in the document.</summary>
    Highlight = 2,
}

public class Note
{
    public int Id { get; set; }

    public Guid DocumentId { get; set; }

    /// <summary>The part the note belongs to; null for notes about the whole document.</summary>
    public int? DocumentPartNumber { get; set; }

    public NoteKind Kind { get; set; }

    /// <summary>
    /// Core text of the note: the highlighted text for highlights, the question for questions
    /// and the user-entered text for notes.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Text selected in the document when the note or question was created, if any.</summary>
    public string? Context { get; set; }

    /// <summary>Only used for questions.</summary>
    public string? Answer { get; set; }

    /// <summary>Only used for highlights: the #rrggbb colour of the highlight.</summary>
    public string? HighlightColour { get; set; }

    /// <summary>Where in the part's text the note applies. Cleared when the part's text is edited.</summary>
    public HighlightLocation? Location { get; set; }

    public bool IsHighlight => Kind == NoteKind.Highlight;

    public bool IsQuestion => Kind == NoteKind.Question;
}
