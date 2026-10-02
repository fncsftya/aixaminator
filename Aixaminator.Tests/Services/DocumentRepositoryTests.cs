using Aixaminator.Data;
using Aixaminator.Services;
using Aixaminator.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Aixaminator.Tests.Services;

public sealed class DocumentRepositoryTests : IAsyncLifetime
{
    private TestDatabase _db = null!;

    private DocumentRepository Repository => _db.Repository;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task The_migrations_match_the_model()
    {
        await using var context = await _db.ContextFactory.CreateDbContextAsync(Token);

        Assert.False(context.Database.HasPendingModelChanges(), "Run 'dotnet ef migrations add' to update the migrations.");
    }

    [Fact]
    public async Task Creating_a_document_stores_its_parts()
    {
        var created = await Repository.CreateAsync(new NewDocument("Book", "About things", ["Chapter one.", "Chapter two."]), Token);

        var document = await Repository.GetAsync(created.Id, Token);

        Assert.NotNull(document);
        Assert.Equal("Book", document.Name);
        Assert.Equal("About things", document.Description);
        Assert.Equal([0, 1], document.Parts.Select(p => p.PartNumber));
        Assert.All(document.Parts, p => Assert.False(p.IsRead || p.Hidden));
        Assert.Equal("Chapter two.", await Repository.GetPartTextAsync(created.Id, 1, Token));
    }

