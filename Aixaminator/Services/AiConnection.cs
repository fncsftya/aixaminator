using Aixaminator.Data;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Aixaminator.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Aixaminator.Data.Ai;

namespace Aixaminator.Services;

public class AiConnection : IAiConnection
{
    private IHttpClientFactory ClientFactory { get; set; }
    private ISettingsService SettingsService { get; }
    private ILogger<AiConnection> Log { get; set; }

    public AiConnection(IHttpClientFactory clientFactory, ISettingsService settingsService, ILogger<AiConnection> logger)
    {
        ClientFactory = clientFactory;
        SettingsService = settingsService;
        Log = logger;
    }

    private (AiProvider Provider, string Model) GetProviderAndModelForAction(string action)
    {
        Debug.Assert(AiAction.AllActions.Contains(action));

        var settings = SettingsService.Settings;
        string model = string.Empty;
        
        // Get the action type
        var actionType = AiSettings.ActionTypes[action];
        
        // If we have an action-provider mapping, use it
        if (settings.Ai.ActionProviderMap.TryGetValue(action, out var providerName))
        {
            var provider = settings.Ai.AiProviders.FirstOrDefault(p => p.Name == providerName);
            if (provider != null)
            {
                // Get the model if configured
                if (settings.Ai.ActionModelMap.TryGetValue(action, out var modelName) && 
                    !string.IsNullOrEmpty(modelName))
                {
                    model = modelName;
                }
                // Default model if none specified
                else
                {
                    model = DefaultModel(provider.Name, actionType);
                }

                return (provider, model);
            }
        }

        // Fallback to the first provider if available
        var fallbackProvider = settings.Ai.AiProviders.FirstOrDefault();
        if (fallbackProvider != null)
        {
            model = DefaultModel(fallbackProvider.Name, actionType);
            return (fallbackProvider, model);
        }
        
        return (new AiProvider(), string.Empty);
    }

    private static string DefaultModel(string providerName, AiActionType actionType)
    {
        // The first model listed for each action type is the default
        if (AiSettings.AvailableModels.TryGetValue(providerName, out var models) &&
            models.TryGetValue(actionType, out var actionModels))
        {
            return actionModels.Keys.First();
        }
        return string.Empty;
    }

    // OpenRouter exposes an OpenAI-compatible API, so both share the OpenAI connector
    private OpenAIChatCompletionService CreateOpenAiCompatibleChat(AiProvider provider, string model)
    {
        string modelToUse = !string.IsNullOrEmpty(model) ? model : DefaultModel(provider.Name, AiActionType.Chat);

        if (provider.Name == "openrouter")
        {
#pragma warning disable SKEXP0010
            return new OpenAIChatCompletionService(
                modelId: modelToUse,
                endpoint: new Uri(OpenRouterBaseUrl),
                apiKey: provider.ApiKey,
                httpClient: ClientFactory.CreateClient());
#pragma warning restore SKEXP0010
        }

        return new OpenAIChatCompletionService(
            modelId: modelToUse,
            apiKey: provider.ApiKey,
            httpClient: ClientFactory.CreateClient());
    }

    private const string OpenRouterBaseUrl = "https://openrouter.ai/api/v1";

    public async Task<string> CleanPage(string pageText)
    {
        var (provider, model) = GetProviderAndModelForAction(AiAction.DocumentCleaning);
        
        var systemPrompt = "You are a tool for cleaning up text. Only reply with the cleaned text. Do not summarise the text.";
        var userPrompt = $"""
        I will attach some text which was extracted from a pdf, webpage, wiki article or other text document
        and therefore may have noisy artefacts which make reading harder. These might be things
        like page titles and footers, tables of contents, OCR issues, document metadata
        references and footnotes. Pay particular attention to out of context phrases which may
        resemble titles, as well as out of context number which may represent footnotes
        or page numbers.
        Use two new lines to separate paragraphs, heading and titles.
        Clean the following text:

{pageText}
""";

        return await SendChatRequest(provider, model, systemPrompt, new List<string> { userPrompt });
    }

