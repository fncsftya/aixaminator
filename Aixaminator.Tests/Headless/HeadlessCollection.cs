namespace Aixaminator.Tests.Headless;

/// <summary>
/// Headless tests share one UI thread and global state (e.g. <see cref="BindingErrors"/>), so they run one at a time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HeadlessCollection
{
    public const string Name = "Headless UI";
}
