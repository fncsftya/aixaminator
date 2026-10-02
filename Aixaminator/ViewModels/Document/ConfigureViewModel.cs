using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using Aixaminator.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aixaminator.ViewModels.Document;

/// <summary>Configure mode: the document's title and description, and the parts that have been hidden.</summary>
public sealed partial class ConfigureViewModel : ViewModelBase, IDisposable
{
    private readonly DocumentSession _session;
    private readonly bool _initialised;

    public ConfigureViewModel(DocumentSession session)
    {
        _session = session;
        Name = session.Name;
        Description = session.Description ?? string.Empty;
        RebuildHiddenParts();
        _session.PartsChanged += OnPartsChanged;
        _initialised = true;
    }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(AllowEmptyStrings = false, ErrorMessage = "A document needs a title.")]
    public partial string Name { get; set; }

    /// <summary>Helps the AI give more accurate quizzes.</summary>
    [ObservableProperty]
    public partial string Description { get; set; }

    public ObservableCollection<HiddenPartViewModel> HiddenParts { get; } = [];

    public bool HasHiddenParts => HiddenParts.Count > 0;

    partial void OnNameChanged(string value) => SaveDetails();

    partial void OnDescriptionChanged(string value) => SaveDetails();

    private void SaveDetails()
    {
        if (_initialised && !string.IsNullOrWhiteSpace(Name))
        {
            _ = _session.UpdateDetailsAsync(Name, Description);
        }
    }

    private void RebuildHiddenParts()
    {
        HiddenParts.Clear();
        foreach (var part in _session.Parts.Where(p => p.Hidden))
        {
            HiddenParts.Add(new HiddenPartViewModel(part, _session));
        }
        OnPropertyChanged(nameof(HasHiddenParts));
    }

    private void OnPartsChanged(object? sender, EventArgs e) => RebuildHiddenParts();

    public void Dispose() => _session.PartsChanged -= OnPartsChanged;
}

public sealed partial class HiddenPartViewModel(DocumentPart part, DocumentSession session) : ViewModelBase
{
    public string DisplayName => part.DisplayName;

    [RelayCommand]
    private Task UnhideAsync()
    {
        part.Hidden = false;
        return session.SavePartAsync(part);
    }
}
