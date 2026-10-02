using Aixaminator.Controls;
using Aixaminator.Data;
using Aixaminator.Models;
using Aixaminator.Tests.Infrastructure;
using Aixaminator.ViewModels.Document;
using Aixaminator.ViewModels.Home;
using Aixaminator.Views.Dialogs;
using Aixaminator.Views.Document;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace Aixaminator.Tests.Headless;

[Collection(HeadlessCollection.Name)]
public class DocumentPageTests
{
    private const string FirstPart = "The quick brown fox jumps over the lazy dog.\n\nA second paragraph follows here.";

    private static async Task<(AppHarness App, Document Document, DocumentViewModel Page)> OpenAsync(params string[] parts)
    {
        var app = await AppHarness.StartAsync();
        // Use the bundled Inter font so text layout is the same on every machine.
        app.Settings.Settings.FontFamily = "Neo-Grotesque";
        var document = await app.AddDocumentAsync("Fables", parts.Length > 0 ? parts : [FirstPart, "Second part text.", "Third part text."]);
        await app.ViewModel.Navigation.OpenDocumentAsync(document.Id);
        app.Window.Settle();
        return (app, document, Assert.IsType<DocumentViewModel>(app.ViewModel.Navigation.CurrentPage));
    }

    private static Button PartButton(AppHarness app, string name) =>
        app.Window.Find<ItemsControl>("PartList").FindAll<Button>()
            .Single(b => b.Classes.Contains("part") && b.FindAll<TextBlock>().Any(t => t.Text == name));

    private static ReaderTextBlock ReaderText(AppHarness app) => app.Window.Find<ReaderTextBlock>("ReaderText");

    [AvaloniaFact]
    public async Task The_sidebar_lists_parts_and_shows_the_selected_one()
    {
        var (app, _, _) = await OpenAsync();
        await using var _app = app;

        Assert.Equal("Fables", app.Window.Find<TextBlock>("DocumentName").Text);
        Assert.Equal("0%", app.Window.Find<TextBlock>("ProgressText").Text);
        Assert.StartsWith("The quick brown fox", ReaderText(app).Inlines!.Text);
        Assert.Contains("current", PartButton(app, "Part 1").Classes);

        app.Window.Click(PartButton(app, "Part 3"));
        await UiExtensions.WaitUntilAsync(() => ReaderText(app).Inlines?.Text == "Third part text.", "part 3 to be shown");

        Assert.Contains("current", PartButton(app, "Part 3").Classes);
        Assert.DoesNotContain("current", PartButton(app, "Part 1").Classes);
        Assert.False(app.Window.Find<Button>("NextButton").IsEffectivelyEnabled);
    }

    [AvaloniaFact]
    public async Task Selecting_text_and_choosing_a_colour_highlights_it()
    {
        var (app, document, page) = await OpenAsync();
        await using var _app = app;
        var reader = Assert.IsType<ReaderViewModel>(page.ModeContent);
        Assert.Empty(app.Window.FindAll<StackPanel>("HighlightMenu"));

        // Drag across the start of the first line, as a user selecting with the mouse.
        var text = ReaderText(app);
        app.Window.Drag(text, new Point(1, 10), new Point(text.Bounds.Width / 3, 10));

        Assert.True(reader.HasSelection);
        var selected = reader.SelectedText;
        Assert.StartsWith("The quick", selected);
        Assert.True(app.Window.Find<StackPanel>("HighlightMenu").IsVisible);

        // The swatches don't take focus, so the selection survives the click.
        var green = HighlightColour.All.Single(c => c.Name == "Green");
        var swatch = app.Window.Find<ItemsControl>("HighlightColours").FindAll<Button>()
            .Single(b => Equals(ToolTip.GetTip(b), "Highlight Green"));
        app.Window.Click(swatch);

        await UiExtensions.WaitUntilAsync(() => page.Session!.Notes.Count == 1, "the highlight to be saved");
        var note = Assert.Single((await app.Repository.GetAsync(document.Id))!.Notes);
        Assert.Equal(NoteKind.Highlight, note.Kind);
        Assert.Equal(selected, note.Text);
        Assert.Equal(green.Hex, note.HighlightColour);

        var highlightedRun = ReaderText(app).Inlines!.OfType<Run>().Single(r => r.Background is not null);
        Assert.Equal(selected, highlightedRun.Text);
        Assert.Equal(Color.Parse(green.Hex), ((ISolidColorBrush)highlightedRun.Background!).Color);
        Assert.Empty(app.Window.FindAll<StackPanel>("HighlightMenu"));
    }

