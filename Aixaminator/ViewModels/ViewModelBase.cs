using CommunityToolkit.Mvvm.ComponentModel;

namespace Aixaminator.ViewModels;

/// <summary>
/// Base class for view models. Derives from <see cref="ObservableValidator"/> so view models can use
/// data annotations with <c>[NotifyDataErrorInfo]</c>; Avalonia displays the resulting errors next to inputs.
/// </summary>
public abstract class ViewModelBase : ObservableValidator;

/// <summary>A top-level page shown in the main window.</summary>
public abstract class PageViewModel : ViewModelBase
{
    /// <summary>Loads the page's data; called by the navigation service once the page is displayed.</summary>
    public virtual Task LoadAsync() => Task.CompletedTask;
}
