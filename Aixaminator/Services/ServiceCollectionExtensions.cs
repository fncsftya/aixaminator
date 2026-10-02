using Aixaminator.Data;
using Aixaminator.ViewModels;
using Aixaminator.ViewModels.Home;
using Avalonia.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aixaminator.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the application's services and view models.</summary>
    /// <param name="paths">Where the application stores its data.</param>
    /// <param name="topLevel">Returns the main window, for platform services such as file pickers.</param>
    public static IServiceCollection AddAixaminator(this IServiceCollection services, AppPaths paths, Func<TopLevel?> topLevel)
    {
        services.AddLogging(logging =>
        {
#if DEBUG
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Debug);
#endif
        });
        services.AddHttpClient();

        // Storage
        services.AddSingleton(paths);
        services.AddSingleton<IAppPaths>(paths);
        services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(paths.ConnectionString));
        services.AddSingleton<IDocumentContentStore, FileDocumentContentStore>();
        services.AddSingleton<IDocumentRepository, DocumentRepository>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAppInitializer, AppInitializer>();

        // Features
        services.AddSingleton<IAiConnection, AiConnection>();
        services.AddSingleton<IWikipediaClient, WikipediaClient>();
        services.AddSingleton<IDocumentImporter, DocumentImporter>();
        services.AddSingleton<IFilePickerService>(_ => new AvaloniaFilePickerService(topLevel));

        // UI infrastructure
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());
        services.AddSingleton<IViewModelFactory, ViewModelFactory>();

        // View models (DocumentViewModel is created per document by the navigation service)
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<DocumentLibraryViewModel>();
        services.AddTransient<CreateDocumentViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services;
    }
}
