using Aixaminator.ViewModels;
using Aixaminator.ViewModels.Document;
using Aixaminator.ViewModels.Home;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aixaminator.Services;

public interface INavigationService
{
    PageViewModel? CurrentPage { get; }

    /// <summary>Shows the home page (library, import and settings).</summary>
    Task GoHomeAsync();

    /// <summary>Opens a document in the reader.</summary>
    Task OpenDocumentAsync(Guid documentId);
}

/// <summary>Creates view models, resolving their dependencies from the container.</summary>
public interface IViewModelFactory
{
    T Create<T>(params object[] arguments) where T : ViewModelBase;
}

public sealed class ViewModelFactory(IServiceProvider services) : IViewModelFactory
{
    public T Create<T>(params object[] arguments) where T : ViewModelBase =>
        ActivatorUtilities.CreateInstance<T>(services, arguments);
}

public sealed partial class NavigationService(IViewModelFactory factory) : ObservableObject, INavigationService
{
    [ObservableProperty]
    public partial PageViewModel? CurrentPage { get; private set; }

    public Task GoHomeAsync() => NavigateAsync(factory.Create<HomeViewModel>());

    public Task OpenDocumentAsync(Guid documentId) => NavigateAsync(factory.Create<DocumentViewModel>(documentId));

    private async Task NavigateAsync(PageViewModel page)
    {
        var previous = CurrentPage;
        CurrentPage = page;
        (previous as IDisposable)?.Dispose();
        await page.LoadAsync();
    }
}
