using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aixaminator.Services;

/// <summary>Minimal client for the Google Gemini <c>generateContent</c> REST API.</summary>
internal sealed class GeminiClient(HttpClient httpClient)
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

    /// <summary>Gets a plain text response.</summary>
    public Task<string> GetChatResponseAsync(
        string apiKey, string modelId, IEnumerable<string> userMessages, string systemInstruction, CancellationToken cancellationToken) =>
        GenerateContentAsync(apiKey, modelId, userMessages, systemInstruction, schema: null, cancellationToken);

    /// <summary>Gets a JSON response conforming to <paramref name="schema"/>.</summary>
    public Task<string> GetJsonResponseAsync(
        string apiKey, string modelId, IEnumerable<string> userMessages, string systemInstruction, string schema, CancellationToken cancellationToken) =>
        GenerateContentAsync(apiKey, modelId, userMessages, systemInstruction, schema, cancellationToken);

    private async Task<string> GenerateContentAsync(
        string apiKey, string modelId, IEnumerable<string> userMessages, string systemInstruction, string? schema, CancellationToken cancellationToken)
    {
        var request = new GenerateContentRequest
        {
            Contents = userMessages.Select(m => new Content { Role = "user", Parts = [new Part { Text = m }] }).ToList(),
            SystemInstruction = new SystemInstruction { Parts = [new Part { Text = systemInstruction }] },
        };

        if (!string.IsNullOrEmpty(schema))
        {
            request.GenerationConfig = new GenerationConfig
            {
                ResponseMimeType = "application/json",
                ResponseSchema = JsonDocument.Parse(schema).RootElement.Clone(),
            };
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}{modelId}:generateContent")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Add("x-goog-api-key", apiKey);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<GenerateContentResponse>(cancellationToken);
        return body?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
    }

    private sealed class GenerateContentRequest
    {
        [JsonPropertyName("contents")]
        public List<Content> Contents { get; set; } = [];

        [JsonPropertyName("systemInstruction")]
        public SystemInstruction? SystemInstruction { get; set; }

        [JsonPropertyName("generationConfig")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GenerationConfig? GenerationConfig { get; set; }
    }

    private sealed class GenerationConfig
    {
        [JsonPropertyName("responseMimeType")]
        public string? ResponseMimeType { get; set; }

        [JsonPropertyName("responseSchema")]
        public JsonElement ResponseSchema { get; set; }
    }

    private sealed class SystemInstruction
    {
        [JsonPropertyName("parts")]
        public List<Part> Parts { get; set; } = [];
    }

    private sealed class Content
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("parts")]
        public List<Part>? Parts { get; set; }
    }

    private sealed class Part
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class GenerateContentResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate>? Candidates { get; set; }
    }

    private sealed class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }
    }
}
