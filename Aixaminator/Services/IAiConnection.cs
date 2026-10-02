using Aixaminator.Models;

namespace Aixaminator.Services;

/// <summary>Talks to the configured AI providers.</summary>
public interface IAiConnection
{
    /// <summary>Checks that an API key works for a provider.</summary>
    Task<bool> TestConnectionAsync(string provider, string apiKey, CancellationToken cancellationToken = default);

    /// <summary>Asks the AI to remove noise (headers, footers, page numbers...) from extracted text.</summary>
    /// <exception cref="AiNotConfiguredException">No provider is configured.</exception>
    Task<string> CleanTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Generates a multiple-choice quiz from key highlights of a document.</summary>
    /// <exception cref="AiNotConfiguredException">No provider is configured.</exception>
    Task<Quiz> GenerateQuizAsync(string documentName, string? documentDescription, string highlights, CancellationToken cancellationToken = default);
}

public sealed class AiNotConfiguredException()
    : InvalidOperationException("No AI provider is configured. Add one in Settings.");
