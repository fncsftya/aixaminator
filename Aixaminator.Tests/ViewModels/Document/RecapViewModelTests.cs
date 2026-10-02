using Aixaminator.Services;
using Aixaminator.Tests.Infrastructure;
using Aixaminator.ViewModels.Document;

namespace Aixaminator.Tests.ViewModels.Document;

public sealed class RecapViewModelTests : IAsyncLifetime
{
    private const string LongText =
        "The Roman Empire was one of the largest empires in ancient history. " +
        "It was founded in 27 BC when Augustus became the first emperor of Rome.";

    private DocumentFixture _f = null!;

    public async ValueTask InitializeAsync() => _f = await DocumentFixture.CreateAsync();

    public ValueTask DisposeAsync() => _f.DisposeAsync();

    private async Task<(DocumentViewModel Vm, RecapViewModel Recap)> RecapAsync(params int[] readParts)
    {
        var document = await _f.Database.AddDocumentAsync("History", "About Rome", LongText, "Second part about the Senate and the people of Rome.", "Third.");
        await _f.MarkReadAsync(document, readParts);
        var vm = await _f.OpenAsync(document.Id);
        vm.ToggleRecapCommand.Execute(null);
        return (vm, Assert.IsType<RecapViewModel>(vm.ModeContent));
    }

    [Fact]
    public async Task Read_parts_are_selected_by_default()
    {
        var (_, recap) = await RecapAsync(0, 2);

        Assert.True(recap.HasParts);
        Assert.Equal(["Part 1", "Part 2", "Part 3"], recap.PartOptions.Select(p => p.DisplayName));
        Assert.Equal([true, false, true], recap.PartOptions.Select(p => p.IsSelected));
    }

    [Fact]
    public async Task A_recap_needs_at_least_one_part()
    {
        var (_, recap) = await RecapAsync();

        Assert.False(recap.GenerateCommand.CanExecute(null));

        recap.PartOptions[0].IsSelected = true;

        Assert.True(recap.GenerateCommand.CanExecute(null));
    }

    [Fact]
    public async Task Generating_a_quiz_sends_key_sentences_of_the_selected_parts()
    {
        var (_, recap) = await RecapAsync(0);
        _f.Ai.Quiz = _ => Task.FromResult(FakeAiConnection.CreateQuiz(2));

        await recap.GenerateCommand.ExecuteAsync(null);

        var request = Assert.Single(_f.Ai.QuizRequests);
        Assert.Equal("History", request.Name);
        Assert.Equal("About Rome", request.Description);
        Assert.Contains("- The Roman Empire was one of the largest empires in ancient history.", request.Highlights);
        Assert.DoesNotContain("Senate", request.Highlights);
        Assert.True(recap.HasQuiz);
        Assert.False(recap.HasError);
        Assert.Equal(["Question 1?", "Question 2?"], recap.Questions.Select(q => q.Text));
        Assert.All(recap.Questions, q =>
        {
            Assert.Equal(4, q.Answers.Count);
            Assert.Single(q.Answers, a => a.IsCorrect);
        });
        Assert.Equal("Create Recap", recap.GenerateButtonText);
    }

    [Fact]
    public async Task Answers_are_shuffled()
    {
        var (_, recap) = await RecapAsync(0);
        _f.Ai.Quiz = _ => Task.FromResult(FakeAiConnection.CreateQuiz(20));

        await recap.GenerateCommand.ExecuteAsync(null);

        Assert.Contains(recap.Questions, q => !q.Answers[^1].IsCorrect);
    }

    [Fact]
    public async Task Choosing_an_answer_gives_feedback()
    {
        var (_, recap) = await RecapAsync(0);
        await recap.GenerateCommand.ExecuteAsync(null);
        var question = recap.Questions.Single();
        Assert.False(question.IsAnswered);

        question.Answers.First(a => !a.IsCorrect).IsSelected = true;

        Assert.True(question.IsAnswered);
        Assert.False(question.IsAnsweredCorrectly);
        Assert.Equal("Not quite. The answer is: Right 1", question.Feedback);

        question.Answers.Single(a => a.IsCorrect).IsSelected = true;

        Assert.True(question.IsAnsweredCorrectly);
        Assert.Equal("Correct!", question.Feedback);
    }

    [Fact]
    public async Task Failures_are_shown_and_can_be_retried()
    {
        var (_, recap) = await RecapAsync(0);
        _f.Ai.Quiz = _ => throw new InvalidOperationException("Unable to generate the quiz.");

        await recap.GenerateCommand.ExecuteAsync(null);

        Assert.True(recap.HasError);
        Assert.Equal("Unable to generate the quiz.", recap.ErrorMessage);
        Assert.False(recap.HasQuiz);

        _f.Ai.Quiz = _ => Task.FromResult(FakeAiConnection.CreateQuiz(1));
        await recap.GenerateCommand.ExecuteAsync(null);

        Assert.False(recap.HasError);
        Assert.True(recap.HasQuiz);
    }

    [Fact]
    public async Task Missing_ai_configuration_is_explained()
    {
        var (_, recap) = await RecapAsync(0);
        _f.Ai.Quiz = _ => throw new AiNotConfiguredException();

        await recap.GenerateCommand.ExecuteAsync(null);

        Assert.Equal("No AI provider is configured. Add one in Settings.", recap.ErrorMessage);
    }

    [Fact]
    public async Task Parts_without_enough_text_cannot_be_recapped()
    {
        var (_, recap) = await RecapAsync(2);

        await recap.GenerateCommand.ExecuteAsync(null);

        Assert.Empty(_f.Ai.QuizRequests);
        Assert.Equal("The selected parts don't contain enough text to create a recap.", recap.ErrorMessage);
    }

    [Fact]
    public async Task Shows_progress_while_generating()
    {
        var (_, recap) = await RecapAsync(0);
        var release = new TaskCompletionSource<Aixaminator.Models.Quiz>();
        _f.Ai.Quiz = _ => release.Task;

        var generating = recap.GenerateCommand.ExecuteAsync(null);

        Assert.True(recap.IsGenerating);
        Assert.Equal("Creating Recap", recap.GenerateButtonText);
        Assert.False(recap.GenerateCommand.CanExecute(null));

        release.SetResult(FakeAiConnection.CreateQuiz(1));
        await generating;
        Assert.False(recap.IsGenerating);
    }
}
