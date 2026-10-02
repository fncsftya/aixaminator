using Newtonsoft.Json;

namespace Aixaminator.Data.Ai;

public class AiSettings
{
    public List<AiProvider> AiProviders { get; set; } = new List<AiProvider>();

    public Dictionary<string, string> ActionProviderMap { get; set; } = new Dictionary<string, string>();

    public Dictionary<string, string> ActionModelMap { get; set; } = new Dictionary<string, string>();

    public void AddProvider(AiProvider provider)
    {
        AiProviders.Add(provider);
        if (AiProviders.Count == 1)
        {
            foreach (var action in AiAction.AllActions)
            {
                ActionProviderMap[action] = provider.Name;
                var actionType = ActionTypes[action];
                ActionModelMap[action] = AvailableModels[provider.Name][actionType].First().Key;
            }
        }
    }

    public void InitializeActionMaps()
    {
        foreach (var action in ActionTypes.Keys)
        {
            if (!ActionProviderMap.ContainsKey(action))
            {
                ActionProviderMap[action] = "";
            }
            if (!ActionModelMap.ContainsKey(action))
            {
                ActionModelMap[action] = "";
            }
        }
    }

    public void RemoveProvider(AiProvider provider)
    {
        AiProviders.Remove(provider);
        if (AiProviders.Count == 0)
        {
            ActionProviderMap.Clear();
            ActionModelMap.Clear();
            InitializeActionMaps();
        }
        else
        {
            foreach (var (action, providerName) in ActionProviderMap.Where(p => p.Value == provider.Name))
            {
                ActionProviderMap[action] = "";
                ActionModelMap[action] = "";
            }
        }
    }

    // NOTE: the first model in each action type is the default model
    [JsonIgnore]
    public static readonly Dictionary<string, Dictionary<AiActionType, Dictionary<string, string>>> AvailableModels = new()
    {
        // https://platform.openai.com/docs/models
        {
            "openai", new()
            {
                {
                    AiActionType.Chat, new()
                    {
                        { "gpt-4o-mini", "GPT-4o Mini" },
                        { "gpt-4o", "GPT-4o" }
                    }
                }
            }
        },
        // https://ai.google.dev/gemini-api/docs/models/gemini#model-variations
        {
            "google", new()
            {
                {
                    AiActionType.Chat, new()
                    {
                        { "gemini-2.0-flash", "Gemini 2.0 Flash" },
                        { "gemini-2.0-flash-lite", "Gemini 2.0 Flash Lite" }
                    }
                }
            }
        }
    };

    public static readonly Dictionary<string, string> ProviderNames = new()
    {
        { "openai", "OpenAI" },
        { "google", "Google" }
    };

    [JsonIgnore]
    public static List<string> AvailableProviders => AvailableModels.Keys.ToList();

    [JsonIgnore]
    public static readonly Dictionary<string, AiActionType> ActionTypes = new()
    {
        { AiAction.DocumentCleaning, AiActionType.Chat },
        { AiAction.QuizGeneration, AiActionType.Chat }
    };
}