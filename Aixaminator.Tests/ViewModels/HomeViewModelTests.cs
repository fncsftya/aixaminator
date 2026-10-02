using Aixaminator.Services;
using Aixaminator.Tests.Infrastructure;
using Aixaminator.ViewModels.Home;

namespace Aixaminator.Tests.ViewModels;

public sealed class HomeViewModelTests : IAsyncLifetime
{
    private readonly FakeFilePicker _picker = new();
    private TestDatabase _db = null!;
    private HomeViewModel _home = null!;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync();
        var navigation = new FakeNavigationService();
        var settings = new SettingsService(_db.Paths);
        _home = new HomeViewModel(
            new DocumentLibraryViewModel(_db.Repository, navigation, new FakeDialogService()),
            new CreateDocumentViewModel(new DocumentImporter(new WikipediaClient()), _db.Repository, _picker, navigation),
            new SettingsViewModel(settings, new FakeAiConnection()));
        await _home.LoadAsync();
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public void Starts_on_my_documents()
    {
        Assert.Equal(HomeTab.MyDocuments, _home.SelectedTab);
        Assert.Equal(0, _home.SelectedTabIndex);
    }

    [Fact]
    public async Task The_tab_index_and_tab_stay_in_sync()
    {
        var changed = new List<string?>();
        _home.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _home.SelectedTabIndex = 2;

        Assert.Equal(HomeTab.Settings, _home.SelectedTab);
        Assert.Contains(nameof(HomeViewModel.SelectedTabIndex), changed);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Returning_to_my_documents_shows_newly_added_documents()
    {
        _picker.File = new FakePickedFile("New.txt", "Fresh text.");
        _home.SelectedTab = HomeTab.AddDocument;
        await _home.CreateDocument.BrowseCommand.ExecuteAsync(null);
        await _home.CreateDocument.CreateCommand.ExecuteAsync(null);

        _home.SelectedTab = HomeTab.MyDocuments;
        await _home.Library.LoadCommand.ExecutionTask!;

        Assert.Equal(["New"], _home.Library.Documents.Select(d => d.Name));
    }

    [Fact]
    public async Task Returning_to_add_document_after_an_import_starts_a_new_one()
    {
        _picker.File = new FakePickedFile("New.txt", "Fresh text.");
        _home.SelectedTab = HomeTab.AddDocument;
        await _home.CreateDocument.BrowseCommand.ExecuteAsync(null);
        await _home.CreateDocument.CreateCommand.ExecuteAsync(null);
        Assert.Equal(ImportStatus.Completed, _home.CreateDocument.Status);

        _home.SelectedTab = HomeTab.MyDocuments;
        _home.SelectedTab = HomeTab.AddDocument;

        Assert.Equal(ImportStatus.NotStarted, _home.CreateDocument.Status);
    }
}
