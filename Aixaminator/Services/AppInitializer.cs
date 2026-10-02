using Aixaminator.Data;
using Microsoft.EntityFrameworkCore;

namespace Aixaminator.Services;

/// <summary>Prepares storage before the UI is used.</summary>
public interface IAppInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed class AppInitializer(
    AppPaths paths,
    IDbContextFactory<AppDbContext> contextFactory,
    ISettingsService settings) : IAppInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureCreated();

        await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        await settings.LoadAsync(cancellationToken);
    }
}
