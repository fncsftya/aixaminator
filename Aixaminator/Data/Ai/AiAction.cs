namespace Aixaminator.Data.Ai;

public static class AiAction
{
    public const string DocumentSplitting = "Document Splitting";
    public const string DocumentCleaning = "Document Cleaning";
    public const string DocumentClassification = "Document Classification";
    public const string QuizGeneration = "Quiz Generation";

    public static HashSet<string> AllActions = new HashSet<string>
    {
        DocumentSplitting,
        DocumentCleaning,
        DocumentClassification,
        QuizGeneration
    };
}