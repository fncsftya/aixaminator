using Aixaminator.Features;
using Aixaminator.Models;
using Aixaminator.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aixaminator.ViewModels.Document;

/// <summary>
/// Edit mode: the text of each part can be edited, optionally with the help of the AI.
/// Edits are kept for every part visited until they are saved or cancelled by the document page.
/// </summary>
public sealed partial class EditorViewModel(IAiConnection ai, ReaderAppearance appearance) : ViewModelBase, IPartContent
{
    private readonly Dictionary<int, string> _original = [];
    private readonly Dictionary<int, string> _edits = [];

    public ReaderAppearance Appearance { get; } = appearance;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPart))]
    [NotifyCanExecuteChangedFor(nameof(CleanCommand))]
    public partial int? PartNumber { get; private set; }

    public bool HasPart => PartNumber is not null;

    /// <summary>The edited text of the current part.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CleanButtonText))]
    [NotifyCanExecuteChangedFor(nameof(CleanCommand))]
    public partial bool IsCleaning { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CleanButtonText))]
    [NotifyCanExecuteChangedFor(nameof(CleanCommand))]
    public partial bool CleaningFailed { get; private set; }

    [ObservableProperty]
    public partial string? CleaningError { get; private set; }

    /// <summary>The part being cleaned, which may differ from the current part.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CleanButtonText))]
    public partial int? CleaningPartNumber { get; private set; }

    public string CleanButtonText =>
        IsCleaning ? $"Cleaning Part {CleaningPartNumber + 1}..." : CleaningFailed ? "Failed" : "Clean Text";

    /// <summary>Parts whose text differs from what is saved.</summary>
    public IReadOnlyDictionary<int, string> ChangedParts =>
        _edits.Where(e => _original[e.Key] != e.Value).ToDictionary(e => e.Key, e => e.Value);

    public bool HasChanges => ChangedParts.Count > 0;

    public void ShowPart(int? partNumber, string text)
    {
        if (partNumber is { } part && !_edits.ContainsKey(part))
        {
            _original[part] = text;
            _edits[part] = text;
        }

        PartNumber = partNumber;
        Text = partNumber is { } current ? _edits[current] : string.Empty;
        CleaningFailed = false;
        CleaningError = null;
    }

    partial void OnTextChanged(string value)
    {
        if (PartNumber is { } part)
        {
            _edits[part] = value;
        }
    }

    private bool CanClean() => HasPart && !IsCleaning && !CleaningFailed;

    [RelayCommand(CanExecute = nameof(CanClean))]
    private async Task CleanAsync()
    {
        if (PartNumber is not { } part)
        {
            return;
        }

        CleaningPartNumber = part;
        IsCleaning = true;
        try
        {
            var cleaned = TextCleaner.CleanAiOutput(await ai.CleanTextAsync(_edits[part]));
            _edits[part] = cleaned;
            if (PartNumber == part)
            {
                Text = cleaned;
            }
        }
        catch (Exception ex)
        {
            CleaningFailed = true;
            CleaningError = ex.Message;
        }
        finally
        {
            IsCleaning = false;
            CleaningPartNumber = null;
        }
    }
}
