using CommunityToolkit.Mvvm.Input;

namespace Aixaminator.ViewModels.Dialogs;

/// <summary>Asks the user to confirm an action. The result is true when confirmed.</summary>
public sealed partial class ConfirmationDialogViewModel(
    string title,
    string message,
    string confirmText = "Continue",
    string cancelText = "Return",
    bool isDestructive = false) : DialogViewModel<bool>
{
    public override string Title { get; } = title;

    public string Message { get; } = message;

    public string ConfirmText { get; } = confirmText;

    public string CancelText { get; } = cancelText;

    /// <summary>Whether confirming deletes something, so the confirm button is styled as dangerous.</summary>
    public bool IsDestructive { get; } = isDestructive;

    [RelayCommand]
    private void Confirm() => Close(true);
}