    [Fact]
    public async Task A_document_needs_at_least_one_part()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Repository.CreateAsync(new NewDocument("Empty", null, []), Token));
    }

    [Fact]
    public async Task Blank_descriptions_are_stored_as_null()
    {
        var created = await Repository.CreateAsync(new NewDocument("Book", "   ", ["text"]), Token);

        Assert.Null((await Repository.GetAsync(created.Id, Token))!.Description);
    }

    [Fact]
    public async Task Getting_an_unknown_document_returns_null()
    {
        Assert.Null(await Repository.GetAsync(Guid.NewGuid(), Token));
    }

    [Fact]
    public async Task Library_lists_documents_in_creation_order_with_progress()
    {
        var first = await _db.AddDocumentAsync("First", null, "a", "b", "c");
        await _db.AddDocumentAsync("Second");
        var part = first.Parts[0];
        part.IsRead = true;
        await Repository.UpdatePartAsync(part, Token);
        var hidden = first.Parts[2];
        hidden.Hidden = true;
        await Repository.UpdatePartAsync(hidden, Token);

        var library = await Repository.GetLibraryAsync(Token);

        Assert.Equal(["First", "Second"], library.Select(d => d.Name));
        Assert.Equal(2, library[0].PartCount);
        Assert.Equal(1, library[0].ReadCount);
        Assert.Equal(50, library[0].Progress);
        Assert.Equal(0, library[1].Progress);
    }

    [Fact]
    public async Task Deleting_a_document_removes_its_parts_notes_and_content()
    {
        var document = await _db.AddDocumentAsync();
        await Repository.AddNoteAsync(new Note { DocumentId = document.Id, DocumentPartNumber = 0, Text = "note" }, Token);

        Assert.True(await Repository.DeleteAsync(document.Id, Token));

        Assert.Null(await Repository.GetAsync(document.Id, Token));
        Assert.False(Directory.Exists(_db.Paths.GetDocumentDirectory(document.Id)));
        await using var context = await _db.ContextFactory.CreateDbContextAsync(Token);
        Assert.Equal(0, await context.Notes.CountAsync(Token));
        Assert.Equal(0, await context.DocumentParts.CountAsync(Token));
        Assert.False(await Repository.DeleteAsync(document.Id, Token));
    }

    [Fact]
    public async Task Details_can_be_updated()
    {
        var document = await _db.AddDocumentAsync();

        await Repository.UpdateDetailsAsync(document.Id, "Renamed", "New description", Token);

        var reloaded = await Repository.GetAsync(document.Id, Token);
        Assert.Equal("Renamed", reloaded!.Name);
        Assert.Equal("New description", reloaded.Description);
    }

    [Fact]
    public async Task Part_settings_can_be_updated()
    {
        var document = await _db.AddDocumentAsync("Doc", null, "a", "b");
        var part = document.Parts[1];
        part.Name = "Introduction";
        part.UseColour = true;
        part.Colour = "#ff0000";
        part.Hidden = true;
        part.IsRead = true;

        await Repository.UpdatePartAsync(part, Token);

        var reloaded = (await Repository.GetAsync(document.Id, Token))!.Parts[1];
        Assert.Equal("Introduction", reloaded.Name);
        Assert.True(reloaded.UseColour);
        Assert.Equal("#ff0000", reloaded.Colour);
        Assert.True(reloaded.Hidden);
        Assert.True(reloaded.IsRead);
        Assert.Equal("Introduction", reloaded.DisplayName);
    }

    [Fact]
    public async Task Notes_of_every_kind_round_trip()
    {
        var document = await _db.AddDocumentAsync();
        var location = new HighlightLocation(0, 1, 2, 3);

        var highlight = await Repository.AddNoteAsync(new Note
        {
            DocumentId = document.Id, DocumentPartNumber = 0, Kind = NoteKind.Highlight,
            Text = "highlighted", HighlightColour = "#f59e0b", Location = location,
        }, Token);
        await Repository.AddNoteAsync(new Note
        {
            DocumentId = document.Id, DocumentPartNumber = 0, Kind = NoteKind.Question,
            Text = "Why?", Answer = "Because.", Context = "some context",
        }, Token);
        await Repository.AddNoteAsync(new Note { DocumentId = document.Id, Kind = NoteKind.Note, Text = "General note" }, Token);

        var notes = (await Repository.GetAsync(document.Id, Token))!.Notes;

        Assert.True(highlight.Id > 0);
        Assert.Equal([NoteKind.Highlight, NoteKind.Question, NoteKind.Note], notes.Select(n => n.Kind));
        Assert.Equal(location, notes[0].Location);
        Assert.Equal("#f59e0b", notes[0].HighlightColour);
        Assert.Equal("Because.", notes[1].Answer);
        Assert.Equal("some context", notes[1].Context);
        Assert.Null(notes[1].Location);
        Assert.Null(notes[2].DocumentPartNumber);
    }

    [Fact]
    public async Task Notes_can_be_deleted()
    {
        var document = await _db.AddDocumentAsync();
        var note = await Repository.AddNoteAsync(new Note { DocumentId = document.Id, DocumentPartNumber = 0, Text = "note" }, Token);

        await Repository.DeleteNoteAsync(note.Id, Token);

        Assert.Empty((await Repository.GetAsync(document.Id, Token))!.Notes);
    }

    [Fact]
    public async Task Saving_part_texts_updates_content_and_clears_stale_locations_in_those_parts()
    {
        var document = await _db.AddDocumentAsync("Doc", null, "Part zero.", "Part one.");
        var location = new HighlightLocation(0, 0, 0, 4);
        await Repository.AddNoteAsync(new Note
        {
            DocumentId = document.Id, DocumentPartNumber = 0, Kind = NoteKind.Highlight, Text = "Part", HighlightColour = "#f59e0b", Location = location,
        }, Token);
        await Repository.AddNoteAsync(new Note
        {
            DocumentId = document.Id, DocumentPartNumber = 1, Kind = NoteKind.Highlight, Text = "Part", HighlightColour = "#f59e0b", Location = location,
        }, Token);

        await Repository.SavePartTextsAsync(document.Id, new Dictionary<int, string> { [0] = "Edited part zero." }, Token);

        Assert.Equal("Edited part zero.", await Repository.GetPartTextAsync(document.Id, 0, Token));
        var notes = (await Repository.GetAsync(document.Id, Token))!.Notes;
        Assert.Null(notes.Single(n => n.DocumentPartNumber == 0).Location);
        Assert.Equal(location, notes.Single(n => n.DocumentPartNumber == 1).Location);
    }
}
