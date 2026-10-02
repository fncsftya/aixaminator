using System.Collections.ObjectModel;
using Aixaminator.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aixaminator.ViewModels.Document;

/// <summary>Study mode: reviews the notes, questions and highlights of the current part or the whole document.</summary>
public sealed partial class StudyViewModel : ViewModelBase, IPartContent, IDisposable
{
    private readonly DocumentSession _session;
    private readonly HashSet<int> _revealedAnswers = [];

    public StudyViewModel(DocumentSession session)
    {
        _session = session;
        _session.NotesChanged += OnNotesChanged;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Heading))]
    public partial int? PartNumber { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Heading), nameof(ToggleShowAllText), nameof(EmptyMessage))]
    public partial bool ShowAll { get; private set; }

    public string Heading => ShowAll || PartNumber is null ? "Study notes for document" : $"Study notes for part {PartNumber + 1}";

    public string ToggleShowAllText => ShowAll ? "Show Partial" : "Show All";

    public ObservableCollection<StudyNoteViewModel> Notes { get; } = [];

    public bool IsEmpty => Notes.Count == 0;

    public string EmptyMessage => ShowAll
        ? "You don't have any notes for this document yet."
        : "You don't have any notes for this part yet.";

    public void ShowPart(int? partNumber, string text)
    {
        PartNumber = partNumber;
        Rebuild();
    }

    [RelayCommand]
    private void ToggleShowAll()
    {
        ShowAll = !ShowAll;
        Rebuild();
    }

    internal bool IsAnswerRevealed(int noteId) => _revealedAnswers.Contains(noteId);

    internal void SetAnswerRevealed(int noteId, bool revealed)
    {
        if (revealed)
        {
            _revealedAnswers.Add(noteId);
        }
        else
        {
            _revealedAnswers.Remove(noteId);
        }
    }

    internal Task DeleteAsync(StudyNoteViewModel note) => _session.DeleteNoteAsync(note.Note);

    private void Rebuild()
    {
        var notes = ShowAll
            ? _session.Notes
            : PartNumber is { } part ? _session.NotesForPart(part) : [];

        Notes.Clear();
        foreach (var note in notes.OrderByDescending(n => n.Id))
        {
            Notes.Add(new StudyNoteViewModel(this, note, _session.GetPartName(note.DocumentPartNumber)));
        }
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void OnNotesChanged(object? sender, EventArgs e) => Rebuild();

    public void Dispose() => _session.NotesChanged -= OnNotesChanged;
}

public sealed partial class StudyNoteViewModel : ViewModelBase
{
    private readonly StudyViewModel _owner;

    internal StudyNoteViewModel(StudyViewModel owner, Note note, string partName)
    {
        _owner = owner;
        Note = note;
        PartName = partName;
        IsAnswerVisible = owner.IsAnswerRevealed(note.Id);
    }

    internal Note Note { get; }

    public NoteKind Kind => Note.Kind;

    public string KindLabel => Kind switch
    {
        NoteKind.Question => "Question",
        NoteKind.Highlight => "Highlight",
        _ => "Note",
    };

    public bool IsQuestion => Kind == NoteKind.Question;

    public bool IsHighlight => Kind == NoteKind.Highlight;

    public bool IsPlainNote => Kind == NoteKind.Note;

    public string? HighlightColour => Note.HighlightColour;

    /// <summary>Text that was selected when the note or question was created.</summary>
    public string? Context => Note.Context;

    public bool HasContext => !string.IsNullOrWhiteSpace(Context);

    public string Text => Note.Text;

    public string? Answer => Note.Answer;

    public string PartName { get; }

    [ObservableProperty]
    public partial bool IsAnswerVisible { get; private set; }

    [RelayCommand]
    private void ToggleAnswer()
    {
        IsAnswerVisible = !IsAnswerVisible;
        _owner.SetAnswerRevealed(Note.Id, IsAnswerVisible);
    }

    [RelayCommand]
    private Task DeleteAsync() => _owner.DeleteAsync(this);
}
