using System.Collections.ObjectModel;
using Aixaminator.Models;
using Aixaminator.Services;
using Aixaminator.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aixaminator.ViewModels.Document;

public enum DocumentMode
{
    Read,
    Edit,
    Study,
    Configure,
    Recap,
}

/// <summary>
/// The document page: a sidebar listing the document's parts, and content that depends on the <see cref="Mode"/>.
/// </summary>
public sealed partial class DocumentViewModel : PageViewModel, IDisposable
{
    private readonly IDocumentRepository _repository;
    private readonly ISettingsService _settings;
    private readonly IAiConnection _ai;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private string _currentText = string.Empty;
    private int _partRequest;

    public DocumentViewModel(
        Guid documentId,
        IDocumentRepository repository,
        ISettingsService settings,
        IAiConnection ai,
        IDialogService dialogs,
        INavigationService navigation)
    {
        DocumentId = documentId;
        _repository = repository;
        _settings = settings;
        _ai = ai;
        _dialogs = dialogs;
        _navigation = navigation;
    }

    public Guid DocumentId { get; }

    /// <summary>Available once loaded.</summary>
    public DocumentSession? Session { get; private set; }

    public ReaderAppearance Appearance { get; private set; } = ReaderAppearance.Default;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    public partial bool IsLoading { get; private set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    public partial bool LoadFailed { get; private set; }

    /// <summary>The document has loaded and can be shown.</summary>
    public bool IsReady => !IsLoading && !LoadFailed;

    [ObservableProperty]
    public partial string Name { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial int Progress { get; private set; }

    public string ProgressText => $"{Progress}%";

    /// <summary>A problem loading or saving, shown as a dismissible banner.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; private set; }

    public bool HasError => ErrorMessage is not null;

    /// <summary>The visible parts of the document.</summary>
    public ObservableCollection<PartItemViewModel> Parts { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousPartCommand), nameof(NextPartCommand), nameof(EditCommand))]
    public partial int? CurrentPartNumber { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(ShowStudyButton), nameof(ShowReadButton),
        nameof(ShowEditActions), nameof(ShowConfigureActions), nameof(ShowDefaultActions), nameof(ShowCloseModeButton))]
    [NotifyCanExecuteChangedFor(
        nameof(StudyCommand), nameof(ToggleRecapCommand), nameof(EditCommand),
        nameof(PreviousPartCommand), nameof(NextPartCommand))]
    public partial DocumentMode Mode { get; private set; }

    /// <summary>The view model for the current mode.</summary>
    [ObservableProperty]
    public partial ViewModelBase? ModeContent { get; private set; }

    // Header: "Study" while reading/editing, "Read" to return from other modes.
    public bool ShowStudyButton => Mode is DocumentMode.Read or DocumentMode.Edit;

    public bool ShowReadButton => !ShowStudyButton;

    // Footer actions
    public bool ShowEditActions => Mode == DocumentMode.Edit;

    public bool ShowConfigureActions => Mode == DocumentMode.Configure;

    public bool ShowDefaultActions => Mode is DocumentMode.Read or DocumentMode.Study or DocumentMode.Recap;

    /// <summary>Configure and recap mode have a "Close" button below their content that returns to reading.</summary>
    public bool ShowCloseModeButton => Mode is DocumentMode.Configure or DocumentMode.Recap;

    public override async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var document = await _repository.GetAsync(DocumentId);
            if (document is null)
            {
                LoadFailed = true;
                return;
            }

            Session = new DocumentSession(document, _repository);
            Session.DetailsChanged += OnDetailsChanged;
            Session.PartsChanged += OnPartsChanged;
            Session.NotesChanged += OnNotesChanged;
            Session.ErrorOccurred += OnErrorOccurred;

            Appearance = _settings.Settings.ToReaderAppearance();
            Name = Session.Name;
            SyncParts();
            SetMode(DocumentMode.Read);

            var visible = Session.VisibleParts;
            var initial = visible.FirstOrDefault(p => !p.IsRead) ?? visible.FirstOrDefault();
            if (initial is not null)
            {
                await ShowPartAsync(initial.PartNumber);
            }
        }
        catch (Exception ex)
        {
            LoadFailed = true;
            ErrorMessage = $"The document could not be loaded: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ----- Parts -----

    /// <summary>Shows a part in the current mode, without any prompts.</summary>
    public async Task ShowPartAsync(int partNumber)
    {
        if (Session is null)
        {
            return;
        }

        // Ignore stale results if another part was requested while loading.
        var request = ++_partRequest;
        var text = await Session.GetPartTextAsync(partNumber);
        if (request != _partRequest)
        {
            return;
        }

        SetCurrentPart(partNumber, text);
    }

    private void SetCurrentPart(int? partNumber, string text)
    {
        CurrentPartNumber = partNumber;
        _currentText = text;
        foreach (var item in Parts)
        {
            item.Refresh();
        }
        (ModeContent as IPartContent)?.ShowPart(partNumber, text);
    }

    private PartItemViewModel? AdjacentPart(int direction)
    {
        var index = Parts.ToList().FindIndex(p => p.PartNumber == CurrentPartNumber);
        var target = index + direction;
        return index < 0 || target < 0 || target >= Parts.Count ? null : Parts[target];
    }

    private bool CanGoToPreviousPart() => Mode == DocumentMode.Read && AdjacentPart(-1) is not null;

    private bool CanGoToNextPart() => Mode == DocumentMode.Read && AdjacentPart(+1) is not null;

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPart))]
    private async Task PreviousPartAsync()
    {
        if (AdjacentPart(-1) is not { } previous || !await ConfirmLeavingCurrentPartAsync())
        {
            return;
        }

        await ShowPartAsync(previous.PartNumber);
    }

    /// <summary>Moves to the next part, marking the current one as read.</summary>
    [RelayCommand(CanExecute = nameof(CanGoToNextPart))]
    private async Task NextPartAsync()
    {
        if (AdjacentPart(+1) is not { } next || !await ConfirmLeavingCurrentPartAsync())
        {
            return;
        }

        if (CurrentPartNumber is { } current && Session!.GetPart(current) is { } part)
        {
            await Session.SetPartReadAsync(part, true);
        }
        await ShowPartAsync(next.PartNumber);
    }

    private async Task<bool> ConfirmLeavingCurrentPartAsync() =>
        CurrentPartNumber is not { } current || Session!.HasNotes(current) || await ConfirmWithoutNotesAsync();

    private Task<bool> ConfirmWithoutNotesAsync() =>
        _dialogs.ConfirmAsync(new ConfirmationDialogViewModel(
            "No notes taken",
            "You haven't taken any notes for this section. Would you like to continue without adding notes?"));

    internal async Task ToggleReadAsync(PartItemViewModel item)
    {
        var markingRead = !item.IsRead;
        if (markingRead && !Session!.HasNotes(item.PartNumber) && !await ConfirmWithoutNotesAsync())
        {
            return;
        }

        await Session!.SetPartReadAsync(item.Part, markingRead);
    }

    internal void TogglePartSettings(PartItemViewModel item)
    {
        var open = !item.IsSettingsOpen;
        foreach (var other in Parts)
        {
            other.CloseSettings();
        }
        if (open)
        {
            item.OpenSettings();
        }
    }

    /// <summary>Brings the sidebar in line with the session's visible parts, keeping existing items.</summary>
    private void SyncParts()
    {
        var visible = Session!.VisibleParts;
        foreach (var item in Parts.Where(i => !visible.Contains(i.Part)).ToList())
        {
            Parts.Remove(item);
        }

        for (var i = 0; i < visible.Count; i++)
        {
            if (i >= Parts.Count || Parts[i].Part != visible[i])
            {
                Parts.Insert(i, new PartItemViewModel(this, visible[i]));
            }
        }

        foreach (var item in Parts)
        {
            item.Refresh();
        }

        Progress = Session.Progress;
        PreviousPartCommand.NotifyCanExecuteChanged();
        NextPartCommand.NotifyCanExecuteChanged();
    }

    // ----- Modes -----

    private bool CanChangeMode() => Mode != DocumentMode.Edit;

    [RelayCommand(CanExecute = nameof(CanChangeMode))]
    private void Study() => SetMode(DocumentMode.Study);

    [RelayCommand]
    private void Read() => SetMode(DocumentMode.Read);

    [RelayCommand(CanExecute = nameof(CanChangeMode))]
    private void ToggleRecap() => SetMode(Mode == DocumentMode.Recap ? DocumentMode.Read : DocumentMode.Recap);

    [RelayCommand]
    private void Configure() => SetMode(DocumentMode.Configure);

    [RelayCommand]
    private void CloseConfigure() => SetMode(DocumentMode.Read);

    private bool CanEdit() => Mode is DocumentMode.Read or DocumentMode.Recap && CurrentPartNumber is not null;

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Edit() => SetMode(DocumentMode.Edit);

    [RelayCommand]
    private void CancelEdit() => SetMode(DocumentMode.Read);

    [RelayCommand]
    private async Task SaveEditsAsync()
    {
        if (ModeContent is EditorViewModel editor && !await Session!.SavePartTextsAsync(editor.ChangedParts))
        {
            return; // stay in edit mode so nothing is lost
        }

        SetMode(DocumentMode.Read);
        if (CurrentPartNumber is { } current)
        {
            await ShowPartAsync(current);
        }
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        if (ModeContent is EditorViewModel { HasChanges: true })
        {
            var discard = await _dialogs.ConfirmAsync(new ConfirmationDialogViewModel(
                "Unsaved edits",
                "Your edits haven't been saved. Do you want to discard them?",
                confirmText: "Discard",
                cancelText: "Keep editing",
                isDestructive: true));
            if (!discard)
            {
                return;
            }
        }

        await _navigation.GoHomeAsync();
    }

    private void SetMode(DocumentMode mode)
    {
        (ModeContent as IDisposable)?.Dispose();

        var session = Session!;
        Mode = mode;
        ModeContent = mode switch
        {
            DocumentMode.Read => new ReaderViewModel(session, _dialogs, Appearance, PreviousPartCommand, NextPartCommand),
            DocumentMode.Edit => new EditorViewModel(_ai, Appearance),
            DocumentMode.Study => new StudyViewModel(session),
            DocumentMode.Configure => new ConfigureViewModel(session),
            DocumentMode.Recap => new RecapViewModel(session, _ai),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
        SetCurrentPart(CurrentPartNumber, _currentText);
    }

    // ----- Session events -----

    [RelayCommand]
    private void DismissError() => ErrorMessage = null;

    private void OnDetailsChanged(object? sender, EventArgs e) => Name = Session!.Name;

    private void OnPartsChanged(object? sender, EventArgs e)
    {
        SyncParts();
        if (CurrentPartNumber is { } current && Session!.GetPart(current) is { Hidden: true })
        {
            SetCurrentPart(null, string.Empty);
        }
    }

    private void OnNotesChanged(object? sender, EventArgs e)
    {
        foreach (var item in Parts)
        {
            item.Refresh();
        }
    }

    private void OnErrorOccurred(object? sender, string message) => ErrorMessage = message;

    public void Dispose()
    {
        (ModeContent as IDisposable)?.Dispose();
        if (Session is not null)
        {
            Session.DetailsChanged -= OnDetailsChanged;
            Session.PartsChanged -= OnPartsChanged;
            Session.NotesChanged -= OnNotesChanged;
            Session.ErrorOccurred -= OnErrorOccurred;
        }
    }
}
