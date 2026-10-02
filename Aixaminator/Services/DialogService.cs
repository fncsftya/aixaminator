using Aixaminator.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aixaminator.Services;

public interface IDialogService
{
    /// <summary>Shows a dialog and waits for it to close.</summary>
    /// <returns>The dialog's result, or default if it was cancelled.</returns>
    Task<TResult?> ShowAsync<TResult>(DialogViewModel<TResult> dialog);
}

public static class DialogServiceExtensions
{
    /// <returns>True if the user confirmed.</returns>
    public static async Task<bool> ConfirmAsync(this IDialogService dialogs, ConfirmationDialogViewModel dialog) =>
        await dialogs.ShowAsync(dialog);
}

/// <summary>
/// Shows dialogs as an overlay in the main window, which binds to <see cref="ActiveDialog"/>.
/// Dialogs opened while another is showing are stacked; the most recent one is active.
/// </summary>
public sealed partial class DialogService : ObservableObject, IDialogService
{
    private readonly List<DialogViewModel> _open = [];

    [ObservableProperty]
    public partial DialogViewModel? ActiveDialog { get; private set; }

    public async Task<TResult?> ShowAsync<TResult>(DialogViewModel<TResult> dialog)
    {
        _open.Add(dialog);
        ActiveDialog = dialog;
        try
        {
            return await dialog.Result;
        }
        finally
        {
            _open.Remove(dialog);
            ActiveDialog = _open.LastOrDefault();
        }
    }
}
