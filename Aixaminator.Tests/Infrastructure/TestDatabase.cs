using Aixaminator.Data;
using Aixaminator.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Aixaminator.Tests.Infrastructure;

/// <summary>
/// A migrated SQLite database and document store in a temporary directory, plus a repository over them.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly TempDirectory _directory = new();

    private TestDatabase()
    {
        Paths = new AppPaths(_directory.Path);
        Paths.EnsureCreated();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(Paths.ConnectionString)
            .Options;
        ContextFactory = new PooledDbContextFactory<AppDbContext>(options);
        Content = new FileDocumentContentStore(Paths);
        Repository = new DocumentRepository(ContextFactory, Content);
    }

    public AppPaths Paths { get; }

    public IDbContextFactory<AppDbContext> ContextFactory { get; }

    public FileDocumentContentStore Content { get; }

    public DocumentRepository Repository { get; }

    public static async Task<TestDatabase> CreateAsync()
    {
        var database = new TestDatabase();
        await using var context = await database.ContextFactory.CreateDbContextAsync();
        await context.Database.MigrateAsync();
        return database;
    }

    /// <summary>Creates a document with the given part texts.</summary>
    public Task<Document> AddDocumentAsync(string name = "Test document", string? description = null, params string[] parts) =>
        Repository.CreateAsync(new NewDocument(name, description, parts.Length == 0 ? ["Some text."] : parts));

    public void Dispose()
    {
        // Release pooled SQLite connections so the database file can be deleted.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        _directory.Dispose();
    }
}
