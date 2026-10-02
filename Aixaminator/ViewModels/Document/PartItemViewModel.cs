using Aixaminator.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aixaminator.ViewModels.Document;

/// <summary>A part in the document page's sidebar.</summary>
public sealed partial class PartItemViewModel : ViewModelBase
{
    private readonly DocumentViewModel _owner;

    internal PartItemViewModel(DocumentViewModel owner, DocumentPart part)
    {
        _owner = owner;
        Part = part;
    }

    internal DocumentPart Part { get; }

    public int PartNumber => Part.PartNumber;

    public string DisplayName => Part.DisplayName;

    public bool IsCurrent => _owner.CurrentPartNumber == PartNumber;

    public bool IsRead => Part.IsRead;

    public string ReadToggleToolTip => IsRead ? "Mark as unread" : "Mark as read";

    public int NoteCount => _owner.Session?.NotesForPart(PartNumber).Count() ?? 0;

    /// <summary>Note counts are shown while studying.</summary>
    public bool ShowNoteCount => _owner.Mode == DocumentMode.Study;

    /// <summary>The read toggle is hidden while editing, studying or configuring.</summary>
    public bool ShowReadToggle => _owner.Mode is DocumentMode.Read or DocumentMode.Recap;

    /// <summary>The custom colour of the part, if it has one.</summary>
    public string? AccentColour => Part.UseColour && Models.ApplicationSettings.IsValidColour(Part.Colour) ? Part.Colour : null;

    [ObservableProperty]
    public partial PartSettingsViewModel? Settings { get; private set; }

    public bool IsSettingsOpen => Settings is not null;

    [RelayCommand]
    private Task SelectAsync() => _owner.ShowPartAsync(PartNumber);

    [RelayCommand]
    private Task ToggleReadAsync() => _owner.ToggleReadAsync(this);

    [RelayCommand]
    private void ToggleSettings() => _owner.TogglePartSettings(this);

    internal void OpenSettings() => Settings ??= new PartSettingsViewModel(Part, _owner.Session!);

    internal void CloseSettings() => Settings = null;

    partial void OnSettingsChanged(PartSettingsViewModel? value) => OnPropertyChanged(nameof(IsSettingsOpen));

    /// <summary>Re-reads every derived property, e.g. after the part or the page's mode changed.</summary>
    internal void Refresh()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(IsCurrent));
        OnPropertyChanged(nameof(IsRead));
        OnPropertyChanged(nameof(ReadToggleToolTip));
        OnPropertyChanged(nameof(NoteCount));
        OnPropertyChanged(nameof(ShowNoteCount));
        OnPropertyChanged(nameof(ShowReadToggle));
        OnPropertyChanged(nameof(AccentColour));
    }
}
