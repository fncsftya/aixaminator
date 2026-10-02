namespace Aixaminator.Data;

/// <summary>
/// A range of text within a document part, expressed as paragraph keys (zero-based paragraph indexes,
/// see <see cref="Features.ParagraphParser"/>) and character offsets within those paragraphs.
/// The end offset is exclusive.
/// </summary>
public sealed record HighlightLocation(int StartKey, int StartOffset, int EndKey, int EndOffset);
