using Aixaminator.Services;
using Aixaminator.Tests.Infrastructure;

namespace Aixaminator.Tests.Services;

public sealed class FileDocumentContentStoreTests : IDisposable
{
    private readonly TempDirectory _directory = new();
    private readonly AppPaths _paths;
    private readonly FileDocumentContentStore _store;

    public FileDocumentContentStoreTests()
    {
        _paths = new AppPaths(_directory.Path);
        _store = new FileDocumentContentStore(_paths);
    }

    public void Dispose() => _directory.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Written_parts_can_be_read_back()
    {
        var id = Guid.NewGuid();

        await _store.WritePartAsync(id, 0, "Part zero", Token);
        await _store.WritePartAsync(id, 1, "Part one", Token);

        Assert.Equal("Part zero", await _store.ReadPartAsync(id, 0, Token));
        Assert.Equal("Part one", await _store.ReadPartAsync(id, 1, Token));
        Assert.True(File.Exists(Path.Combine(_paths.GetDocumentDirectory(id), "1.txt")));
    }

    [Fact]
    public async Task Missing_parts_read_as_empty()
    {
        Assert.Equal(string.Empty, await _store.ReadPartAsync(Guid.NewGuid(), 3, Token));
    }

    [Fact]
    public async Task Deleting_a_document_removes_its_directory()
    {
        var id = Guid.NewGuid();
        await _store.WritePartAsync(id, 0, "text", Token);

        _store.DeleteDocument(id);

        Assert.False(Directory.Exists(_paths.GetDocumentDirectory(id)));
        _store.DeleteDocument(id); // deleting again is a no-op
    }
}
