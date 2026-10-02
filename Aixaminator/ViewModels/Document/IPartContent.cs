namespace Aixaminator.ViewModels.Document;

/// <summary>Content of the document page that follows the part selected in the sidebar.</summary>
public interface IPartContent
{
    /// <param name="partNumber">The selected part, or null if none is selected (e.g. it was just hidden).</param>
    /// <param name="text">The saved text of the part.</param>
    void ShowPart(int? partNumber, string text);
}
