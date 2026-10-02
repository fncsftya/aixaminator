namespace Aixaminator.Models;

/// <summary>A named highlight colour.</summary>
public sealed record HighlightColour(string Name, string Hex)
{
    public static IReadOnlyList<HighlightColour> All { get; } =
    [
        new("Yellow", "#f59e0b"),
        new("Red", "#ef4444"),
        new("Green", "#22c55e"),
        new("Blue", "#0ea5e9"),
        new("Purple", "#8b5cf6"),
    ];
}

public static class Palette
{
    /// <summary>Preset colours offered for marking document parts.</summary>
    public static IReadOnlyList<string> PartColours { get; } =
    [
        "#ef4444", "#f97316", "#f59e0b", "#22c55e", "#14b8a6",
        "#0ea5e9", "#6366f1", "#8b5cf6", "#ec4899", "#6b7280",
    ];
}
