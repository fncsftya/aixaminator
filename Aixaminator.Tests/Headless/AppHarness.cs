using Aixaminator.Data;
using Aixaminator.Services;
using Aixaminator.Tests.Infrastructure;
using Aixaminator.ViewModels;
using Aixaminator.Views;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Aixaminator.Tests.Headless;

/// <summary>
/// The whole application — real DI container, database and views — in a headless window,
/// with storage in a temporary directory and fakes for the AI and the native file picker.
/// </summary>
public sealed class AppHarness : IAsyncDisposable
{
    private readonly TempDirectory _directory;
    private readonly ServiceProvider _services;

    private AppHarness(TempDirectory directory, ServiceProvider services, MainWindow window, FakeAiConnection ai, FakeFilePicker filePicker)
    {
        _directory = directory;
        _services = services;
        Window = window;
        Ai = ai;
        FilePicker = filePicker;
    }

    public MainWindow Window { get; }

    public MainWindowViewModel ViewModel => (MainWindowViewModel)Window.DataContext!;

    public IDocumentRepository Repository => _services.GetRequiredService<IDocumentRepository>();

    public ISettingsService Settings => _services.GetRequiredService<ISettingsService>();

    public AppPaths Paths => _services.GetRequiredService<AppPaths>();

    public FakeAiConnection Ai { get; }

    public FakeFilePicker FilePicker { get; }

    public static async Task<AppHarness> StartAsync()
    {
        var directory = new TempDirectory();
        var ai = new FakeAiConnection();
        var filePicker = new FakeFilePicker();
        MainWindow? window = null;

        var services = new ServiceCollection()
            .AddAixaminator(new AppPaths(directory.Path), () => window)
            .AddSingleton<IAiConnection>(ai)
            .AddSingleton<IFilePickerService>(filePicker)
            .BuildServiceProvider();

        window = new MainWindow { DataContext = services.GetRequiredService<MainWindowViewModel>(), Width = 1280, Height = 800 };
        var harness = new AppHarness(directory, services, window, ai, filePicker);
        window.Show();
        await harness.ViewModel.InitializeAsync();
        window.Settle();
        return harness;
    }

    public Task<Document> AddDocumentAsync(string name, params string[] parts) =>
        Repository.CreateAsync(new NewDocument(name, null, parts));

    public async ValueTask DisposeAsync()
    {
        Window.Close();
        await _services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        _directory.Dispose();
    }
}
