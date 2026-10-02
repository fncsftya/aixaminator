using System.ComponentModel.DataAnnotations;
using Aixaminator.Data;
using Aixaminator.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aixaminator.ViewModels.Document;

/// <summary>Settings for one part (name, colour, visibility), edited inline in the sidebar and saved as they change.</summary>
public sealed partial class PartSettingsViewModel : ViewModelBase
{
    private readonly DocumentPart _part;
    private readonly DocumentSession _session;
    private readonly bool _initialised;

    public PartSettingsViewModel(DocumentPart part, DocumentSession session)
    {
        _part = part;
        _session = session;
        Name = part.Name ?? string.Empty;
        UseCustomColour = part.UseColour;
        Colour = part.Colour ?? Palette.PartColours[0];
        _initialised = true;
    }

    public IReadOnlyList<string> ColourChoices => Palette.PartColours;

    /// <summary>Custom name; when blank the default "Part N" is used.</summary>
    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial bool UseCustomColour { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Enter a colour like #ff0000.")]
    public partial string Colour { get; set; }

    partial void OnNameChanged(string value) => Save(p => p.Name = string.IsNullOrWhiteSpace(value) ? null : value.Trim());

    partial void OnUseCustomColourChanged(bool value) => Save(p =>
    {
        p.UseColour = value;
        if (value && ApplicationSettings.IsValidColour(Colour))
        {
            p.Colour = Colour;
        }
    });

    partial void OnColourChanged(string value)
    {
        if (ApplicationSettings.IsValidColour(value))
        {
            Save(p => p.Colour = value);
        }
    }

    [RelayCommand]
    private void PickColour(string colour)
    {
        UseCustomColour = true;
        Colour = colour;
    }

    [RelayCommand]
    private void Hide() => Save(p => p.Hidden = true);

    private void Save(Action<DocumentPart> change)
    {
        if (!_initialised)
        {
            return;
        }

        change(_part);
        _ = _session.SavePartAsync(_part);
    }
}
