using Aixaminator.Services;
using Aixaminator.ViewModels;
using Aixaminator.ViewModels.Dialogs;
using Aixaminator.ViewModels.Home;
using Aixaminator.Views;
using Aixaminator.Views.Home;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace Aixaminator.Tests.Headless;

[Collection(HeadlessCollection.Name)]
public class MainWindowTests
{
    [AvaloniaFact]
    public async Task Startup_prepares_storage_and_shows_the_home_page()
    {
        await using var app = await AppHarness.StartAsync();

        Assert.False(app.ViewModel.IsStarting);
        Assert.Null(app.ViewModel.StartupError);
        Assert.IsType<HomeViewModel>(app.ViewModel.Navigation.CurrentPage);
        Assert.True(File.Exists(app.Paths.DatabasePath));
        Assert.True(File.Exists(app.Paths.SettingsPath));

        var home = app.Window.FindAll<HomeView>().Single();
        Assert.Equal(["My Documents", "Add Document", "Settings"],
            home.Find<TabControl>("Tabs").Items.Cast<TabItem>().Select(t => t.Header));
        Assert.Single(app.Window.FindAll<DocumentLibraryView>());
    }

    [AvaloniaFact]
    public async Task Startup_failures_are_reported()
    {
        var vm = new MainWindowViewModel(new NavigationService(null!), new DialogService(), new FailingInitializer());
        var window = new MainWindow { DataContext = vm };
        window.Show();

        await vm.InitializeAsync();
        window.Settle();

        Assert.Equal("Aixaminator could not start: disk full", window.Find<TextBlock>("StartupErrorText").Text);
        Assert.Null(vm.Navigation.CurrentPage);
    }

    [AvaloniaFact]
    public async Task Dialogs_are_modal()
    {
        await using var app = await AppHarness.StartAsync();
        var dialog = new ConfirmationDialogViewModel("Are you sure?", "Really?");

        var result = app.ViewModel.Dialogs.ShowAsync(dialog);
        app.Window.Settle();

        Assert.True(app.Window.Find<Panel>("DialogOverlay").IsVisible);
        Assert.False(app.Window.Find<ContentControl>("PageHost").IsEnabled);
        Assert.Equal("Really?", app.Window.Find<TextBlock>("Message").Text);

        app.Window.Click(app.Window.Find<Button>("ConfirmButton"));

        Assert.True(await result);
        Assert.True(app.Window.Find<ContentControl>("PageHost").IsEnabled);
        Assert.Empty(app.Window.FindAll<Panel>("DialogOverlay"));
    }

    [AvaloniaFact]
    public async Task Escape_dismisses_a_dialog()
    {
        await using var app = await AppHarness.StartAsync();

        var result = app.ViewModel.Dialogs.ShowAsync(new ConfirmationDialogViewModel("Title", "Message"));
        app.Window.Settle();
        app.Window.PressKey(Key.Escape, PhysicalKey.Escape);

        Assert.False(await result);
        Assert.Null(app.ViewModel.Dialogs.ActiveDialog);
    }

    [AvaloniaFact]
    public async Task Clicking_outside_a_dialog_dismisses_it()
    {
        await using var app = await AppHarness.StartAsync();

        var result = app.ViewModel.Dialogs.ShowAsync(new ConfirmationDialogViewModel("Title", "Message"));
        app.Window.Settle();
        var backdrop = app.Window.Find<Border>("DialogBackdrop");
        app.Window.Drag(backdrop, new Avalonia.Point(5, 5), new Avalonia.Point(5, 5));

        Assert.False(await result);
    }

    [AvaloniaFact]
    public async Task Every_page_binds_without_errors()
    {
        await using var app = await AppHarness.StartAsync();
        var document = await app.AddDocumentAsync("Tour", "First part.", "Second part.");
        await app.Repository.AddNoteAsync(new Data.Note
        {
            DocumentId = document.Id, DocumentPartNumber = 0, Kind = Data.NoteKind.Question, Text = "Q?", Answer = "A.",
        });
        BindingErrors.Clear();

        var home = (HomeViewModel)app.ViewModel.Navigation.CurrentPage!;
        foreach (var tab in Enum.GetValues<HomeTab>())
        {
            home.SelectedTab = tab;
            app.Window.Settle();
        }
        home.Settings.StartAddingProviderCommand.Execute(null);
        app.Window.Settle();

        await app.ViewModel.Navigation.OpenDocumentAsync(document.Id);
        app.Window.Settle();
        var page = (Aixaminator.ViewModels.Document.DocumentViewModel)app.ViewModel.Navigation.CurrentPage!;
        page.Parts[0].ToggleSettingsCommand.Execute(null);
        foreach (var command in new[] { page.StudyCommand, page.ReadCommand, page.ConfigureCommand, page.ReadCommand, page.ToggleRecapCommand, page.ReadCommand, page.EditCommand })
        {
            command.Execute(null);
            app.Window.Settle();
        }
        _ = app.ViewModel.Dialogs.ShowAsync(new AddNoteDialogViewModel(true, "context"));
        app.Window.Settle();

        Assert.Empty(BindingErrors.Snapshot());
    }

    [AvaloniaFact]
    public void Binding_errors_are_detected()
    {
        BindingErrors.Clear();
        var text = new TextBlock { DataContext = new object() };
        text.Bind(TextBlock.TextProperty, new Avalonia.Data.ReflectionBinding("MissingProperty"));

        text.ShowInWindow(100, 100).Close();

        Assert.Contains(BindingErrors.Snapshot(), m => m.Contains("MissingProperty"));
        BindingErrors.Clear();
    }

    private sealed class FailingInitializer : IAppInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => throw new IOException("disk full");
    }
}
