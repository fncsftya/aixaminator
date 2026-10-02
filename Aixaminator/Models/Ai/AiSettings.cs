namespace Aixaminator.Models.Ai;

/// <summary>
/// The configured AI providers and which provider/model is used for each <see cref="AiAction"/>.
/// An empty string in the maps means "not set".
/// </summary>
public sealed class AiSettings
{
    public List<AiProvider> AiProviders { get; set; } = [];

    public Dictionary<string, string> ActionProviderMap { get; set; } = [];

    public Dictionary<string, string> ActionModelMap { get; set; } = [];

    /// <summary>Models offered per provider and action type. The first model listed is the default.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<AiActionType, IReadOnlyList<AiModel>>> AvailableModels { get; } =
        new Dictionary<string, IReadOnlyDictionary<AiActionType, IReadOnlyList<AiModel>>>
        {
            // https://platform.openai.com/docs/models
            ["openai"] = new Dictionary<AiActionType, IReadOnlyList<AiModel>>
            {
                [AiActionType.Chat] = [new("gpt-4o-mini", "GPT-4o Mini"), new("gpt-4o", "GPT-4o")],
            },
            // https://ai.google.dev/gemini-api/docs/models/gemini#model-variations
            ["google"] = new Dictionary<AiActionType, IReadOnlyList<AiModel>>
            {
                [AiActionType.Chat] = [new("gemini-2.0-flash", "Gemini 2.0 Flash"), new("gemini-2.0-flash-lite", "Gemini 2.0 Flash Lite")],
            },
            // https://openrouter.ai/models
            ["openrouter"] = new Dictionary<AiActionType, IReadOnlyList<AiModel>>
            {
                [AiActionType.Chat] = [new("deepseek/deepseek-v4.1-flash", "DeepSeek V4.1 Flash")],
            },
        };

    public static IReadOnlyDictionary<string, string> ProviderNames { get; } = new Dictionary<string, string>
    {
        ["openai"] = "OpenAI",
        ["google"] = "Google",
        ["openrouter"] = "OpenRouter",
    };

    public static IReadOnlyDictionary<string, AiActionType> ActionTypes { get; } = new Dictionary<string, AiActionType>
    {
        [AiAction.DocumentCleaning] = AiActionType.Chat,
        [AiAction.QuizGeneration] = AiActionType.Chat,
    };

    public static IReadOnlyList<string> AvailableProviders { get; } = [.. AvailableModels.Keys];

    public static string DisplayName(string provider) => ProviderNames.GetValueOrDefault(provider, provider);

    /// <summary>The models a provider offers for an action; empty if the provider is unknown or not set.</summary>
    public static IReadOnlyList<AiModel> ModelsFor(string provider, string action) =>
        AvailableModels.TryGetValue(provider, out var byType) && byType.TryGetValue(ActionTypes[action], out var models)
            ? models
            : [];

    public static string DefaultModel(string provider, string action) =>
        ModelsFor(provider, action).FirstOrDefault()?.Id ?? string.Empty;

    /// <summary>
    /// Adds a provider, or updates the API key if it is already configured.
    /// Any action without a provider is mapped to the new provider's default model.
    /// </summary>
    public void AddProvider(AiProvider provider)
    {
        if (!AvailableModels.ContainsKey(provider.Name))
        {
            throw new ArgumentException($"Unknown AI provider '{provider.Name}'.", nameof(provider));
        }

        Normalise();

        var existing = AiProviders.FirstOrDefault(p => p.Name == provider.Name);
        if (existing is not null)
        {
            existing.ApiKey = provider.ApiKey;
            return;
        }

        AiProviders.Add(provider);
        foreach (var action in AiAction.All.Where(a => ActionProviderMap[a] == string.Empty))
        {
            SetActionProvider(action, provider.Name);
        }
    }

    /// <summary>Removes a provider; actions that used it are left without a provider.</summary>
    public void RemoveProvider(string providerName)
    {
        AiProviders.RemoveAll(p => p.Name == providerName);
        Normalise();
    }

    /// <summary>Sets the provider for an action and selects that provider's default model.</summary>
    public void SetActionProvider(string action, string providerName)
    {
        ActionProviderMap[action] = providerName;
        ActionModelMap[action] = DefaultModel(providerName, action);
    }

    public void SetActionModel(string action, string model)
    {
        ActionModelMap[action] = model;
    }

    /// <summary>
    /// Works out which provider and model to use for an action: the mapped one if it is configured,
    /// otherwise the default model of the first configured provider.
    /// </summary>
    /// <returns>Null when no provider is configured.</returns>
    public (AiProvider Provider, string Model)? ResolveAction(string action)
    {
        if (ActionProviderMap.TryGetValue(action, out var providerName)
            && AiProviders.FirstOrDefault(p => p.Name == providerName) is { } provider)
        {
            var model = ActionModelMap.GetValueOrDefault(action);
            return (provider, string.IsNullOrEmpty(model) ? DefaultModel(provider.Name, action) : model);
        }

        if (AiProviders.FirstOrDefault() is { } fallback)
        {
            return (fallback, DefaultModel(fallback.Name, action));
        }

        return null;
    }

    /// <summary>Ensures every action has an entry and drops references to providers that aren't configured.</summary>
    public void Normalise()
    {
        AiProviders ??= [];
        ActionProviderMap ??= [];
        ActionModelMap ??= [];

        AiProviders.RemoveAll(p => p is null || !AvailableModels.ContainsKey(p.Name));

        foreach (var action in AiAction.All)
        {
            var provider = ActionProviderMap.GetValueOrDefault(action) ?? string.Empty;
            if (provider != string.Empty && AiProviders.All(p => p.Name != provider))
            {
                provider = string.Empty;
            }

            var model = ActionModelMap.GetValueOrDefault(action) ?? string.Empty;
            if (provider == string.Empty || ModelsFor(provider, action).All(m => m.Id != model))
            {
                model = provider == string.Empty ? string.Empty : DefaultModel(provider, action);
            }

            ActionProviderMap[action] = provider;
            ActionModelMap[action] = model;
        }
    }
}
