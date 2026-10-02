using Aixaminator.Data;
using Aixaminator.Features;
using Aixaminator.Models;
using Aixaminator.Tests.Infrastructure;
using Aixaminator.ViewModels.Dialogs;
using Aixaminator.ViewModels.Document;

namespace Aixaminator.Tests.ViewModels.Document;

public sealed class ReaderViewModelTests : IAsyncLifetime
{
    private const string Text = "Hello world.\n\nSecond paragraph.";
    private DocumentFixture _f = null!;

    public async ValueTask InitializeAsync() => _f = await DocumentFixture.CreateAsync();

    public ValueTask DisposeAsync() => _f.DisposeAsync();

    private async Task<(DocumentViewModel Vm, ReaderViewModel Reader)> OpenAsync(string text = Text)
    {
        var document = await _f.AddDocumentAsync("Doc", text);
        var vm = await _f.OpenAsync(document.Id);
        return (vm, Assert.IsType<ReaderViewModel>(vm.ModeContent));
    }

    [Fact]
    public async Task Shows_the_paragraphs_of_the_part()
    {
        var (_, reader) = await OpenAsync();

        Assert.True(reader.HasPart);
        Assert.False(reader.IsEmpty);
        Assert.Equal(["Hello world.", "Second paragraph."], reader.Content.Paragraphs.Select(p => p.Text));
    }

    [Fact]
    public async Task Parts_without_text_are_reported_as_empty()
    {
        var (_, reader) = await OpenAsync("   ");

        Assert.True(reader.IsEmpty);
    }

    [Fact]
    public async Task Existing_highlights_are_shown()
    {
        var document = await _f.AddDocumentAsync("Doc", Text);
        await _f.AddNoteAsync(document.Id, 0, NoteKind.Highlight, "world", new HighlightLocation(0, 6, 0, 11), "#22c55e");

        var vm = await _f.OpenAsync(document.Id);

        var reader = Assert.IsType<ReaderViewModel>(vm.ModeContent);
        Assert.Contains(new ReaderSegment("world", "#22c55e"), reader.Content.Paragraphs[0].Segments);
    }

    [Fact]
    public async Task Highlight_options_appear_only_with_a_selection()
    {
        var (_, reader) = await OpenAsync();
        Assert.False(reader.HasSelection);
        Assert.Equal(HighlightColour.All, reader.HighlightColours);

        reader.SelectionStart = 6;
        reader.SelectionEnd = 11;

        Assert.True(reader.HasSelection);
        Assert.Equal("world", reader.SelectedText);

        // only a paragraph separator selected
        reader.SelectionStart = 12;
        reader.SelectionEnd = 14;
        Assert.False(reader.HasSelection);
    }

    [Fact]
    public async Task Highlighting_saves_a_note_and_shows_it()
    {
        var (vm, reader) = await OpenAsync();
        reader.SelectionStart = 20;
        reader.SelectionEnd = 6;
        var colour = HighlightColour.All[1];

        await reader.HighlightCommand.ExecuteAsync(colour);

        var note = Assert.Single((await _f.Repository.GetAsync(vm.DocumentId))!.Notes);
        Assert.Equal(NoteKind.Highlight, note.Kind);
        Assert.Equal("world.\n\nSecond", note.Text);
        Assert.Equal(colour.Hex, note.HighlightColour);
        Assert.Equal(new HighlightLocation(0, 6, 1, 6), note.Location);
        Assert.Equal(0, note.DocumentPartNumber);
        Assert.Contains(new ReaderSegment("Second", colour.Hex), reader.Content.Paragraphs[1].Segments);
        Assert.False(reader.HasSelection);
    }

    [Fact]
    public async Task Highlighting_without_a_selection_does_nothing()
    {
        var (vm, reader) = await OpenAsync();

        await reader.HighlightCommand.ExecuteAsync(HighlightColour.All[0]);

        Assert.Empty((await _f.Repository.GetAsync(vm.DocumentId))!.Notes);
    }

    [Fact]
    public async Task Adding_a_note_about_the_selection()
    {
        var (vm, reader) = await OpenAsync();
        reader.SelectionStart = 0;
        reader.SelectionEnd = 5;
        _f.Dialogs.Respond = _ => new NoteInput("Greeting", null);

        await reader.AddNoteCommand.ExecuteAsync(null);

        var dialog = Assert.IsType<AddNoteDialogViewModel>(Assert.Single(_f.Dialogs.Shown));
        Assert.False(dialog.IsQuestion);
        Assert.Equal("Hello", dialog.ReferenceText);
        var note = Assert.Single((await _f.Repository.GetAsync(vm.DocumentId))!.Notes);
        Assert.Equal(NoteKind.Note, note.Kind);
        Assert.Equal("Greeting", note.Text);
        Assert.Equal("Hello", note.Context);
        Assert.Equal(new HighlightLocation(0, 0, 0, 5), note.Location);
        Assert.Equal(1, vm.Parts[0].NoteCount);
        Assert.False(reader.HasSelection);
    }

    [Fact]
    public async Task Adding_a_question_without_a_selection()
    {
        var (vm, reader) = await OpenAsync();
        _f.Dialogs.Respond = _ => new NoteInput("Why?", "Because.");

        await reader.AddQuestionCommand.ExecuteAsync(null);

        var dialog = Assert.IsType<AddNoteDialogViewModel>(Assert.Single(_f.Dialogs.Shown));
        Assert.True(dialog.IsQuestion);
        Assert.Null(dialog.ReferenceText);
        var note = Assert.Single((await _f.Repository.GetAsync(vm.DocumentId))!.Notes);
        Assert.Equal(NoteKind.Question, note.Kind);
        Assert.Equal("Why?", note.Text);
        Assert.Equal("Because.", note.Answer);
        Assert.Null(note.Context);
        Assert.Null(note.Location);
    }

    [Fact]
    public async Task Cancelling_the_note_dialog_saves_nothing()
    {
        var (vm, reader) = await OpenAsync();
        _f.Dialogs.Respond = _ => null;

        await reader.AddNoteCommand.ExecuteAsync(null);

        Assert.Empty((await _f.Repository.GetAsync(vm.DocumentId))!.Notes);
    }

    [Fact]
    public async Task Navigation_commands_are_the_documents()
    {
        var (vm, reader) = await OpenAsync();

        Assert.Same(vm.PreviousPartCommand, reader.PreviousCommand);
        Assert.Same(vm.NextPartCommand, reader.NextCommand);
    }
}
