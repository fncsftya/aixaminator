using Aixaminator.Services;
using Aixaminator.Tests.Infrastructure;
using Aixaminator.ViewModels.Home;

namespace Aixaminator.Tests.ViewModels;

public sealed class CreateDocumentViewModelTests : IAsyncLifetime
{
    private readonly FakeFilePicker _picker = new();
    private readonly FakeNavigationService _navigation = new();
    private readonly StubWikipedia _wikipedia = new();
    private TestDatabase _db = null!;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private CreateDocumentViewModel CreateViewModel() =>
        new(new DocumentImporter(_wikipedia), _db.Repository, _picker, _navigation);

    [Fact]
    public void Starts_on_the_file_source()
    {
        var vm = CreateViewModel();

        Assert.Equal(ImportSource.File, vm.Source);
        Assert.True(vm.IsFileSource);
        Assert.False(vm.IsWebSource);
        Assert.Equal(ImportStatus.NotStarted, vm.Status);
        Assert.Equal("No file chosen", vm.SelectedFileName);
    }

    [Fact]
    public async Task Browsing_selects_a_file()
    {
        _picker.File = new FakePickedFile("notes.txt", "Hello");
        var vm = CreateViewModel();

        await vm.BrowseCommand.ExecuteAsync(null);

        Assert.Equal("notes.txt", vm.SelectedFileName);
    }

    [Fact]
    public async Task Cancelling_the_picker_keeps_the_previous_file()
    {
        _picker.File = new FakePickedFile("notes.txt", "Hello");
        var vm = CreateViewModel();
        await vm.BrowseCommand.ExecuteAsync(null);

        _picker.File = null;
        await vm.BrowseCommand.ExecuteAsync(null);

        Assert.Equal("notes.txt", vm.SelectedFileName);
    }

    [Fact]
    public async Task Creating_without_a_file_shows_an_error()
    {
        var vm = CreateViewModel();

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Equal("No file selected.", vm.ErrorMessage);
        Assert.True(vm.HasError);
        Assert.Equal(ImportStatus.NotStarted, vm.Status);
    }

    [Fact]
    public async Task Creating_from_a_file_adds_a_single_part_document()
    {
        _picker.File = new FakePickedFile("My Book.txt", "Once upon a time.");
        var vm = CreateViewModel();
        await vm.BrowseCommand.ExecuteAsync(null);
        vm.Description = "A story";

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Equal(ImportStatus.Completed, vm.Status);
        Assert.Null(vm.ErrorMessage);
        var summary = Assert.Single(await _db.Repository.GetLibraryAsync());
        Assert.Equal("My Book", summary.Name);
        Assert.Equal("A story", summary.Description);
        Assert.Equal(summary.Id, vm.CreatedDocumentId);
        Assert.Equal("Once upon a time.", await _db.Repository.GetPartTextAsync(summary.Id, 0));
    }

    [Fact]
    public async Task The_created_document_can_be_opened()
    {
        _picker.File = new FakePickedFile("doc.txt", "text");
        var vm = CreateViewModel();
        await vm.BrowseCommand.ExecuteAsync(null);
        await vm.CreateCommand.ExecuteAsync(null);

        await vm.ViewDocumentCommand.ExecuteAsync(null);

        Assert.Equal([vm.CreatedDocumentId!.Value], _navigation.OpenedDocuments);
    }

    [Fact]
    public async Task Import_errors_are_shown_and_the_form_stays_usable()
    {
        _picker.File = new FakePickedFile("image.bin", [0x00, 0x01, 0x02]);
        var vm = CreateViewModel();
        await vm.BrowseCommand.ExecuteAsync(null);

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Contains("binary", vm.ErrorMessage);
        Assert.Equal(ImportStatus.NotStarted, vm.Status);
        Assert.True(vm.CreateCommand.CanExecute(null));
        Assert.Empty(await _db.Repository.GetLibraryAsync());
    }

    [Fact]
    public async Task Web_source_requires_a_url()
    {
        var vm = CreateViewModel();
        vm.IsWebSource = true;

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Equal(ImportSource.Web, vm.Source);
        Assert.Equal("No URL provided.", vm.ErrorMessage);
    }

    [Fact]
    public void Invalid_urls_are_flagged_while_typing()
    {
        var vm = CreateViewModel();
        vm.IsWebSource = true;

        vm.Url = "not a url";
        Assert.Equal("Please enter a valid URL.", vm.GetErrors(nameof(vm.Url)).Single().ErrorMessage);

        vm.Url = "https://en.wikipedia.org/wiki/Example";
        Assert.False(vm.HasErrors);

        vm.Url = "";
        Assert.False(vm.HasErrors);
    }

    [Fact]
    public async Task Non_wikipedia_urls_are_rejected()
    {
        var vm = CreateViewModel();
        vm.IsWebSource = true;
        vm.Url = "https://example.com/page";

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Equal("Only Wikipedia URLs are currently supported.", vm.ErrorMessage);
    }

    [Fact]
    public async Task Wikipedia_articles_can_be_imported()
    {
        _wikipedia.Text = "An article.";
        var vm = CreateViewModel();
        vm.IsWebSource = true;
        vm.Url = "https://en.wikipedia.org/wiki/Grace_Hopper";

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Equal(ImportStatus.Completed, vm.Status);
        Assert.Equal("Grace Hopper", Assert.Single(await _db.Repository.GetLibraryAsync()).Name);
    }

    [Fact]
    public async Task Switching_source_clears_the_error()
    {
        var vm = CreateViewModel();
        await vm.CreateCommand.ExecuteAsync(null);

        vm.IsWebSource = true;

        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Reset_starts_a_new_import()
    {
        _picker.File = new FakePickedFile("doc.txt", "text");
        var vm = CreateViewModel();
        await vm.BrowseCommand.ExecuteAsync(null);
        vm.Description = "desc";
        await vm.CreateCommand.ExecuteAsync(null);

        vm.ResetCommand.Execute(null);

        Assert.Equal(ImportStatus.NotStarted, vm.Status);
        Assert.Equal("No file chosen", vm.SelectedFileName);
        Assert.Equal(string.Empty, vm.Description);
        Assert.Null(vm.CreatedDocumentId);
    }

    private sealed class StubWikipedia : IWikipediaClient
    {
        public string Text { get; set; } = "text";

        public Task<string> GetTextAsync(string url, CancellationToken cancellationToken) => Task.FromResult(Text);
    }
}
