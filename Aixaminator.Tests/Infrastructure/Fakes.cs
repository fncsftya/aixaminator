using System.Text;
using Aixaminator.Models;
using Aixaminator.Services;
using Aixaminator.ViewModels;
using Aixaminator.ViewModels.Dialogs;

namespace Aixaminator.Tests.Infrastructure;

/// <summary>Answers dialogs immediately using <see cref="Respond"/> and records what was shown.</summary>
public sealed class FakeDialogService : IDialogService
{
    public List<DialogViewModel> Shown { get; } = [];

    /// <summary>Returns the result for a dialog. By default confirmations are accepted and other dialogs cancelled.</summary>
    public Func<DialogViewModel, object?> Respond { get; set; } = dialog => dialog is ConfirmationDialogViewModel ? true : null;

    public Task<TResult?> ShowAsync<TResult>(DialogViewModel<TResult> dialog)
    {
        Shown.Add(dialog);
        return Task.FromResult((TResult?)Respond(dialog));
    }
}

public sealed class FakeNavigationService : INavigationService
{
    public PageViewModel? CurrentPage => null;

    public int HomeCount { get; private set; }

    public List<Guid> OpenedDocuments { get; } = [];

    public Task GoHomeAsync()
    {
        HomeCount++;
        return Task.CompletedTask;
    }

    public Task OpenDocumentAsync(Guid documentId)
    {
        OpenedDocuments.Add(documentId);
        return Task.CompletedTask;
    }
}

public sealed class FakeAiConnection : IAiConnection
{
    public Func<string, Task<string>> Clean { get; set; } = text => Task.FromResult(text);

    public Func<string, Task<Quiz>> Quiz { get; set; } = _ => Task.FromResult(CreateQuiz(1));

    public Func<string, string, Task<bool>> Test { get; set; } = (_, _) => Task.FromResult(true);

    public List<string> CleanRequests { get; } = [];

    public List<(string Name, string? Description, string Highlights)> QuizRequests { get; } = [];

    public Task<bool> TestConnectionAsync(string provider, string apiKey, CancellationToken cancellationToken = default) =>
        Test(provider, apiKey);

    public Task<string> CleanTextAsync(string text, CancellationToken cancellationToken = default)
    {
        CleanRequests.Add(text);
        return Clean(text);
    }

    public Task<Quiz> GenerateQuizAsync(string documentName, string? documentDescription, string highlights, CancellationToken cancellationToken = default)
    {
        QuizRequests.Add((documentName, documentDescription, highlights));
        return Quiz(highlights);
    }

    public static Quiz CreateQuiz(int questions) => new()
    {
        Content = new QuizContent
        {
            Questions = Enumerable.Range(1, questions).Select(i => new QuizQuestion
            {
                Text = $"Question {i}?",
                CorrectAnswer = $"Right {i}",
                IncorrectAnswers = [$"Wrong {i}a", $"Wrong {i}b", $"Wrong {i}c"],
            }).ToList(),
        },
    };
}

public sealed class FakePickedFile(string name, byte[] content) : IPickedFile
{
    public FakePickedFile(string name, string text) : this(name, Encoding.UTF8.GetBytes(text))
    {
    }

    public string Name { get; } = name;

    public Task<Stream> OpenReadAsync() => Task.FromResult<Stream>(new MemoryStream(content));
}

public sealed class FakeFilePicker : IFilePickerService
{
    public IPickedFile? File { get; set; }

    public Task<IPickedFile?> PickDocumentAsync() => Task.FromResult(File);
}
