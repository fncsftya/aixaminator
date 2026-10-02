using System.Text.Json.Serialization;

namespace Aixaminator.Models;

/// <summary>A multiple-choice quiz, as returned by the AI (see the JSON schema in <see cref="Services.AiConnection"/>).</summary>
public sealed class Quiz
{
    [JsonPropertyName("quiz")]
    public QuizContent Content { get; set; } = new();
}

public sealed class QuizContent
{
    [JsonPropertyName("questions")]
    public List<QuizQuestion> Questions { get; set; } = [];
}

public sealed class QuizQuestion
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("correctAnswer")]
    public string CorrectAnswer { get; set; } = string.Empty;

    [JsonPropertyName("incorrectAnswers")]
    public List<string> IncorrectAnswers { get; set; } = [];
}
