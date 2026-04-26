using Aixaminator.Data;
using Microsoft.Data.Sqlite;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Sqlite;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Embeddings;
using SemanticSlicer;
using SemanticSlicer.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Shared.AI.Data;
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
        
        // Get the action type (chat or embedding)
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
                // Default models if none specified
                else if (provider.Name == "openai")
                {
                    model = actionType switch
                    {
                        AiActionType.Chat => "gpt-4o-mini",
                        AiActionType.Embedding => "text-embedding-3-small",
                        _ => string.Empty
                    };
                }
                // Add Google provider defaults
                else if (provider.Name == "google")
                {
                    model = actionType switch
                    {
                        AiActionType.Chat => "gemini-2.0-flash",
                        AiActionType.Embedding => "text-embedding-004",
                        _ => string.Empty
                    };
                }
                
                return (provider, model);
            }
        }
        
        // Fallback to the first provider if available
        var fallbackProvider = settings.Ai.AiProviders.FirstOrDefault();
        if (fallbackProvider != null)
        {
            if (fallbackProvider.Name == "openai")
            {
                model = actionType switch
                {
                    AiActionType.Chat => "gpt-4o-mini",
                    AiActionType.Embedding => "text-embedding-3-small",
                    _ => string.Empty
                };
            }
            // Add Google provider defaults for fallback case
            else if (fallbackProvider.Name == "google")
            {
                model = actionType switch
                {
                    AiActionType.Chat => "gemini-2.0-flash",
                    AiActionType.Embedding => "text-embedding-004",
                    _ => string.Empty
                };
            }
            
            return (fallbackProvider, model);
        }
        
        return (new AiProvider(), string.Empty);
    }

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

    public async Task<string> ClassifyDocument(List<string> topics)
    {
        var (provider, model) = GetProviderAndModelForAction(AiAction.DocumentClassification);

        var parts = topics.Select(t => $"""
            <topic>
                <sentences>
                    {string.Join("\n", t.Split(" ~@@~ ").Select(s => $"<sentence>{s}</sentence>"))}
                </sentences>
            </topic>
        """).ToList();
        
        var systemPrompt = """
            You are a tool for classifying text.
            You will be given a list of topics represented by simple xml markup.
            Determine the themes for each topic from the provided sentences.
        """;
        var userPrompt = $"""
            I will attach some text sampled from various topics.
            Note: the text has been extracted and may have artefacts.
            Reply with a concise, comma-separated list of themes for each topic. One topic per line.
            If possible, also perform named entity recognition. If there are any names present,
            state them on the final line as comma-separated values and indicate them with an asterisk.
            If names have been included in previous topics, do not include them in
            subsequent ones.

            Example input:
            <topics>
                <topic>
                    <sentences>
                        <sentence>In Chapters 2 and 3 we have studied developments in theological and philosophical hermeneutics.</sentence>
                        <sentence>In this chapter we have reported and commented on the develop-
                                  ment of philosophical hermeneutics from Schleiermacher through
                                  Dilthey, Husserl, Heidegger, Gadamer, Habermas to Ricreur.</sentence>
                    </sentences>
                </topic>
            </topics>

            Example output:
            Theology, philosophy.
            * Schleiermacher, Dilthey, Husserl, Heidegger, Gadamer, Habermas, Ricreur.

            Classify the following text:
            <topics>
            {string.Join("", parts)}
            </topics>
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
        if (provider.Name == "openai")
        {
            // Use the selected model or fall back to default if empty
            string modelToUse = !string.IsNullOrEmpty(model) ? model : "gpt-4o-mini";

            OpenAIChatCompletionService chat = new(
                modelId: modelToUse,
                apiKey: provider.ApiKey,
                httpClient: ClientFactory.CreateClient());
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
                jsonSchemaIsStrict: true);

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
        if (provider.Name == "openai")
        {            
            // Use the selected model or fall back to default if empty
            string modelToUse = !string.IsNullOrEmpty(model) ? model : "gpt-4o-mini";
            
            OpenAIChatCompletionService chat = new(
                modelId: modelToUse,
                apiKey: provider.ApiKey,
                httpClient: ClientFactory.CreateClient());
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

    public async Task EmbedDocument(string targetPath, string documentText)
    {
        var slicer = new Slicer(new() { MaxChunkTokenCount = 150 });
        var chunks = slicer.GetDocumentChunks(documentText).Chunk(1536);

        Debug.Assert(chunks is not null);
        
        var (provider, model) = GetProviderAndModelForAction(AiAction.DocumentSplitting);
        
        if (provider.Name == "openai" || provider.Name == "google")
        {
            await DoEmbedding(targetPath, chunks, provider, model);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private async Task DoEmbedding(string targetPath, IEnumerable<DocumentChunk[]> chunks, AiProvider provider, string model)
    {
        var sw = Stopwatch.StartNew();
        var tempPath = Path.GetTempFileName();
        
        try
        {
            using (var connection = new SqliteConnection(@"Data Source=:memory:;Pooling=false"))
            {
                await connection.OpenAsync();
                connection.EnableExtensions(true);
                connection.LoadExtension("vec0");
                
                await GenerateEmbeddings(connection, chunks, provider, model);

                using (var bakConnection = new SqliteConnection(@$"Data Source={tempPath};Pooling=false"))
                {
                    connection.BackupDatabase(bakConnection);
                    SqliteConnection.ClearPool(bakConnection);
                }

                await connection.CloseAsync();
                SqliteConnection.ClearPool(connection);
            }

            File.Copy(tempPath, targetPath, true);
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete temporary file: {ex.Message}");
            }

            Debug.WriteLine($"Embeddings generated in {sw.ElapsedMilliseconds} ms");
            sw.Stop();
        }
    }

    private async Task GenerateEmbeddings(SqliteConnection connection, IEnumerable<DocumentChunk[]> chunks, AiProvider provider, string model)
    {
        var vectorStore = new SqliteVectorStore(connection);

        #pragma warning disable SKEXP0010, SKEXP0001
        // Use the selected model or fall back to default if empty
        string modelToUse = !string.IsNullOrEmpty(model) ? model : 
            provider.Name == "google" ? "text-embedding-004" : "text-embedding-3-small";
        
        // Create the appropriate embedding service based on the provider
        ITextEmbeddingGenerationService tes;
        
        if (provider.Name == "openai")
        {
            tes = new OpenAITextEmbeddingGenerationService(
                modelToUse,
                provider.ApiKey);
        }
        else if (provider.Name == "google")
        {
            #pragma warning disable SKEXP0070
            tes = new GoogleAITextEmbeddingGenerationService(
                modelToUse,
                provider.ApiKey,
                httpClient: ClientFactory.CreateClient());
            #pragma warning restore SKEXP0070
        }
        else
        {
            throw new NotImplementedException($"Provider '{provider.Name}' is not supported for embeddings.");
        }

        // Create the appropriate collection based on provider
        string collectionName = "sk_document_chunks";
        
        // Handle different provider types
        if (provider.Name.ToLowerInvariant() == "google")
        {
            var collection = vectorStore.GetCollection<ulong, GoogleDocumentChunk>(collectionName);
            await collection.CreateCollectionIfNotExistsAsync();
            
            var tasks = chunks.Select(async group =>
            {
                var embeddings = await tes.GenerateEmbeddingsAsync(group.Select(c => c.Content).ToList());
                
                // Create Google document chunks
                var googleChunks = embeddings.Select((embedding, i) =>
                {
                    var chunk = group[i];
                    return new GoogleDocumentChunk
                    {
                        ChunkId = (ulong)chunk.Index,
                        Content = chunk.Content,
                        Embedding = embedding
                    };
                }).ToList();
                
                await collection.UpsertBatchAsync(googleChunks).LastAsync();
            });
            
            using var transaction = await connection.BeginTransactionAsync();
            await Task.WhenAll(tasks);
            await transaction.CommitAsync();
        }
        else // OpenAI or default
        {
            var collection = vectorStore.GetCollection<ulong, OpenAIDocumentChunk>(collectionName);
            await collection.CreateCollectionIfNotExistsAsync();
            
            var tasks = chunks.Select(async group =>
            {
                var embeddings = await tes.GenerateEmbeddingsAsync(group.Select(c => c.Content).ToList());
                
                // Create OpenAI document chunks
                var openAIChunks = embeddings.Select((embedding, i) =>
                {
                    var chunk = group[i];
                    return new OpenAIDocumentChunk
                    {
                        ChunkId = (ulong)chunk.Index,
                        Content = chunk.Content,
                        Embedding = embedding
                    };
                }).ToList();
                
                await collection.UpsertBatchAsync(openAIChunks).LastAsync();
            });
            
            using var transaction = await connection.BeginTransactionAsync();
            await Task.WhenAll(tasks);
            await transaction.CommitAsync();
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
        }
        catch
        {
            return false;
        }
        
        return false;
    }
}
