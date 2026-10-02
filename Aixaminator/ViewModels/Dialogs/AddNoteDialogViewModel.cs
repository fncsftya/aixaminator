using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aixaminator.ViewModels.Dialogs;

/// <summary>What the user entered in <see cref="AddNoteDialogViewModel"/>. <see cref="Answer"/> is only set for questions.</summary>
public sealed record NoteInput(string Text, string? Answer);

/// <summary>Collects a note, or a question and its answer, optionally about some selected text.</summary>
public sealed partial class AddNoteDialogViewModel(bool isQuestion, string? referenceText) : DialogViewModel<NoteInput>
{
    public bool IsQuestion { get; } = isQuestion;

    public override string Title => IsQuestion ? "Add a question" : "Add a note";

    /// <summary>The text selected in the document when the dialog was opened.</summary>
    public string? ReferenceText { get; } = string.IsNullOrWhiteSpace(referenceText) ? null : referenceText.Trim();

    public bool HasReferenceText => ReferenceText is not null;

    public string TextLabel => IsQuestion ? "Question" : "Content";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(AddNoteDialogViewModel), nameof(ValidateText))]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(AddNoteDialogViewModel), nameof(ValidateAnswer))]
    public partial string Answer { get; set; } = string.Empty;

    [RelayCommand]
    private void Add()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }

        Close(new NoteInput(Text.Trim(), IsQuestion ? Answer.Trim() : null));
    }

    public static ValidationResult? ValidateText(string text, ValidationContext context)
    {
        var dialog = (AddNoteDialogViewModel)context.ObjectInstance;
        return string.IsNullOrWhiteSpace(text)
            ? new ValidationResult($"You must provide some text for your {(dialog.IsQuestion ? "question" : "note")}.")
            : ValidationResult.Success;
    }

    public static ValidationResult? ValidateAnswer(string answer, ValidationContext context)
    {
        var dialog = (AddNoteDialogViewModel)context.ObjectInstance;
        return dialog.IsQuestion && string.IsNullOrWhiteSpace(answer)
            ? new ValidationResult("You must provide an answer for your question.")
            : ValidationResult.Success;
    }
}
