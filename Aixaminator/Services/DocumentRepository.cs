using Aixaminator.Data;
using Microsoft.EntityFrameworkCore;

namespace Aixaminator.Services;

/// <summary>A document to add to the library.</summary>
/// <param name="Parts">The text of each part, in order. At least one is required.</param>
public sealed record NewDocument(string Name, string? Description, IReadOnlyList<string> Parts);

/// <summary>A document as listed in the library. Counts only include visible parts.</summary>
public sealed record DocumentSummary(Guid Id, string Name, string? Description, int PartCount, int ReadCount)
{
    public int Progress => PartCount == 0 ? 0 : 100 * ReadCount / PartCount;
}

/// <summary>
/// Persistence for documents, their parts and notes.
/// Entities returned are detached snapshots; pass them back to the update methods to save changes.
/// </summary>
public interface IDocumentRepository
{
    Task<IReadOnlyList<DocumentSummary>> GetLibraryAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a document with its parts (ordered by number) and notes (ordered by creation).</summary>
    Task<Document?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Document> CreateAsync(NewDocument document, CancellationToken cancellationToken = default);

    /// <returns>False if the document didn't exist.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateDetailsAsync(Guid id, string name, string? description, CancellationToken cancellationToken = default);

    Task UpdatePartAsync(DocumentPart part, CancellationToken cancellationToken = default);

    /// <returns>The note, with its <see cref="Note.Id"/> assigned.</returns>
    Task<Note> AddNoteAsync(Note note, CancellationToken cancellationToken = default);

    Task DeleteNoteAsync(int noteId, CancellationToken cancellationToken = default);

    Task<string> GetPartTextAsync(Guid documentId, int partNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the text of some parts. Note locations in those parts no longer point at the right text,
    /// so they are cleared.
    /// </summary>
    Task SavePartTextsAsync(Guid documentId, IReadOnlyDictionary<int, string> texts, CancellationToken cancellationToken = default);
}

public sealed class DocumentRepository(
    IDbContextFactory<AppDbContext> contextFactory,
    IDocumentContentStore content) : IDocumentRepository
{
    public async Task<IReadOnlyList<DocumentSummary>> GetLibraryAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Documents
            .OrderBy(d => d.CreatedAtUtc)
            .ThenBy(d => d.Name)
            .Select(d => new DocumentSummary(
                d.Id,
                d.Name,
                d.Description,
                d.Parts.Count(p => !p.Hidden),
                d.Parts.Count(p => !p.Hidden && p.IsRead)))
            .ToListAsync(cancellationToken);
    }

    public async Task<Document?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Documents
            .AsNoTracking()
            .AsSplitQuery()
            .Include(d => d.Parts.OrderBy(p => p.PartNumber))
            .Include(d => d.Notes.OrderBy(n => n.Id))
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<Document> CreateAsync(NewDocument newDocument, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newDocument.Name))
        {
            throw new ArgumentException("A document needs a name.", nameof(newDocument));
        }

        if (newDocument.Parts.Count == 0)
        {
            throw new ArgumentException("A document needs at least one part.", nameof(newDocument));
        }

        var document = new Document
        {
            Id = Guid.NewGuid(),
            Name = newDocument.Name.Trim(),
            Description = NullIfBlank(newDocument.Description),
        };
        document.Parts = newDocument.Parts.Select((_, i) => new DocumentPart { DocumentId = document.Id, PartNumber = i }).ToList();

        try
        {
            for (var i = 0; i < newDocument.Parts.Count; i++)
            {
                await content.WritePartAsync(document.Id, i, newDocument.Parts[i], cancellationToken);
            }

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            context.Documents.Add(document);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            content.DeleteDocument(document.Id);
            throw;
        }

        return document;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Parts and notes are removed by cascading foreign keys.
        var deleted = await context.Documents.Where(d => d.Id == id).ExecuteDeleteAsync(cancellationToken);
        content.DeleteDocument(id);
        return deleted > 0;
    }

    public async Task UpdateDetailsAsync(Guid id, string name, string? description, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A document needs a name.", nameof(name));
        }

        var trimmedName = name.Trim();
        var normalisedDescription = NullIfBlank(description);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Documents
            .Where(d => d.Id == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(d => d.Name, trimmedName)
                    .SetProperty(d => d.Description, normalisedDescription),
                cancellationToken);
    }

    public async Task UpdatePartAsync(DocumentPart part, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.DocumentParts.Update(part);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Note> AddNoteAsync(Note note, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Notes.Add(note);
        await context.SaveChangesAsync(cancellationToken);
        return note;
    }

    public async Task DeleteNoteAsync(int noteId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Notes.Where(n => n.Id == noteId).ExecuteDeleteAsync(cancellationToken);
    }

    public Task<string> GetPartTextAsync(Guid documentId, int partNumber, CancellationToken cancellationToken = default) =>
        content.ReadPartAsync(documentId, partNumber, cancellationToken);

    public async Task SavePartTextsAsync(Guid documentId, IReadOnlyDictionary<int, string> texts, CancellationToken cancellationToken = default)
    {
        foreach (var (partNumber, text) in texts)
        {
            await content.WritePartAsync(documentId, partNumber, text, cancellationToken);
        }

        var partNumbers = texts.Keys.ToList();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var stale = await context.Notes
            .Where(n => n.DocumentId == documentId
                        && n.DocumentPartNumber != null
                        && partNumbers.Contains(n.DocumentPartNumber.Value)
                        && n.Location != null)
            .ToListAsync(cancellationToken);
        foreach (var note in stale)
        {
            note.Location = null;
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
