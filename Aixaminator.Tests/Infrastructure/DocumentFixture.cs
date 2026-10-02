using Aixaminator.Data;
using Aixaminator.Services;
using Aixaminator.ViewModels.Document;

namespace Aixaminator.Tests.Infrastructure;

/// <summary>Database, settings and fakes needed to open a <see cref="DocumentViewModel"/>.</summary>
public sealed class DocumentFixture : IAsyncDisposable
{
    private DocumentFixture(TestDatabase database)
    {
        Database = database;
        Settings = new SettingsService(database.Paths);
    }

    public TestDatabase Database { get; }

    public IDocumentRepository Repository => Database.Repository;

    public SettingsService Settings { get; }

    public FakeAiConnection Ai { get; } = new();

    public FakeDialogService Dialogs { get; } = new();

    public FakeNavigationService Navigation { get; } = new();

    public static async Task<DocumentFixture> CreateAsync()
    {
        var fixture = new DocumentFixture(await TestDatabase.CreateAsync());
        await fixture.Settings.LoadAsync();
        return fixture;
    }

    public Task<Document> AddDocumentAsync(string name, params string[] parts) => Database.AddDocumentAsync(name, null, parts);

    public async Task<DocumentViewModel> OpenAsync(Guid documentId)
    {
        var vm = new DocumentViewModel(documentId, Repository, Settings, Ai, Dialogs, Navigation);
        await vm.LoadAsync();
        return vm;
    }

    public async Task<DocumentSession> CreateSessionAsync(Guid documentId) =>
        new((await Repository.GetAsync(documentId))!, Repository);

    public Task<Note> AddNoteAsync(Guid documentId, int part, NoteKind kind = NoteKind.Note, string text = "A note",
        HighlightLocation? location = null, string? colour = null, string? answer = null) =>
        Repository.AddNoteAsync(new Note
        {
            DocumentId = documentId,
            DocumentPartNumber = part,
            Kind = kind,
            Text = text,
            Location = location,
            HighlightColour = colour,
            Answer = answer,
        });

    public async Task MarkReadAsync(Document document, params int[] parts)
    {
        foreach (var part in parts)
        {
            document.Parts[part].IsRead = true;
            await Repository.UpdatePartAsync(document.Parts[part]);
        }
    }

    public ValueTask DisposeAsync()
    {
        Database.Dispose();
        return ValueTask.CompletedTask;
    }
}
