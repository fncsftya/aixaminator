using Aixaminator.Data;
using Aixaminator.Features;
using Aixaminator.Models;
using Aixaminator.Services;
using Aixaminator.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aixaminator.ViewModels.Document;

/// <summary>
/// Read mode: shows the text of the current part with its highlights. Selecting text allows it to be
/// highlighted or used as the reference for a note or question.
/// </summary>
public sealed partial class ReaderViewModel : ViewModelBase, IPartContent, IDisposable
{
    private readonly DocumentSession _session;
    private readonly IDialogService _dialogs;
    private string _text = string.Empty;

    public ReaderViewModel(
        DocumentSession session,
        IDialogService dialogs,
        ReaderAppearance appearance,
        IAsyncRelayCommand previousCommand,
        IAsyncRelayCommand nextCommand)
    {
        _session = session;
        _dialogs = dialogs;
        Appearance = appearance;
        PreviousCommand = previousCommand;
        NextCommand = nextCommand;
        _session.NotesChanged += OnNotesChanged;
    }

    public ReaderAppearance Appearance { get; }

    public IAsyncRelayCommand PreviousCommand { get; }

    public IAsyncRelayCommand NextCommand { get; }

    public IReadOnlyList<HighlightColour> HighlightColours => HighlightColour.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPart), nameof(IsEmpty))]
    [NotifyCanExecuteChangedFor(nameof(AddNoteCommand), nameof(AddQuestionCommand))]
    public partial int? PartNumber { get; private set; }

    public bool HasPart => PartNumber is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(HasSelection), nameof(SelectedText))]
    public partial ReaderContent Content { get; private set; } = ReaderContent.Empty;

    /// <summary>The part has no text to show.</summary>
    public bool IsEmpty => HasPart && Content.IsEmpty;

    /// <summary>Selection within <see cref="ReaderContent.Text"/>; bound two-way to the text control.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(SelectedText))]
    public partial int SelectionStart { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(SelectedText))]
    public partial int SelectionEnd { get; set; }

    /// <summary>Whether the selection covers some text that can be highlighted.</summary>
    public bool HasSelection => SelectionLocation is not null;

    public string SelectedText => HasSelection ? Content.GetText(SelectionStart, SelectionEnd) : string.Empty;

    private HighlightLocation? SelectionLocation => Content.GetLocation(SelectionStart, SelectionEnd);

    public void ShowPart(int? partNumber, string text)
    {
        _text = text;
        PartNumber = partNumber;
        Rebuild();
    }

    [RelayCommand]
    private async Task HighlightAsync(HighlightColour colour)
    {
        if (PartNumber is not { } part || SelectionLocation is not { } location)
        {
            return;
        }

        var text = SelectedText;
        ClearSelection();
        await _session.AddNoteAsync(new Note
        {
            DocumentPartNumber = part,
            Kind = NoteKind.Highlight,
            Text = text,
            HighlightColour = colour.Hex,
            Location = location,
        });
    }

    [RelayCommand(CanExecute = nameof(HasPart))]
    private Task AddNoteAsync() => AddAsync(isQuestion: false);

    [RelayCommand(CanExecute = nameof(HasPart))]
    private Task AddQuestionAsync() => AddAsync(isQuestion: true);

    private async Task AddAsync(bool isQuestion)
    {
        if (PartNumber is not { } part)
        {
            return;
        }

        // Capture the selection now; it may change while the dialog is open.
        var location = SelectionLocation;
        var context = location is null ? null : SelectedText;

        var input = await _dialogs.ShowAsync(new AddNoteDialogViewModel(isQuestion, context));
        if (input is null)
        {
            return;
        }

        ClearSelection();
        await _session.AddNoteAsync(new Note
        {
            DocumentPartNumber = part,
            Kind = isQuestion ? NoteKind.Question : NoteKind.Note,
            Text = input.Text,
            Answer = input.Answer,
            Context = context,
            Location = location,
        });
    }

    private void ClearSelection()
    {
        SelectionStart = 0;
        SelectionEnd = 0;
    }

    private void Rebuild()
    {
        ClearSelection();
        Content = PartNumber is { } part
            ? ReaderContent.Create(_text, _session.NotesForPart(part))
            : ReaderContent.Empty;
    }

    private void OnNotesChanged(object? sender, EventArgs e) => Rebuild();

    public void Dispose() => _session.NotesChanged -= OnNotesChanged;
}
