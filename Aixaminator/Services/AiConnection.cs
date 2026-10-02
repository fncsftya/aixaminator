using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Aixaminator.Models;
using Aixaminator.Models.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Aixaminator.Services;

public sealed class AiConnection(
    IHttpClientFactory clientFactory,
    ISettingsService settingsService,
    ILogger<AiConnection> logger) : IAiConnection
{
    // OpenRouter exposes an OpenAI-compatible API, so both share the OpenAI connector.
    private const string OpenRouterBaseUrl = "https://openrouter.ai/api/v1";

    private const string QuizSchema = """
        {
          "type": "object",
          "properties": {
            "quiz": {
              "type": "object",
              "properties": {
                "questions": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "text": { "type": "string" },
                      "correctAnswer": { "type": "string" },
                      "incorrectAnswers": { "type": "array", "items": { "type": "string" } }
                    },
                    "required": ["text", "correctAnswer", "incorrectAnswers"]
                  }
                }
              },
              "required": ["questions"]
            }
          },
          "required": ["quiz"]
        }
        """;

    private static readonly JsonSerializerOptions QuizJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<string> CleanTextAsync(string text, CancellationToken cancellationToken = default)
    {
        var (provider, model) = Resolve(AiAction.DocumentCleaning);

        const string systemPrompt = "You are a tool for cleaning up text. Only reply with the cleaned text. Do not summarise the text.";
        var userPrompt = $"""
            I will attach some text which was extracted from a pdf, webpage, wiki article or other text document
            and therefore may have noisy artefacts which make reading harder. These might be things
            like page titles and footers, tables of contents, OCR issues, document metadata
            references and footnotes. Pay particular attention to out of context phrases which may
            resemble titles, as well as out of context number which may represent footnotes
            or page numbers.
            Use two new lines to separate paragraphs, heading and titles.
            Clean the following text:

            {text}
            """;

        return await SendChatRequestAsync(provider, model, systemPrompt, [userPrompt], cancellationToken);
    }

    public async Task<Quiz> GenerateQuizAsync(
        string documentName, string? documentDescription, string highlights, CancellationToken cancellationToken = default)
    {
        var (provider, model) = Resolve(AiAction.QuizGeneration);

        const string systemPrompt = "You are a tool for generating educational quizzes based on document highlights.";
        var userPrompt = new StringBuilder()
            .AppendLine("Create a quiz based on the following key highlights from a document.")
            .AppendLine("The quiz should include at least 5 and up to 10 multiple-choice questions.")
            .AppendLine("Each question should have 4 options with one correct answer.")
            .AppendLine("Use a variety of modal verbs in the questions you provide.")
            .AppendLine("Try to avoid using the phrase 'According to the text' in the quiz questions.")
            .AppendLine("Prioritize questions relevant to the content provided. Do not include questions that are not relevant to the content.")
            .AppendLine("If you can determine the source of the content, use your knowledge to aid you in creating the quiz.")
            .AppendLine($"The user is asking for a quiz on the document: {documentName}")
            .AppendLine("This name may not be accurate.");

        if (!string.IsNullOrWhiteSpace(documentDescription))
        {
            userPrompt
                .AppendLine("The user has also provided the following description for this document:")
                .AppendLine(documentDescription);
        }

        userPrompt
            .AppendLine("Here are the key highlights to use:")
            .AppendLine(highlights);

        try
        {
            var response = await SendJsonRequestAsync(provider, model, systemPrompt, [userPrompt.ToString()], QuizSchema, cancellationToken);
            var quiz = JsonSerializer.Deserialize<Quiz>(response, QuizJsonOptions);
            if (quiz is null || quiz.Content.Questions.Count == 0)
            {
                throw new JsonException("The response did not contain any questions.");
            }
            return quiz;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Quiz generation failed");
            throw new InvalidOperationException("Unable to generate the quiz.", ex);
        }
    }

    public async Task<bool> TestConnectionAsync(string provider, string apiKey, CancellationToken cancellationToken = default)
    {
        var client = clientFactory.CreateClient();
        using var request = provider switch
        {
            "openai" => new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models"),
            "google" => new HttpRequestMessage(HttpMethod.Get, "https://generativelanguage.googleapis.com/v1beta/models"),
            // The models list is public, so check the key itself instead
            "openrouter" => new HttpRequestMessage(HttpMethod.Get, $"{OpenRouterBaseUrl}/key"),
            _ => null,
        };

        if (request is null)
        {
            return false;
        }

        try
        {
            if (provider == "google")
            {
                request.Headers.Add("x-goog-api-key", apiKey);
            }
            else
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Network failures, timeouts and malformed keys all mean the connection doesn't work.
            logger.LogInformation(ex, "Connection test for {Provider} failed", provider);
            return false;
        }
    }

    private (AiProvider Provider, string Model) Resolve(string action) =>
        settingsService.Settings.Ai.ResolveAction(action) ?? throw new AiNotConfiguredException();

    private OpenAIChatCompletionService CreateOpenAiCompatibleChat(AiProvider provider, string model)
    {
        if (provider.Name == "openrouter")
        {
#pragma warning disable SKEXP0010 // custom endpoints are experimental
            return new OpenAIChatCompletionService(
                modelId: model,
                endpoint: new Uri(OpenRouterBaseUrl),
                apiKey: provider.ApiKey,
                httpClient: clientFactory.CreateClient());
#pragma warning restore SKEXP0010
        }

        return new OpenAIChatCompletionService(modelId: model, apiKey: provider.ApiKey, httpClient: clientFactory.CreateClient());
    }

    private static ChatHistory CreateHistory(string systemPrompt, IEnumerable<string> userPrompts)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(systemPrompt);
        foreach (var prompt in userPrompts)
        {
            history.AddUserMessage(prompt);
        }
        return history;
    }

    private async Task<string> SendChatRequestAsync(
        AiProvider provider, string model, string systemPrompt, IReadOnlyList<string> userPrompts, CancellationToken cancellationToken)
    {
        switch (provider.Name)
        {
            case "openai" or "openrouter":
                var chat = CreateOpenAiCompatibleChat(provider, model);
                var response = await chat.GetChatMessageContentAsync(
                    CreateHistory(systemPrompt, userPrompts), cancellationToken: cancellationToken);
                return response.ToString();

            case "google":
                return await new GeminiClient(clientFactory.CreateClient())
                    .GetChatResponseAsync(provider.ApiKey, model, userPrompts, systemPrompt, cancellationToken);

            default:
                throw new NotSupportedException($"Unsupported AI provider '{provider.Name}'.");
        }
    }

    private async Task<string> SendJsonRequestAsync(
        AiProvider provider, string model, string systemPrompt, IReadOnlyList<string> userPrompts, string schema, CancellationToken cancellationToken)
    {
        switch (provider.Name)
        {
            case "openai" or "openrouter":
                var chat = CreateOpenAiCompatibleChat(provider, model);
                var responseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "quiz_result",
                    jsonSchema: BinaryData.FromString(schema),
                    // The schema doesn't set additionalProperties, which strict mode requires on OpenRouter
                    jsonSchemaIsStrict: provider.Name == "openai");
#pragma warning disable SKEXP0010 // structured outputs are experimental
                var settings = new OpenAIPromptExecutionSettings { ResponseFormat = responseFormat };
#pragma warning restore SKEXP0010
                var response = await chat.GetChatMessageContentAsync(
                    CreateHistory(systemPrompt, userPrompts), settings, cancellationToken: cancellationToken);
                return response.ToString();

            case "google":
                return await new GeminiClient(clientFactory.CreateClient())
                    .GetJsonResponseAsync(provider.ApiKey, model, userPrompts, systemPrompt, schema, cancellationToken);

            default:
                throw new NotSupportedException($"Unsupported AI provider '{provider.Name}'.");
        }
    }
}
