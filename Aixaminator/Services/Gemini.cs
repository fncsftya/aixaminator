using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Aixaminator.Services
{
    public class Gemini
    {
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
        
        /// <summary>
        /// Get a chat response from the Google Gemini API
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for the request</param>
        /// <param name="apiKey">The Google API key</param>
        /// <param name="modelId">The model ID to use (e.g., "gemini-2.0-flash")</param>
        /// <param name="chatHistory">List of chat messages</param>
        /// <param name="systemInstruction">The system instruction for the model</param>
        /// <returns>The model's response text</returns>
        public async Task<string> GetChatResponse(
            HttpClient httpClient, 
            string apiKey, 
            string modelId, 
            List<string> chatHistory, 
            string systemInstruction)
        {
            return await GenerateContentAsync(
                httpClient,
                apiKey,
                modelId,
                chatHistory,
                systemInstruction,
                null
            );
        }

        /// <summary>
        /// Get a JSON-formatted response from the Google Gemini API
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for the request</param>
        /// <param name="apiKey">The Google API key</param>
        /// <param name="modelId">The model ID to use (e.g., "gemini-2.0-flash")</param>
        /// <param name="chatHistory">List of chat messages</param>
        /// <param name="systemInstruction">The system instruction for the model</param>
        /// <param name="schema">JSON schema that defines the structure of the response</param>
        /// <returns>The model's response text in JSON format</returns>
        public async Task<string> GetJsonResponse(
            HttpClient httpClient, 
            string apiKey, 
            string modelId, 
            List<string> chatHistory, 
            string systemInstruction,
            string schema)
        {
            return await GenerateContentAsync(
                httpClient,
                apiKey,
                modelId,
                chatHistory,
                systemInstruction,
                schema
            );
        }

        /// <summary>
        /// Common helper method for generating content from the Gemini API
        /// </summary>
        private async Task<string> GenerateContentAsync(
            HttpClient httpClient,
            string apiKey,
            string modelId,
            List<string> chatHistory,
            string systemInstruction,
            string schema = null)
        {
            // Prepare the request URL with the API key
            string requestUrl = $"{BaseUrl}{modelId}:generateContent?key={apiKey}";
            
            // Create contents from chat history
            var contents = new List<Content>();
            
            // Add messages from chat history alternating between user and model roles
            foreach (var message in chatHistory)
            {
                contents.Add(new Content
                {
                    Role = "user",
                    Parts = new List<Part> { new Part { Text = message } }
                });
            }
            
            // Create the request payload
            var request = new GenerateContentRequest
            {
                Contents = contents,
                SystemInstruction = new SystemInstruction
                {
                    Parts = new List<Part> { new Part { Text = systemInstruction } }
                }
            };

            // Add generation config if schema is provided
            if (!string.IsNullOrEmpty(schema))
            {
                // Parse the schema string to JsonElement
                JsonElement schemaElement = JsonDocument.Parse(schema).RootElement.Clone();
                
                request.GenerationConfig = new GenerationConfig
                {
                    ResponseMimeType = "application/json",
                    ResponseSchema = schemaElement
                };
            }
            
            // Send the request
            var response = await httpClient.PostAsJsonAsync(requestUrl, request);
            
            // Ensure success
            response.EnsureSuccessStatusCode();
            
            // Parse the response
            var generateContentResponse = await response.Content.ReadFromJsonAsync<GenerateContentResponse>();
            
            if (generateContentResponse?.Candidates == null || generateContentResponse.Candidates.Count == 0 ||
                generateContentResponse.Candidates[0].Content?.Parts == null || 
                generateContentResponse.Candidates[0].Content.Parts.Count == 0)
            {
                return string.Empty;
            }
            
            // Extract and return the text from the first candidate's content
            return generateContentResponse.Candidates[0].Content.Parts[0].Text;
        }
    }
    
    // Request Classes
    
    public class GenerateContentRequest
    {
        [JsonPropertyName("contents")]
        public List<Content> Contents { get; set; }
        
        [JsonPropertyName("systemInstruction")]
        public SystemInstruction SystemInstruction { get; set; }
        
        [JsonPropertyName("generationConfig")]
        public GenerationConfig GenerationConfig { get; set; }
    }
    
    public class GenerationConfig
    {
        [JsonPropertyName("responseMimeType")]
        public string ResponseMimeType { get; set; }
        
        [JsonPropertyName("responseSchema")]
        public JsonElement ResponseSchema { get; set; }
    }
    
    public class SystemInstruction
    {
        [JsonPropertyName("parts")]
        public List<Part> Parts { get; set; }
    }
    
    public class Content
    {
        [JsonPropertyName("role")]
        public string Role { get; set; }
        
        [JsonPropertyName("parts")]
        public List<Part> Parts { get; set; }
    }
    
    public class Part
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
    }
    
    // Response Classes
    
    public class GenerateContentResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate> Candidates { get; set; }
        
        [JsonPropertyName("promptFeedback")]
        public PromptFeedback PromptFeedback { get; set; }
    }
    
    public class Candidate
    {
        [JsonPropertyName("content")]
        public Content Content { get; set; }
        
        [JsonPropertyName("finishReason")]
        public string FinishReason { get; set; }
        
        [JsonPropertyName("index")]
        public int Index { get; set; }
        
        [JsonPropertyName("safetyRatings")]
        public List<SafetyRating> SafetyRatings { get; set; }
    }
    
    public class SafetyRating
    {
        [JsonPropertyName("category")]
        public string Category { get; set; }
        
        [JsonPropertyName("probability")]
        public string Probability { get; set; }
    }
    
    public class PromptFeedback
    {
        [JsonPropertyName("safetyRatings")]
        public List<SafetyRating> SafetyRatings { get; set; }
    }
} 