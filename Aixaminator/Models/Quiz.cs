using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aixaminator.Models
{
    public class Quiz
    {
        [JsonPropertyName("quiz")]
        public QuizContent Content { get; set; }
    }

    public class QuizContent
    {
        [JsonPropertyName("questions")]
        public List<QuizQuestion> Questions { get; set; }
    }

    public class QuizQuestion
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("correctAnswer")]
        public string CorrectAnswer { get; set; }

        [JsonPropertyName("incorrectAnswers")]
        public List<string> IncorrectAnswers { get; set; }
    }
} 