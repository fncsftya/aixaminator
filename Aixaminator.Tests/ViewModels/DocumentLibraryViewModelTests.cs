using Aixaminator.Tests.Infrastructure;
using Aixaminator.ViewModels.Dialogs;
using Aixaminator.ViewModels.Home;

namespace Aixaminator.Tests.ViewModels;

public sealed class DocumentLibraryViewModelTests : IAsyncLifetime
{
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeNavigationService _navigation = new();
    private TestDatabase _db = null!;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private DocumentLibraryViewModel CreateViewModel() => new(_db.Repository, _navigation, _dialogs);

    [Fact]
    public async Task Loading_lists_documents_with_their_progress()
    {
        var first = await _db.AddDocumentAsync("First", null, "a", "b");
        await _db.AddDocumentAsync("Second");
        first.Parts[0].IsRead = true;
        await _db.Repository.UpdatePartAsync(first.Parts[0]);
        var vm = CreateViewModel();

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(["First", "Second"], vm.Documents.Select(d => d.Name));
        Assert.Equal("50%", vm.Documents[0].ProgressText);
        Assert.False(vm.IsEmpty);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task An_empty_library_is_reported()
    {
        var vm = CreateViewModel();

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task Opening_a_document_navigates_to_it()
    {
        var document = await _db.AddDocumentAsync();
        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.Documents[0].OpenCommand.ExecuteAsync(null);

        Assert.Equal([document.Id], _navigation.OpenedDocuments);
    }

    [Fact]
    public async Task Deleting_a_document_asks_for_confirmation_first()
    {
        await _db.AddDocumentAsync("Keep me");
        _dialogs.Respond = _ => false;
        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.Documents[0].DeleteCommand.ExecuteAsync(null);

        var dialog = Assert.IsType<ConfirmationDialogViewModel>(Assert.Single(_dialogs.Shown));
        Assert.True(dialog.IsDestructive);
        Assert.Contains("Keep me", dialog.Message);
        Assert.Single(vm.Documents);
        Assert.Single(await _db.Repository.GetLibraryAsync());
    }

    [Fact]
    public async Task Confirmed_deletion_removes_the_document()
    {
        await _db.AddDocumentAsync("Delete me");
        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.Documents[0].DeleteCommand.ExecuteAsync(null);

        Assert.Empty(vm.Documents);
        Assert.True(vm.IsEmpty);
        Assert.Empty(await _db.Repository.GetLibraryAsync());
    }
}