    [AvaloniaFact]
    public async Task Notes_are_added_through_the_dialog()
    {
        var (app, document, page) = await OpenAsync();
        await using var _app = app;

        app.Window.Click(app.Window.Find<Button>("AddQuestionButton"));
        Assert.Single(app.Window.FindAll<AddNoteDialogView>());
        app.Window.Click(app.Window.Find<Button>("AddButton"));
        var answer = app.Window.Find<TextBox>("AnswerText");
        Assert.True(DataValidationErrors.GetHasErrors(app.Window.Find<TextBox>("NoteText")));

        app.Window.TypeInto(app.Window.Find<TextBox>("NoteText"), "What does the fox jump over?");
        app.Window.TypeInto(answer, "The lazy dog");
        app.Window.Click(app.Window.Find<Button>("AddButton"));

        await UiExtensions.WaitUntilAsync(() => page.Session!.Notes.Count == 1, "the question to be saved");
        Assert.Empty(app.Window.FindAll<AddNoteDialogView>());
        var note = Assert.Single((await app.Repository.GetAsync(document.Id))!.Notes);
        Assert.Equal(NoteKind.Question, note.Kind);
        Assert.Equal("The lazy dog", note.Answer);
    }

    [AvaloniaFact]
    public async Task Moving_on_without_notes_asks_for_confirmation()
    {
        var (app, _, page) = await OpenAsync();
        await using var _app = app;

        app.Window.Click(app.Window.Find<Button>("NextButton"));
        Assert.Equal("No notes taken", ((Aixaminator.ViewModels.Dialogs.ConfirmationDialogViewModel)app.ViewModel.Dialogs.ActiveDialog!).Title);

        app.Window.Click(app.Window.Find<Button>("CancelButton"));
        Assert.Equal(0, page.CurrentPartNumber);

        app.Window.Click(app.Window.Find<Button>("NextButton"));
        app.Window.Click(app.Window.Find<Button>("ConfirmButton"));

        await UiExtensions.WaitUntilAsync(() => page.CurrentPartNumber == 1, "part 2 to be shown");
        Assert.Equal("Second part text.", ReaderText(app).Inlines!.Text);
        Assert.Contains("read", PartButton(app, "Part 1").Classes);
        Assert.Equal("33%", app.Window.Find<TextBlock>("ProgressText").Text);
    }

    [AvaloniaFact]
    public async Task Mode_buttons_switch_the_content()
    {
        var (app, document, _) = await OpenAsync();
        await using var _app = app;
        await app.Repository.AddNoteAsync(new Note { DocumentId = document.Id, DocumentPartNumber = 0, Text = "Remember this" });
        await app.ViewModel.Navigation.OpenDocumentAsync(document.Id);
        app.Window.Settle();

        app.Window.Click(app.Window.Find<Button>("StudyButton"));
        Assert.Single(app.Window.FindAll<StudyView>());
        Assert.Equal("Study notes for part 1", app.Window.Find<TextBlock>("Heading").Text);
        Assert.Contains(app.Window.Find<ItemsControl>("NoteList").FindAll<TextBlock>(), t => t.Text == "Remember this");
        Assert.False(app.Window.Find<Button>("EditButton").IsEffectivelyEnabled);

        app.Window.Click(app.Window.Find<Button>("ReadButton"));
        Assert.Single(app.Window.FindAll<ReaderView>());

        app.Window.Click(app.Window.Find<Button>("ConfigureButton"));
        Assert.Single(app.Window.FindAll<ConfigureView>());
        app.Window.TypeInto(app.Window.Find<TextBox>("DocumentTitleInput"), "Aesop's Fables");
        Assert.Equal("Aesop's Fables", app.Window.Find<TextBlock>("DocumentName").Text);
        app.Window.Click(app.Window.Find<Button>("CancelConfigureButton"));
        Assert.Single(app.Window.FindAll<ReaderView>());

        app.Window.Click(app.Window.Find<Button>("RecapButton"));
        Assert.Single(app.Window.FindAll<RecapView>());
        app.Window.Click(app.Window.Find<Button>("CloseModeButton"));
        Assert.Single(app.Window.FindAll<ReaderView>());

        await ((DocumentViewModel)app.ViewModel.Navigation.CurrentPage!).Session!.WhenIdleAsync();
        Assert.Equal("Aesop's Fables", (await app.Repository.GetAsync(document.Id))!.Name);
    }

