using System.Collections.ObjectModel;
using Aixaminator.Services;
using Aixaminator.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aixaminator.ViewModels.Home;

/// <summary>The "My Documents" tab: lists the documents in the library.</summary>
public sealed partial class DocumentLibraryViewModel(
    IDocumentRepository repository,
    INavigationService navigation,
    IDialogService dialogs) : ViewModelBase
{
    public ObservableCollection<DocumentListItemViewModel> Documents { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    public bool IsEmpty => !IsLoading && Documents.Count == 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var documents = await repository.GetLibraryAsync();
            Documents.Clear();
            foreach (var document in documents)
            {
                Documents.Add(new DocumentListItemViewModel(document, this));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Your documents could not be loaded: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    internal Task OpenAsync(DocumentListItemViewModel item) => navigation.OpenDocumentAsync(item.Id);

    internal async Task DeleteAsync(DocumentListItemViewModel item)
    {
        var confirmed = await dialogs.ConfirmAsync(new ConfirmationDialogViewModel(
            "Delete document",
            $"\"{item.Name}\" and all of its notes will be permanently deleted.",
            confirmText: "Delete",
            cancelText: "Cancel",
            isDestructive: true));
        if (!confirmed)
        {
            return;
        }

        try
        {
            await repository.DeleteAsync(item.Id);
            Documents.Remove(item);
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"\"{item.Name}\" could not be deleted: {ex.Message}";
        }
    }
}

public sealed partial class DocumentListItemViewModel(DocumentSummary summary, DocumentLibraryViewModel library) : ViewModelBase
{
    public Guid Id => summary.Id;

    public string Name => summary.Name;

    public string? Description => summary.Description;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public int Progress => summary.Progress;

    public string ProgressText => $"{summary.Progress}%";

    [RelayCommand]
    private Task OpenAsync() => library.OpenAsync(this);

    [RelayCommand]
    private Task DeleteAsync() => library.DeleteAsync(this);
}
