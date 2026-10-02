using Aixaminator.Services;
using Aixaminator.ViewModels.Dialogs;

namespace Aixaminator.Tests.ViewModels;

public class DialogTests
{
    [Fact]
    public async Task Confirming_returns_true()
    {
        var dialog = new ConfirmationDialogViewModel("Title", "Message");

        dialog.ConfirmCommand.Execute(null);

        Assert.True(await dialog.Result);
        Assert.True(dialog.IsClosed);
    }

    [Fact]
    public async Task Cancelling_a_confirmation_returns_false()
    {
        var dialog = new ConfirmationDialogViewModel("Title", "Message");

        dialog.CancelCommand.Execute(null);

        Assert.False(await dialog.Result);
    }

    [Fact]
    public void Confirmation_defaults_match_the_navigation_prompt()
    {
        var dialog = new ConfirmationDialogViewModel("No notes taken", "Continue?");

        Assert.Equal("Continue", dialog.ConfirmText);
        Assert.Equal("Return", dialog.CancelText);
        Assert.False(dialog.IsDestructive);
    }

    [Fact]
    public async Task A_note_requires_text()
    {
        var dialog = new AddNoteDialogViewModel(isQuestion: false, referenceText: null);

        dialog.AddCommand.Execute(null);

        Assert.False(dialog.IsClosed);
        Assert.True(dialog.HasErrors);
        Assert.Equal("You must provide some text for your note.", dialog.GetErrors(nameof(dialog.Text)).Single().ErrorMessage);

        dialog.Text = "  My note  ";
        Assert.False(dialog.HasErrors);
        dialog.AddCommand.Execute(null);

        Assert.Equal(new NoteInput("My note", null), await dialog.Result);
    }

    [Fact]
    public async Task A_question_requires_an_answer()
    {
        var dialog = new AddNoteDialogViewModel(isQuestion: true, referenceText: "selected text")
        {
            Text = "What is it?",
        };

        dialog.AddCommand.Execute(null);

        Assert.False(dialog.IsClosed);
        Assert.Equal("You must provide an answer for your question.", dialog.GetErrors(nameof(dialog.Answer)).Single().ErrorMessage);

        dialog.Answer = "This.";
        dialog.AddCommand.Execute(null);

        Assert.Equal(new NoteInput("What is it?", "This."), await dialog.Result);
    }

    [Fact]
    public void Note_dialog_labels_depend_on_the_kind()
    {
        var note = new AddNoteDialogViewModel(false, "  ");
        var question = new AddNoteDialogViewModel(true, " Some text ");

        Assert.Equal("Add a note", note.Title);
        Assert.Equal("Content", note.TextLabel);
        Assert.False(note.HasReferenceText);
        Assert.Equal("Add a question", question.Title);
        Assert.Equal("Question", question.TextLabel);
        Assert.Equal("Some text", question.ReferenceText);
    }

    [Fact]
    public async Task Cancelled_note_dialog_returns_null()
    {
        var dialog = new AddNoteDialogViewModel(false, null);

        dialog.CancelCommand.Execute(null);

        Assert.Null(await dialog.Result);
    }

    [Fact]
    public async Task Dialog_service_exposes_the_active_dialog_until_it_closes()
    {
        var service = new DialogService();
        var dialog = new ConfirmationDialogViewModel("Title", "Message");

        var result = service.ShowAsync(dialog);

        Assert.Same(dialog, service.ActiveDialog);
        Assert.False(result.IsCompleted);

        dialog.ConfirmCommand.Execute(null);

        Assert.True(await result);
        Assert.Null(service.ActiveDialog);
    }

    [Fact]
    public async Task Nested_dialogs_restore_the_previous_dialog()
    {
        var service = new DialogService();
        var outer = new ConfirmationDialogViewModel("Outer", "");
        var inner = new ConfirmationDialogViewModel("Inner", "");

        var outerResult = service.ShowAsync(outer);
        var innerResult = service.ShowAsync(inner);
        Assert.Same(inner, service.ActiveDialog);

        inner.CancelCommand.Execute(null);
        await innerResult;
        Assert.Same(outer, service.ActiveDialog);

        outer.ConfirmCommand.Execute(null);
        await outerResult;
        Assert.Null(service.ActiveDialog);
    }
}
