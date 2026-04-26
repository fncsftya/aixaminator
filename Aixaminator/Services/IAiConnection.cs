using Aixaminator.Data;
using Aixaminator.Models;

namespace Aixaminator.Services;

public interface IAiConnection
{
    Task<bool> TestConnection(string provider, string apiKey);
    Task EmbedDocument(string targetPath, string documentText);
    Task<string> CleanPage(string pageText);
    Task<Quiz> GenerateQuiz(Document document, string highlightsText);
    Task<string> ClassifyDocument(List<string> topics);
}
