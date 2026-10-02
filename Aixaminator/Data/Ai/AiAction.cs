namespace Aixaminator.Data.Ai;

public static class AiAction
{
    public const string DocumentCleaning = "Document Cleaning";
    public const string QuizGeneration = "Quiz Generation";

    public static HashSet<string> AllActions = new HashSet<string>
    {
        DocumentCleaning,
        QuizGeneration
    };
}