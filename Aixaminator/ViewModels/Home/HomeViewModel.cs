using CommunityToolkit.Mvvm.ComponentModel;

namespace Aixaminator.ViewModels.Home;

public enum HomeTab
{
    MyDocuments,
    AddDocument,
    Settings,
}

/// <summary>The start page, with tabs for the library, importing documents and settings.</summary>
public sealed partial class HomeViewModel(
    DocumentLibraryViewModel library,
    CreateDocumentViewModel createDocument,
    SettingsViewModel settings) : PageViewModel
{
    public DocumentLibraryViewModel Library { get; } = library;

    public CreateDocumentViewModel CreateDocument { get; } = createDocument;

    public SettingsViewModel Settings { get; } = settings;

    [ObservableProperty]
    public partial HomeTab SelectedTab { get; set; } = HomeTab.MyDocuments;

    /// <summary>Index form of <see cref="SelectedTab"/> for binding to a TabControl.</summary>
    public int SelectedTabIndex
    {
        get => (int)SelectedTab;
        set => SelectedTab = (HomeTab)value;
    }

    public override Task LoadAsync() => Library.LoadCommand.ExecuteAsync(null);

    partial void OnSelectedTabChanged(HomeTab value)
    {
        OnPropertyChanged(nameof(SelectedTabIndex));
        switch (value)
        {
            case HomeTab.MyDocuments:
                // The library may have changed (e.g. a document was just added), so refresh it.
                Library.LoadCommand.Execute(null);
                break;
            case HomeTab.AddDocument when CreateDocument.Status == ImportStatus.Completed:
                CreateDocument.ResetCommand.Execute(null);
                break;
        }
    }
}
