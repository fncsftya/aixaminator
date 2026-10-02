namespace Aixaminator.Models.Ai;

/// <summary>The things the application uses an AI model for.</summary>
public static class AiAction
{
    public const string DocumentCleaning = "Document Cleaning";
    public const string QuizGeneration = "Quiz Generation";

    public static IReadOnlyList<string> All { get; } = [DocumentCleaning, QuizGeneration];

    /// <summary>Actions that exist in the product but are not implemented yet; shown as placeholders in settings.</summary>
    public static IReadOnlyList<string> Unavailable { get; } = ["Document Splitting", "Document Classification"];
}
