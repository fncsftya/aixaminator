using Aixaminator.Data;
using Aixaminator.Models;

namespace Aixaminator.Services;

public interface IAiConnection
{
    Task<bool> TestConnection(string provider, string apiKey);
    Task<string> CleanPage(string pageText);
    Task<Quiz> GenerateQuiz(Document document, string highlightsText);
}