    [AvaloniaFact]
    public async Task Recap_quizzes_give_feedback()
    {
        var (app, document, page) = await OpenAsync(
            "The Roman Empire was one of the largest empires in ancient history. It was founded in 27 BC by Augustus.");
        await using var _app = app;
        app.Window.Click(app.Window.Find<Button>("RecapButton"));
        var recap = Assert.IsType<RecapViewModel>(page.ModeContent);

        var generate = app.Window.Find<Button>("GenerateButton");
        Assert.False(generate.IsEffectivelyEnabled);
        app.Window.Click(app.Window.Find<ItemsControl>("PartOptions").FindAll<CheckBox>().Single());
        app.Window.Click(generate);

        await UiExtensions.WaitUntilAsync(() => recap.HasQuiz, "the quiz");
        app.Window.Settle();
        var answers = app.Window.Find<ItemsControl>("Questions").FindAll<RadioButton>().ToList();
        Assert.Equal(4, answers.Count);

        var correct = answers.Single(r => ((QuizAnswerViewModel)r.DataContext!).IsCorrect);
        app.Window.Click(correct);

        Assert.Contains(app.Window.Find<ItemsControl>("Questions").FindAll<TextBlock>(), t => t.Text == "Correct!");
    }

    [AvaloniaFact]
    public async Task Text_can_be_edited_and_saved()
    {
        var (app, document, _) = await OpenAsync();
        await using var _app = app;

        app.Window.Click(app.Window.Find<Button>("EditButton"));
        var editor = app.Window.Find<TextBox>("EditorText");
        Assert.Equal(FirstPart, editor.Text);
        Assert.False(app.Window.Find<Button>("StudyButton").IsEffectivelyEnabled);

        app.Window.TypeInto(editor, "Rewritten text.");
        app.Window.Click(app.Window.Find<Button>("SaveEditsButton"));

        await UiExtensions.WaitUntilAsync(() => app.Window.FindAll<ReaderView>().Any(), "read mode");
        Assert.Equal("Rewritten text.", ReaderText(app).Inlines!.Text);
        Assert.Equal("Rewritten text.", await app.Repository.GetPartTextAsync(document.Id, 0));
    }

    [AvaloniaFact]
    public async Task The_editor_keeps_the_reader_colours_while_focused()
    {
        var (app, _, _) = await OpenAsync();
        await using var _app = app;
        app.Settings.Settings.BackgroundColor = "#123456";
        await app.ViewModel.Navigation.OpenDocumentAsync(((DocumentViewModel)app.ViewModel.Navigation.CurrentPage!).DocumentId);
        app.Window.Settle();

        app.Window.Click(app.Window.Find<Button>("EditButton"));
        var editor = app.Window.Find<TextBox>("EditorText");
        app.Window.Click(editor);

        Assert.True(editor.IsFocused);
        var border = editor.FindAll<Border>("PART_BorderElement").Single();
        Assert.Equal(Color.Parse("#123456"), Assert.IsAssignableFrom<ISolidColorBrush>(border.Background).Color);
    }

    [AvaloniaFact]
    public async Task Ai_cleaning_replaces_the_edited_text()
    {
        var (app, _, _) = await OpenAsync();
        await using var _app = app;
        app.Ai.Clean = _ => Task.FromResult("Clean text.\n12");

        app.Window.Click(app.Window.Find<Button>("EditButton"));
        app.Window.Click(app.Window.Find<Button>("CleanButton"));

        await UiExtensions.WaitUntilAsync(() => app.Window.Find<TextBox>("EditorText").Text == "Clean text.\n\n", "the cleaned text");
        Assert.Equal("Clean Text", app.Window.Find<Button>("CleanButton").Content);
    }

    [AvaloniaFact]
    public async Task Parts_can_be_renamed_and_hidden_from_the_sidebar()
    {
        var (app, document, page) = await OpenAsync();
        await using var _app = app;
        var settingsToggle = app.Window.Find<ItemsControl>("PartList").FindAll<Button>()
            .Where(b => b.Classes.Contains("settings-toggle")).ElementAt(1);

        app.Window.Click(settingsToggle);
        app.Window.TypeInto(app.Window.Find<TextBox>("PartNameInput"), "Chapter Two");

        Assert.NotNull(PartButton(app, "Chapter Two"));

        app.Window.Click(app.Window.Find<Button>("HidePartButton"));
        await page.Session!.WhenIdleAsync();
        app.Window.Settle();

        var names = app.Window.Find<ItemsControl>("PartList").FindAll<Button>()
            .Where(b => b.Classes.Contains("part"))
            .Select(b => b.FindAll<TextBlock>().First().Text);
        Assert.Equal(["Part 1", "Part 3"], names);
        var saved = (await app.Repository.GetAsync(document.Id))!.Parts[1];
        Assert.Equal("Chapter Two", saved.Name);
        Assert.True(saved.Hidden);
    }

    [AvaloniaFact]
    public async Task Closing_returns_to_the_library()
    {
        var (app, _, _) = await OpenAsync();
        await using var _app = app;

        app.Window.Click(app.Window.Find<Button>("CloseButton"));

        await UiExtensions.WaitUntilAsync(() => app.ViewModel.Navigation.CurrentPage is HomeViewModel, "the home page");
        app.Window.Settle();
        Assert.Contains(app.Window.FindAll<TextBlock>(), t => t.Text == "Fables");
    }
}
