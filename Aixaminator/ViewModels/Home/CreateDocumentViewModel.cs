using System.ComponentModel.DataAnnotations;
using Aixaminator.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aixaminator.ViewModels.Home;

public enum ImportSource
{
    File,
    Web,
}

public enum ImportStatus
{
    NotStarted,
    Reading,
    Processing,
    Completed,
}

/// <summary>The "Add Document" tab: imports a document from a local file or a web page.</summary>
public sealed partial class CreateDocumentViewModel(
    IDocumentImporter importer,
    IDocumentRepository repository,
    IFilePickerService filePicker,
    INavigationService navigation) : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFileSource), nameof(IsWebSource))]
    public partial ImportSource Source { get; set; } = ImportSource.File;

    /// <summary>Two-way bindable flag for the "Local File" option.</summary>
    public bool IsFileSource
    {
        get => Source == ImportSource.File;
        set
        {
            if (value) Source = ImportSource.File;
        }
    }

    /// <summary>Two-way bindable flag for the "Web" option.</summary>
    public bool IsWebSource
    {
        get => Source == ImportSource.Web;
        set
        {
            if (value) Source = ImportSource.Web;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedFileName))]
    public partial IPickedFile? SelectedFile { get; private set; }

    public string SelectedFileName => SelectedFile?.Name ?? "No file chosen";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(CreateDocumentViewModel), nameof(ValidateUrl))]
    public partial string Url { get; set; } = string.Empty;

    /// <summary>Optional summary of the document; helps the AI give more accurate quizzes.</summary>
    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(IsCompleted), nameof(IsEditable), nameof(StatusMessage))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand), nameof(BrowseCommand))]
    public partial ImportStatus Status { get; private set; } = ImportStatus.NotStarted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; private set; }

    public bool HasError => ErrorMessage is not null;

    public bool IsBusy => Status is ImportStatus.Reading or ImportStatus.Processing;

    public bool IsEditable => Status == ImportStatus.NotStarted;

    public bool IsCompleted => Status == ImportStatus.Completed;

    public string? StatusMessage => Status switch
    {
        ImportStatus.Reading => "Analyzing document...",
        ImportStatus.Processing => "Processing document...",
        ImportStatus.Completed => "Done!",
        _ => null,
    };

    [ObservableProperty]
    public partial Guid? CreatedDocumentId { get; private set; }

    partial void OnSourceChanged(ImportSource value) => ErrorMessage = null;

    private bool CanEdit() => IsEditable;

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task BrowseAsync()
    {
        var file = await filePicker.PickDocumentAsync();
        if (file is not null)
        {
            SelectedFile = file;
            ErrorMessage = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task CreateAsync()
    {
        ErrorMessage = null;

        if (Source == ImportSource.File && SelectedFile is null)
        {
            ErrorMessage = "No file selected.";
            return;
        }

        if (Source == ImportSource.Web && string.IsNullOrWhiteSpace(Url))
        {
            ErrorMessage = "No URL provided.";
            return;
        }

        Status = ImportStatus.Reading;
        try
        {
            ImportedText imported;
            if (Source == ImportSource.File)
            {
                await using var stream = await SelectedFile!.OpenReadAsync();
                imported = await importer.ReadFileAsync(SelectedFile.Name, stream);
            }
            else
            {
                imported = await importer.ReadUrlAsync(Url.Trim());
            }

            Status = ImportStatus.Processing;

            // Automatic splitting isn't available yet, so the whole text becomes a single part.
            var document = await repository.CreateAsync(new NewDocument(imported.SuggestedName, Description, [imported.Text]));

            CreatedDocumentId = document.Id;
            Status = ImportStatus.Completed;
        }
        catch (DocumentImportException ex)
        {
            ErrorMessage = ex.Message;
            Status = ImportStatus.NotStarted;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error processing content: {ex.Message}";
            Status = ImportStatus.NotStarted;
        }
    }

    [RelayCommand]
    private async Task ViewDocumentAsync()
    {
        if (CreatedDocumentId is { } id)
        {
            await navigation.OpenDocumentAsync(id);
        }
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedFile = null;
        Url = string.Empty;
        Description = string.Empty;
        ErrorMessage = null;
        CreatedDocumentId = null;
        ClearErrors();
        Status = ImportStatus.NotStarted;
    }

    public static ValidationResult? ValidateUrl(string url, ValidationContext context) =>
        string.IsNullOrWhiteSpace(url) || DocumentImporter.IsValidUrl(url.Trim())
            ? ValidationResult.Success
            : new ValidationResult("Please enter a valid URL.");
}
