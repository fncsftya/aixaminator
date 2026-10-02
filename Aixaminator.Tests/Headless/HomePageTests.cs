using Aixaminator.Tests.Infrastructure;
using Aixaminator.ViewModels.Document;
using Aixaminator.ViewModels.Home;
using Aixaminator.Views.Document;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace Aixaminator.Tests.Headless;

[Collection(HeadlessCollection.Name)]
public class HomePageTests
{
    private static HomeViewModel Home(AppHarness app) => Assert.IsType<HomeViewModel>(app.ViewModel.Navigation.CurrentPage);

    private static async Task ReloadLibraryAsync(AppHarness app)
    {
        await Home(app).Library.LoadCommand.ExecuteAsync(null);
        app.Window.Settle();
    }

    [AvaloniaFact]
    public async Task The_library_lists_documents_and_opens_them()
    {
        await using var app = await AppHarness.StartAsync();
        Assert.True(app.Window.Find<TextBlock>("EmptyMessage").IsVisible);
        await app.AddDocumentAsync("Moby Dick", "Call me Ishmael.");
        await ReloadLibraryAsync(app);

        var open = app.Window.FindAll<Button>().Single(b => b.Classes.Contains("open-document"));
        app.Window.Click(open);

        await UiExtensions.WaitUntilAsync(() => app.Window.FindAll<DocumentView>().Any(), "the document page");
        var page = Assert.IsType<DocumentViewModel>(app.ViewModel.Navigation.CurrentPage);
        Assert.Equal("Moby Dick", page.Name);
    }

    [AvaloniaFact]
    public async Task Documents_are_deleted_after_confirmation()
    {
        await using var app = await AppHarness.StartAsync();
        await app.AddDocumentAsync("Delete me", "text");
        await ReloadLibraryAsync(app);

        app.Window.Click(app.Window.Find<ItemsControl>("DocumentList").FindButton("Delete"));
        Assert.Contains("Delete me", app.Window.Find<TextBlock>("Message").Text);
        app.Window.Click(app.Window.Find<Button>("ConfirmButton"));

        await UiExtensions.WaitUntilAsync(() => Home(app).Library.Documents.Count == 0, "the document to be removed");
        Assert.Empty(await app.Repository.GetLibraryAsync());
        Assert.True(app.Window.Find<TextBlock>("EmptyMessage").IsVisible);
    }

    [AvaloniaFact]
    public async Task A_file_can_be_imported_and_opened()
    {
        await using var app = await AppHarness.StartAsync();
        app.FilePicker.File = new FakePickedFile("Lecture Notes.txt", "The mitochondria is the powerhouse of the cell.");

        app.Window.Click(app.Window.Find<TabItem>("AddDocumentTab"));
        app.Window.Click(app.Window.Find<Button>("BrowseButton"));
        await UiExtensions.WaitUntilAsync(() => app.Window.Find<TextBox>("SelectedFileName").Text == "Lecture Notes.txt", "the file name");
        app.Window.TypeInto(app.Window.Find<TextBox>("DescriptionInput"), "Biology");
        app.Window.Click(app.Window.Find<Button>("CreateButton"));

        await UiExtensions.WaitUntilAsync(() => app.Window.FindAll<Button>("ViewDocumentButton").Any(), "the import to finish");
        var summary = Assert.Single(await app.Repository.GetLibraryAsync());
        Assert.Equal("Lecture Notes", summary.Name);
        Assert.Equal("Biology", summary.Description);

        app.Window.Click(app.Window.Find<Button>("ViewDocumentButton"));
        await UiExtensions.WaitUntilAsync(() => app.Window.FindAll<ReaderView>().Any(), "the reader");
        Assert.Contains("powerhouse", app.Window.Find<Aixaminator.Controls.ReaderTextBlock>("ReaderText").Inlines!.Text);
    }

