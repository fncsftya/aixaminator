using Aixaminator.Data;
using Aixaminator.Services;

namespace Aixaminator.ViewModels.Document;

/// <summary>
/// The state of an open document, shared by the view models of the document page, and the operations that change it.
/// <para>
/// Local state is updated immediately and changes are written to the repository one at a time, in order,
/// so rapid edits (e.g. typing a part's name) can't be saved out of order. Failures are reported through
/// <see cref="ErrorOccurred"/> rather than thrown.
/// </para>
/// </summary>
public sealed class DocumentSession
{
    private readonly IDocumentRepository _repository;
    private readonly List<DocumentPart> _parts;
    private readonly List<Note> _notes;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public DocumentSession(Data.Document document, IDocumentRepository repository)
    {
        _repository = repository;
        DocumentId = document.Id;
        Name = document.Name;
        Description = document.Description;
        _parts = document.Parts.OrderBy(p => p.PartNumber).ToList();
        _notes = document.Notes.OrderBy(n => n.Id).ToList();
    }

    public Guid DocumentId { get; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    /// <summary>All parts, including hidden ones, ordered by number.</summary>
    public IReadOnlyList<DocumentPart> Parts => _parts;

    public IReadOnlyList<DocumentPart> VisibleParts => _parts.Where(p => !p.Hidden).ToList();

    /// <summary>All notes, ordered by creation.</summary>
    public IReadOnlyList<Note> Notes => _notes;

    /// <summary>Percentage of visible parts that have been read.</summary>
    public int Progress
    {
        get
        {
            var visible = VisibleParts;
            return visible.Count == 0 ? 0 : 100 * visible.Count(p => p.IsRead) / visible.Count;
        }
    }

    public event EventHandler? DetailsChanged;

    /// <summary>Raised when any part's settings, visibility or read state changes.</summary>
    public event EventHandler? PartsChanged;

    public event EventHandler? NotesChanged;

    /// <summary>Raised with a user-facing message when loading or saving fails.</summary>
    public event EventHandler<string>? ErrorOccurred;

    public DocumentPart? GetPart(int partNumber) => _parts.FirstOrDefault(p => p.PartNumber == partNumber);

    public string GetPartName(int? partNumber) =>
        partNumber is { } number ? GetPart(number)?.DisplayName ?? DocumentPart.DefaultName(number) : "Document";

    public IEnumerable<Note> NotesForPart(int partNumber) => _notes.Where(n => n.DocumentPartNumber == partNumber);

    public bool HasNotes(int partNumber) => NotesForPart(partNumber).Any();

    public async Task<string> GetPartTextAsync(int partNumber)
    {
        try
        {
            return await _repository.GetPartTextAsync(DocumentId, partNumber);
        }
        catch (Exception ex)
        {
            ReportError($"The text of {GetPartName(partNumber)} could not be loaded", ex);
            return string.Empty;
        }
    }

    /// <returns>The saved note, or null if saving failed.</returns>
    public async Task<Note?> AddNoteAsync(Note note)
    {
        note.DocumentId = DocumentId;
        var saved = await WriteAsync("Your note could not be saved", () => _repository.AddNoteAsync(note));
        if (!saved)
        {
            return null;
        }

        _notes.Add(note);
        NotesChanged?.Invoke(this, EventArgs.Empty);
        return note;
    }

    public Task DeleteNoteAsync(Note note)
    {
        if (_notes.Remove(note))
        {
            NotesChanged?.Invoke(this, EventArgs.Empty);
        }
        return WriteAsync("The note could not be deleted", () => _repository.DeleteNoteAsync(note.Id));
    }

    /// <summary>Saves the current state of a part after its properties have been changed.</summary>
    public Task SavePartAsync(DocumentPart part)
    {
        PartsChanged?.Invoke(this, EventArgs.Empty);
        return WriteAsync($"The changes to {part.DisplayName} could not be saved", () => _repository.UpdatePartAsync(part));
    }

    public Task SetPartReadAsync(DocumentPart part, bool isRead)
    {
        if (part.IsRead == isRead)
        {
            return Task.CompletedTask;
        }

        part.IsRead = isRead;
        return SavePartAsync(part);
    }

    /// <param name="name">Must not be blank.</param>
    public Task UpdateDetailsAsync(string name, string? description)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description;
        DetailsChanged?.Invoke(this, EventArgs.Empty);

        var (savedName, savedDescription) = (Name, Description);
        return WriteAsync("The document details could not be saved",
            () => _repository.UpdateDetailsAsync(DocumentId, savedName, savedDescription));
    }

    /// <summary>Replaces the text of some parts; notes in those parts lose their (now invalid) locations.</summary>
    /// <returns>False if saving failed.</returns>
    public async Task<bool> SavePartTextsAsync(IReadOnlyDictionary<int, string> texts)
    {
        if (texts.Count == 0)
        {
            return true;
        }

        var saved = await WriteAsync("Your edits could not be saved", () => _repository.SavePartTextsAsync(DocumentId, texts));
        if (saved)
        {
            foreach (var note in _notes.Where(n => n.DocumentPartNumber is { } part && texts.ContainsKey(part)))
            {
                note.Location = null;
            }
            NotesChanged?.Invoke(this, EventArgs.Empty);
        }
        return saved;
    }

    /// <summary>Completes once every change made so far has been written.</summary>
    public async Task WhenIdleAsync()
    {
        await _writeLock.WaitAsync();
        _writeLock.Release();
    }

    private async Task<bool> WriteAsync(string failureMessage, Func<Task> write)
    {
        await _writeLock.WaitAsync();
        try
        {
            await write();
            return true;
        }
        catch (Exception ex)
        {
            ReportError(failureMessage, ex);
            return false;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void ReportError(string message, Exception ex) => ErrorOccurred?.Invoke(this, $"{message}: {ex.Message}");
}
