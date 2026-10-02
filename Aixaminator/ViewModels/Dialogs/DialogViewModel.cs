using CommunityToolkit.Mvvm.Input;

namespace Aixaminator.ViewModels.Dialogs;

/// <summary>A modal dialog shown over the current page by <see cref="Services.IDialogService"/>.</summary>
public abstract class DialogViewModel : ViewModelBase
{
    private IRelayCommand? _cancelCommand;

    public abstract string Title { get; }

    /// <summary>Closes the dialog without a result, e.g. when the user presses Escape or clicks outside it.</summary>
    public IRelayCommand CancelCommand => _cancelCommand ??= new RelayCommand(Cancel);

    public abstract void Cancel();
}

/// <summary>A dialog that produces a result of type <typeparamref name="TResult"/>.</summary>
public abstract class DialogViewModel<TResult> : DialogViewModel
{
    private readonly TaskCompletionSource<TResult?> _result = new();

    /// <summary>Completes when the dialog closes; default when it was cancelled.</summary>
    public Task<TResult?> Result => _result.Task;

    public bool IsClosed => _result.Task.IsCompleted;

    public override void Cancel() => Close(default);

    protected void Close(TResult? result) => _result.TrySetResult(result);
}