    [AvaloniaFact]
    public async Task Import_errors_are_shown()
    {
        await using var app = await AppHarness.StartAsync();

        app.Window.Click(app.Window.Find<TabItem>("AddDocumentTab"));
        app.Window.Click(app.Window.Find<Button>("CreateButton"));

        Assert.Equal("No file selected.", app.Window.Find<TextBlock>("ErrorText").Text);
        Assert.True(app.Window.Find<Button>("CreateButton").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task The_web_source_validates_urls()
    {
        await using var app = await AppHarness.StartAsync();
        app.Window.Click(app.Window.Find<TabItem>("AddDocumentTab"));
        Assert.Empty(app.Window.FindAll<TextBox>("UrlInput"));

        app.Window.Click(app.Window.Find<RadioButton>("WebSourceOption"));
        var url = app.Window.Find<TextBox>("UrlInput");
        Assert.Empty(app.Window.FindAll<Button>("BrowseButton"));

        app.Window.TypeInto(url, "not a url");

        Assert.True(DataValidationErrors.GetHasErrors(url));
        Assert.Contains("Please enter a valid URL.", DataValidationErrors.GetErrors(url)!.Select(e => e?.ToString()));

        app.Window.TypeInto(url, "https://en.wikipedia.org/wiki/Cell_(biology)");
        Assert.False(DataValidationErrors.GetHasErrors(url));
    }

    [AvaloniaFact]
    public async Task Display_settings_are_previewed_and_saved()
    {
        await using var app = await AppHarness.StartAsync();
        app.Window.Click(app.Window.Find<TabItem>("SettingsTab"));

        app.Window.Find<ComboBox>("FontSizeSelector").SelectedItem = 32;
        app.Window.TypeInto(app.Window.Find<TextBox>("BackgroundColorInput"), "#222222");
        await Home(app).Settings.WhenSavedAsync();
        app.Window.Settle();

        var preview = app.Window.Find<TextBlock>("PreviewText");
        Assert.Equal(32, preview.FontSize);
        Assert.Equal(Avalonia.Media.Color.Parse("#222222"),
            Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(app.Window.Find<Border>("Preview").Background).Color);
        Assert.Equal("Saved!", app.Window.Find<TextBlock>("SaveStatus").Text);
        Assert.Equal(32, app.Settings.Settings.FontSize);
        Assert.Contains("\"FontSize\": 32", await File.ReadAllTextAsync(app.Paths.SettingsPath));
    }

    [AvaloniaFact]
    public async Task Invalid_colours_are_flagged()
    {
        await using var app = await AppHarness.StartAsync();
        app.Window.Click(app.Window.Find<TabItem>("SettingsTab"));
        var input = app.Window.Find<TextBox>("ForegroundColorInput");

        app.Window.TypeInto(input, "#zzzzzz");

        Assert.True(DataValidationErrors.GetHasErrors(input));
        Assert.Equal(Aixaminator.Models.ApplicationSettings.DefaultForegroundColor, app.Settings.Settings.ForegroundColor);
    }

    [AvaloniaFact]
    public async Task Ai_providers_can_be_added_from_the_settings()
    {
        await using var app = await AppHarness.StartAsync();
        app.Window.Click(app.Window.Find<TabItem>("SettingsTab"));

        app.Window.Click(app.Window.Find<Button>("AddProviderButton"));
        var save = app.Window.Find<Button>("SaveProviderButton");
        Assert.False(save.IsEffectivelyEnabled);
        app.Window.Find<ComboBox>("NewProviderSelector").SelectedIndex = 1;
        app.Window.TypeInto(app.Window.Find<TextBox>("NewProviderApiKey"), "AIza-test-key-1234");
        app.Window.Click(save);
        await Home(app).Settings.WhenSavedAsync();
        app.Window.Settle();

        var row = app.Window.Find<ItemsControl>("ProviderList");
        Assert.Contains(row.FindAll<TextBlock>(), t => t.Text == "Google");
        Assert.Contains(row.FindAll<TextBlock>(), t => t.Text == "AIza****1234");
        Assert.Equal("google", Assert.Single(app.Settings.Settings.Ai.AiProviders).Name);

        app.Window.Click(row.FindButton("Check Connection"));
        await UiExtensions.WaitUntilAsync(() => row.FindAll<Button>().Any(b => Equals(b.Content, "Connected!")), "the connection check");
    }
}
