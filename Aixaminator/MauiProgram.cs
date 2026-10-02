using Aixaminator.Data;
using Aixaminator.Services;
using Aixaminator.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aixaminator;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // The database and settings live here; SQLite won't create missing parent directories.
        Directory.CreateDirectory(Constants.GetInternalFilepath());

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(Constants.DbConnection), ServiceLifetime.Transient);

        builder.Services.AddHttpClient();

        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IAiConnection, AiConnection>();

        var app = builder.Build();

        // Trying in Home.razor for now
        //using (var scope = app.Services.CreateScope())
        //{
        //    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        //    dbContext.Database.Migrate(); // Use EnsureCreated() if you don't use migrations
        //}

        return app;
    }
}
