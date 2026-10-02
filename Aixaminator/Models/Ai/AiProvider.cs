namespace Aixaminator.Models.Ai;

/// <summary>A configured AI provider. <see cref="Name"/> is a key of <see cref="AiSettings.ProviderNames"/>.</summary>
public sealed class AiProvider
{
    public string Name { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>A model offered by a provider.</summary>
public sealed record AiModel(string Id, string DisplayName);
