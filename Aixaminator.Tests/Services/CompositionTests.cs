using Aixaminator.Services;
using Aixaminator.Tests.Infrastructure;
using Aixaminator.ViewModels;
using Aixaminator.ViewModels.Document;
using Aixaminator.ViewModels.Home;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Aixaminator.Tests.Services;

public sealed class CompositionTests : IAsyncLifetime
{
    private readonly TempDirectory _directory = new();
    private ServiceProvider _services = null!;

    public async ValueTask InitializeAsync()
    {
        _services = new ServiceCollection()
            .AddAixaminator(new AppPaths(_directory.Path), () => null)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        await _services.GetRequiredService<IAppInitializer>().InitializeAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        _directory.Dispose();
    }

    [Fact]
    public void Every_view_model_can_be_resolved()
    {
        Assert.NotNull(_services.GetRequiredService<MainWindowViewModel>());
        Assert.NotNull(_services.GetRequiredService<HomeViewModel>());
        Assert.NotNull(_services.GetRequiredService<IViewModelFactory>().Create<DocumentViewModel>(Guid.NewGuid()));
    }

    [Fact]
    public void Navigation_and_dialogs_are_shared_between_their_interfaces_and_the_main_window()
    {
        var main = _services.GetRequiredService<MainWindowViewModel>();

        Assert.Same(main.Navigation, _services.GetRequiredService<INavigationService>());
        Assert.Same(main.Dialogs, _services.GetRequiredService<IDialogService>());
    }

    [Fact]
    public async Task Navigating_loads_the_new_page_and_disposes_the_old_one()
    {
        var repository = _services.GetRequiredService<IDocumentRepository>();
        var document = await repository.CreateAsync(new NewDocument("Doc", null, ["text"]), TestContext.Current.CancellationToken);
        var navigation = _services.GetRequiredService<NavigationService>();

        await navigation.OpenDocumentAsync(document.Id);

        var page = Assert.IsType<DocumentViewModel>(navigation.CurrentPage);
        Assert.True(page.IsReady);
        var session = page.Session!;

        await navigation.GoHomeAsync();

        var home = Assert.IsType<HomeViewModel>(navigation.CurrentPage);
        Assert.Equal(["Doc"], home.Library.Documents.Select(d => d.Name));
        // the disposed page no longer reacts to the session
        await session.UpdateDetailsAsync("Renamed", null);
        Assert.Equal("Doc", page.Name);
    }
}
