using Aixaminator.Data;
using Aixaminator.Models;
using Aixaminator.Services;
using Aixaminator.Tests.Infrastructure;
using Aixaminator.ViewModels.Document;

namespace Aixaminator.Tests.ViewModels.Document;

/// <summary>Saving can fail (e.g. a full disk); the user is told instead of the app crashing.</summary>
public sealed class PersistenceFailureTests : IAsyncLifetime
{
    private DocumentFixture _f = null!;

    public async ValueTask InitializeAsync() => _f = await DocumentFixture.CreateAsync();

    public ValueTask DisposeAsync() => _f.DisposeAsync();

    private async Task<DocumentViewModel> OpenWithFailingWritesAsync()
    {
        var document = await _f.AddDocumentAsync("Doc", "Part zero.", "Part one.");
        var vm = new DocumentViewModel(document.Id, new FailingWritesRepository(_f.Repository), _f.Settings, _f.Ai, _f.Dialogs, _f.Navigation);
        await vm.LoadAsync();
        return vm;
    }

    [Fact]
    public async Task Failed_saves_are_shown_and_can_be_dismissed()
    {
        var vm = await OpenWithFailingWritesAsync();

        await vm.Parts[0].ToggleReadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Equal("The changes to Part 1 could not be saved: disk full", vm.ErrorMessage);

        vm.DismissErrorCommand.Execute(null);

        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task Notes_that_fail_to_save_are_not_shown()
    {
        var vm = await OpenWithFailingWritesAsync();
        var reader = Assert.IsType<ReaderViewModel>(vm.ModeContent);
        reader.SelectionStart = 0;
        reader.SelectionEnd = 4;

        await reader.HighlightCommand.ExecuteAsync(HighlightColour.All[0]);

        Assert.Empty(vm.Session!.Notes);
        Assert.Equal("Your note could not be saved: disk full", vm.ErrorMessage);
    }

    [Fact]
    public async Task Failed_edits_keep_the_editor_open()
    {
        var vm = await OpenWithFailingWritesAsync();
        vm.EditCommand.Execute(null);
        ((EditorViewModel)vm.ModeContent!).Text = "Changed";

        await vm.SaveEditsCommand.ExecuteAsync(null);

        Assert.Equal(DocumentMode.Edit, vm.Mode);
        Assert.Equal("Changed", ((EditorViewModel)vm.ModeContent!).Text);
        Assert.StartsWith("Your edits could not be saved", vm.ErrorMessage);
    }

    /// <summary>Reads from the real repository; every write fails.</summary>
    private sealed class FailingWritesRepository(IDocumentRepository inner) : IDocumentRepository
    {
        private static Task Fail() => Task.FromException(new IOException("disk full"));

        public Task<IReadOnlyList<DocumentSummary>> GetLibraryAsync(CancellationToken cancellationToken = default) => inner.GetLibraryAsync(cancellationToken);

        public Task<Data.Document?> GetAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetAsync(id, cancellationToken);

        public Task<string> GetPartTextAsync(Guid documentId, int partNumber, CancellationToken cancellationToken = default) =>
            inner.GetPartTextAsync(documentId, partNumber, cancellationToken);

        public Task<Data.Document> CreateAsync(NewDocument document, CancellationToken cancellationToken = default) =>
            Task.FromException<Data.Document>(new IOException("disk full"));

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromException<bool>(new IOException("disk full"));

        public Task UpdateDetailsAsync(Guid id, string name, string? description, CancellationToken cancellationToken = default) => Fail();

        public Task UpdatePartAsync(DocumentPart part, CancellationToken cancellationToken = default) => Fail();

        public Task<Note> AddNoteAsync(Note note, CancellationToken cancellationToken = default) => Task.FromException<Note>(new IOException("disk full"));

        public Task DeleteNoteAsync(int noteId, CancellationToken cancellationToken = default) => Fail();

        public Task SavePartTextsAsync(Guid documentId, IReadOnlyDictionary<int, string> texts, CancellationToken cancellationToken = default) => Fail();
    }
}
