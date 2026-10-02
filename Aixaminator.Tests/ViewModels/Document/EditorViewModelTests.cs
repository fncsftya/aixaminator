using Aixaminator.Services;
using Aixaminator.Tests.Infrastructure;
using Aixaminator.ViewModels.Document;

namespace Aixaminator.Tests.ViewModels.Document;

public sealed class EditorViewModelTests : IAsyncLifetime
{
    private DocumentFixture _f = null!;

    public async ValueTask InitializeAsync() => _f = await DocumentFixture.CreateAsync();

    public ValueTask DisposeAsync() => _f.DisposeAsync();

    private async Task<(DocumentViewModel Vm, EditorViewModel Editor)> EditAsync()
    {
        var document = await _f.AddDocumentAsync("Doc", "Part zero.", "Part one.");
        var vm = await _f.OpenAsync(document.Id);
        vm.EditCommand.Execute(null);
        return (vm, Assert.IsType<EditorViewModel>(vm.ModeContent));
    }

    [Fact]
    public async Task Only_changed_parts_are_reported()
    {
        var (vm, editor) = await EditAsync();
        Assert.False(editor.HasChanges);

        await vm.Parts[1].SelectCommand.ExecuteAsync(null);
        editor.Text = "Changed one.";
        await vm.Parts[0].SelectCommand.ExecuteAsync(null);

        Assert.True(editor.HasChanges);
        Assert.Equal(new Dictionary<int, string> { [1] = "Changed one." }, editor.ChangedParts);
    }

    [Fact]
    public async Task Cleaning_replaces_the_text_with_the_cleaned_ai_output()
    {
        var (_, editor) = await EditAsync();
        editor.Text = "Noisy text";
        _f.Ai.Clean = _ => Task.FromResult("An extra-\nordinary page.\n42");
        Assert.Equal("Clean Text", editor.CleanButtonText);

        await editor.CleanCommand.ExecuteAsync(null);

        Assert.Equal(["Noisy text"], _f.Ai.CleanRequests);
        Assert.Equal("An extraordinary page.\n\n", editor.Text);
        Assert.False(editor.IsCleaning);
        Assert.Equal("Clean Text", editor.CleanButtonText);
    }

    [Fact]
    public async Task Cleaning_shows_progress_for_the_part()
    {
        var (_, editor) = await EditAsync();
        var release = new TaskCompletionSource<string>();
        _f.Ai.Clean = _ => release.Task;

        var cleaning = editor.CleanCommand.ExecuteAsync(null);

        Assert.True(editor.IsCleaning);
        Assert.Equal("Cleaning Part 1...", editor.CleanButtonText);
        Assert.False(editor.CleanCommand.CanExecute(null));

        release.SetResult("Clean");
        await cleaning;
        Assert.False(editor.IsCleaning);
    }

    [Fact]
    public async Task Cleaned_text_goes_to_the_part_it_was_requested_for()
    {
        var (vm, editor) = await EditAsync();
        var release = new TaskCompletionSource<string>();
        _f.Ai.Clean = _ => release.Task;

        var cleaning = editor.CleanCommand.ExecuteAsync(null);
        await vm.Parts[1].SelectCommand.ExecuteAsync(null);
        release.SetResult("Cleaned zero.");
        await cleaning;

        Assert.Equal("Part one.", editor.Text);
        Assert.Equal("Cleaned zero.\n", editor.ChangedParts[0]);
    }

    [Fact]
    public async Task Failed_cleaning_is_reported_until_another_part_is_shown()
    {
        var (vm, editor) = await EditAsync();
        _f.Ai.Clean = _ => throw new AiNotConfiguredException();

        await editor.CleanCommand.ExecuteAsync(null);

        Assert.True(editor.CleaningFailed);
        Assert.Equal("Failed", editor.CleanButtonText);
        Assert.Equal("No AI provider is configured. Add one in Settings.", editor.CleaningError);
        Assert.False(editor.CleanCommand.CanExecute(null));
        Assert.Equal("Part zero.", editor.Text);

        await vm.Parts[1].SelectCommand.ExecuteAsync(null);

        Assert.False(editor.CleaningFailed);
        Assert.True(editor.CleanCommand.CanExecute(null));
    }
}
