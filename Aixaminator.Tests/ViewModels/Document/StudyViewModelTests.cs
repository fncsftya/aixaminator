using Aixaminator.Data;
using Aixaminator.Tests.Infrastructure;
using Aixaminator.ViewModels.Document;

namespace Aixaminator.Tests.ViewModels.Document;

public sealed class StudyViewModelTests : IAsyncLifetime
{
    private DocumentFixture _f = null!;

    public async ValueTask InitializeAsync() => _f = await DocumentFixture.CreateAsync();

    public ValueTask DisposeAsync() => _f.DisposeAsync();

    private async Task<(DocumentViewModel Vm, StudyViewModel Study)> StudyAsync(Func<Guid, Task>? arrange = null)
    {
        var document = await _f.AddDocumentAsync("Doc", "Part zero.", "Part one.");
        if (arrange is not null)
        {
            await arrange(document.Id);
        }
        var vm = await _f.OpenAsync(document.Id);
        vm.StudyCommand.Execute(null);
        return (vm, Assert.IsType<StudyViewModel>(vm.ModeContent));
    }

    [Fact]
    public async Task Shows_the_notes_of_the_current_part_newest_first()
    {
        var (_, study) = await StudyAsync(async id =>
        {
            await _f.AddNoteAsync(id, 0, text: "First");
            await _f.AddNoteAsync(id, 1, text: "Other part");
            await _f.AddNoteAsync(id, 0, text: "Second");
        });

        Assert.Equal("Study notes for part 1", study.Heading);
        Assert.Equal(["Second", "First"], study.Notes.Select(n => n.Text));
        Assert.False(study.IsEmpty);
    }

    [Fact]
    public async Task Parts_without_notes_show_an_empty_message()
    {
        var (_, study) = await StudyAsync();

        Assert.True(study.IsEmpty);
        Assert.Equal("You don't have any notes for this part yet.", study.EmptyMessage);
    }

    [Fact]
    public async Task Selecting_another_part_shows_its_notes()
    {
        var (vm, study) = await StudyAsync(id => _f.AddNoteAsync(id, 1, text: "Part one note"));

        await vm.Parts[1].SelectCommand.ExecuteAsync(null);

        Assert.Equal("Study notes for part 2", study.Heading);
        Assert.Equal(["Part one note"], study.Notes.Select(n => n.Text));
    }

    [Fact]
    public async Task Show_all_lists_every_note_in_the_document()
    {
        var (_, study) = await StudyAsync(async id =>
        {
            await _f.AddNoteAsync(id, 0, text: "A");
            await _f.AddNoteAsync(id, 1, text: "B");
        });
        Assert.Equal("Show All", study.ToggleShowAllText);

        study.ToggleShowAllCommand.Execute(null);

        Assert.True(study.ShowAll);
        Assert.Equal("Study notes for document", study.Heading);
        Assert.Equal("Show Partial", study.ToggleShowAllText);
        Assert.Equal(["B", "A"], study.Notes.Select(n => n.Text));
        Assert.Equal(["Part 2", "Part 1"], study.Notes.Select(n => n.PartName));
    }

    [Fact]
    public async Task Notes_are_described_by_kind()
    {
        var (_, study) = await StudyAsync(async id =>
        {
            await _f.AddNoteAsync(id, 0, NoteKind.Note, "note");
            await _f.AddNoteAsync(id, 0, NoteKind.Question, "question?", answer: "answer");
            await _f.AddNoteAsync(id, 0, NoteKind.Highlight, "highlight", new HighlightLocation(0, 0, 0, 4), "#ef4444");
        });

        Assert.Equal(["Highlight", "Question", "Note"], study.Notes.Select(n => n.KindLabel));
        Assert.Equal("#ef4444", study.Notes[0].HighlightColour);
        Assert.True(study.Notes[1].IsQuestion);
    }

    [Fact]
    public async Task Answers_are_hidden_until_revealed_and_stay_revealed()
    {
        var (_, study) = await StudyAsync(id => _f.AddNoteAsync(id, 0, NoteKind.Question, "Why?", answer: "Because."));
        var question = study.Notes.Single();
        Assert.False(question.IsAnswerVisible);

        question.ToggleAnswerCommand.Execute(null);
        Assert.True(question.IsAnswerVisible);

        study.ToggleShowAllCommand.Execute(null);
        Assert.True(study.Notes.Single().IsAnswerVisible);

        study.Notes.Single().ToggleAnswerCommand.Execute(null);
        Assert.False(study.Notes.Single().IsAnswerVisible);
    }

    [Fact]
    public async Task Notes_can_be_deleted()
    {
        var (vm, study) = await StudyAsync(async id =>
        {
            await _f.AddNoteAsync(id, 0, text: "Keep");
            await _f.AddNoteAsync(id, 0, text: "Delete");
        });

        await study.Notes.Single(n => n.Text == "Delete").DeleteCommand.ExecuteAsync(null);

        Assert.Equal(["Keep"], study.Notes.Select(n => n.Text));
        Assert.Equal(1, vm.Parts[0].NoteCount);
        Assert.Equal(["Keep"], (await _f.Repository.GetAsync(vm.DocumentId))!.Notes.Select(n => n.Text));
    }
}