    public async Task<Quiz> GenerateQuiz(Document document, string highlightsText)
    {
        var (provider, model) = GetProviderAndModelForAction(AiAction.QuizGeneration);
        
        var systemPrompt = "You are a tool for generating educational quizzes based on document highlights.";
        var userPrompt = new StringBuilder();
        userPrompt.AppendLine("Create a quiz based on the following key highlights from a document.");
        userPrompt.AppendLine("The quiz should include at least 5 and up to 10 multiple-choice questions.");
        userPrompt.AppendLine("Each question should have 4 options with one correct answer.");
        userPrompt.AppendLine("Use a variety of modal verbs in the questions you provide.");
        userPrompt.AppendLine("Try to avoid using the phrase 'According to the text' in the quiz questions.");
        userPrompt.AppendLine("Prioritize questions relevant to the content provided. Do not include questions that are not relevant to the content.");
        userPrompt.AppendLine("If you can determine the source of the content, use your knowledge to aid you in creating the quiz.");

        userPrompt.AppendLine($"The user is asking for a quiz on the document: {document.Name}");
        userPrompt.AppendLine($"This name may not be accurate.");

        userPrompt.AppendLine($"The user has also provided the following description for this document:");
        userPrompt.AppendLine(document.Description);

        userPrompt.AppendLine("Here are the key highlights to use:");
        userPrompt.AppendLine(highlightsText);

        var schema = """
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
                          "text": {
                            "type": "string"
                          },
                          "correctAnswer": {
                            "type": "string"
                          },
                          "incorrectAnswers": {
                            "type": "array",
                            "items": {
                              "type": "string"
                            }
                          }
                        },
                        "required": [
                          "text",
                          "correctAnswer",
                          "incorrectAnswers"
                        ]
                      }
                    }
                  },
                  "required": ["questions"]
                }
              },
              "required": ["quiz"]
            }
            """;

        try
        {
            var response = await SendJsonRequest(provider, model, systemPrompt, new List<string> { userPrompt.ToString() }, schema);
            // Deserialize the JSON response into Quiz object
            var quizObject = JsonSerializer.Deserialize<Quiz>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (quizObject == null)
            {
                Log.LogWarning("Quiz response could not be deserialised.");
                throw new JsonException("Unable to generate the quiz");
            }
            
            return quizObject;
        }
        catch (JsonException ex)
        {
            Log.LogWarning("Quiz generation failed: {0}", ex.Message);
            throw new Exception("Unable to generate the quiz", ex);
        }
    }

    private async Task<string> SendJsonRequest(AiProvider provider, string model, string systemPrompt, List<string> userPrompts, string schema)
    {
        if (provider.Name is "openai" or "openrouter")
        {
            var chat = CreateOpenAiCompatibleChat(provider, model);
            ChatHistory history = [];
            history.AddSystemMessage(systemPrompt);

            // Add each user prompt as a separate message
            foreach (var prompt in userPrompts)
            {
                history.AddUserMessage(prompt);
            }

            OpenAI.Chat.ChatResponseFormat responseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "quiz_result",
                jsonSchema: BinaryData.FromString(schema),
                // The schema doesn't set additionalProperties, which strict mode requires on OpenRouter
                jsonSchemaIsStrict: provider.Name == "openai");

#pragma warning disable SKEXP0010
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ResponseFormat = responseFormat,
            };
#pragma warning restore SKEXP0010
            var response = await chat.GetChatMessageContentAsync(history, executionSettings);
            return response.ToString();
        }
        else if (provider.Name == "google")
        {
            // Use the selected model or fall back to default if empty
            string modelToUse = !string.IsNullOrEmpty(model) ? model : "gemini-2.0-flash";

            // Use our custom Gemini implementation instead of SemanticKernel's
            var gemini = new Gemini();
            var chatHistory = userPrompts;

            var response = await gemini.GetJsonResponse(
                ClientFactory.CreateClient(),
                provider.ApiKey,
                modelToUse,
                chatHistory,
                systemPrompt,
                schema);

            return response;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private async Task<string> SendChatRequest(AiProvider provider, string model, string systemPrompt, List<string> userPrompts)
    {
        if (provider.Name is "openai" or "openrouter")
        {
            var chat = CreateOpenAiCompatibleChat(provider, model);
            ChatHistory history = [];
            history.AddSystemMessage(systemPrompt);
            
            // Add each user prompt as a separate message
            foreach (var prompt in userPrompts)
            {
                history.AddUserMessage(prompt);
            }

            var response = await chat.GetChatMessageContentAsync(history);
            return response.ToString();
        }
        else if (provider.Name == "google")
        {
            // Use the selected model or fall back to default if empty
            string modelToUse = !string.IsNullOrEmpty(model) ? model : "gemini-2.0-flash";
            
            // Use our custom Gemini implementation instead of SemanticKernel's
            var gemini = new Gemini();
            var chatHistory = userPrompts;
            
            var response = await gemini.GetChatResponse(
                ClientFactory.CreateClient(),
                provider.ApiKey,
                modelToUse,
                chatHistory,
                systemPrompt);
                
            return response;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public async Task<bool> TestConnection(string provider, string apiKey)
    {
        var client = ClientFactory.CreateClient();
        
        try
        {
            if (provider == "openai")
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                var response = await client.GetAsync("https://api.openai.com/v1/models");
                return response.IsSuccessStatusCode;
            }
            else if (provider == "google")
            {
                client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
                var response = await client.GetAsync("https://generativelanguage.googleapis.com/v1beta/models");
                return response.IsSuccessStatusCode;
            }
            else if (provider == "openrouter")
            {
                // The models list is public, so check the key itself instead
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                var response = await client.GetAsync($"{OpenRouterBaseUrl}/key");
                return response.IsSuccessStatusCode;
            }
        }
        catch
        {
            return false;
        }
        
        return false;
    }
}
