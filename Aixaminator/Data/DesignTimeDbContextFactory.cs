using Aixaminator.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aixaminator.Data;

/// <summary>
/// Used by the EF Core tooling (<c>dotnet ef migrations add ...</c>) to create a context at design time.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var paths = AppPaths.CreateDefault();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(paths.ConnectionString)
            .Options;
        return new AppDbContext(options);
    }
}
