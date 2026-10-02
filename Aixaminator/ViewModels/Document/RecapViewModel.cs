using System.Collections.ObjectModel;
using System.Text;
using Aixaminator.Data;
using Aixaminator.Features;
using Aixaminator.Models;
using Aixaminator.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aixaminator.ViewModels.Document;

/// <summary>
/// Recap mode: generates a multiple-choice quiz from key sentences of the selected parts.
/// </summary>
public sealed partial class RecapViewModel : ViewModelBase
{
    private readonly DocumentSession _session;
    private readonly IAiConnection _ai;
    private readonly Random _random;

    public RecapViewModel(DocumentSession session, IAiConnection ai, Random? random = null)
    {
        _session = session;
        _ai = ai;
        _random = random ?? Random.Shared;
        foreach (var part in session.VisibleParts)
        {
            PartOptions.Add(new RecapPartOptionViewModel(part, this));
        }
    }

    /// <summary>The visible parts; read parts are selected initially.</summary>
    public ObservableCollection<RecapPartOptionViewModel> PartOptions { get; } = [];

    public bool HasParts => PartOptions.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GenerateButtonText))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    public partial bool IsGenerating { get; private set; }

    public string GenerateButtonText => IsGenerating ? "Creating Recap" : "Create Recap";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; private set; }

    public bool HasError => ErrorMessage is not null;

    public ObservableCollection<QuizQuestionViewModel> Questions { get; } = [];

    public bool HasQuiz => Questions.Count > 0;

    internal void OnPartSelectionChanged() => GenerateCommand.NotifyCanExecuteChanged();

    private bool CanGenerate() => !IsGenerating && PartOptions.Any(p => p.IsSelected);

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        IsGenerating = true;
        ErrorMessage = null;
        Questions.Clear();
        OnPropertyChanged(nameof(HasQuiz));
        try
        {
            var highlights = await ExtractHighlightsAsync();
            if (string.IsNullOrWhiteSpace(highlights))
            {
                ErrorMessage = "The selected parts don't contain enough text to create a recap.";
                return;
            }

            var quiz = await _ai.GenerateQuizAsync(_session.Name, _session.Description, highlights);
            foreach (var question in quiz.Content.Questions)
            {
                Questions.Add(new QuizQuestionViewModel(question, Shuffle([.. question.IncorrectAnswers, question.CorrectAnswer])));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsGenerating = false;
            OnPropertyChanged(nameof(HasQuiz));
        }
    }

    private async Task<string> ExtractHighlightsAsync()
    {
        var highlights = new StringBuilder();
        foreach (var option in PartOptions.Where(p => p.IsSelected))
        {
            var text = await _session.GetPartTextAsync(option.PartNumber);
            foreach (var sentence in TextSummarizer.ExtractKeyHighlights(text, maxHighlights: 5, maxLength: 1000))
            {
                highlights.AppendLine($"- {sentence}");
            }
        }
        return highlights.ToString();
    }

    private List<string> Shuffle(List<string> items)
    {
        // Fisher-Yates
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
        return items;
    }
}

public sealed partial class RecapPartOptionViewModel : ViewModelBase
{
    private readonly RecapViewModel _owner;

    internal RecapPartOptionViewModel(DocumentPart part, RecapViewModel owner)
    {
        _owner = owner;
        PartNumber = part.PartNumber;
        DisplayName = part.DisplayName;
        IsSelected = part.IsRead;
    }

    public int PartNumber { get; }

    public string DisplayName { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value) => _owner?.OnPartSelectionChanged();
}

public sealed partial class QuizQuestionViewModel : ViewModelBase
{
    public QuizQuestionViewModel(QuizQuestion question, IReadOnlyList<string> answers)
    {
        Text = question.Text;
        CorrectAnswer = question.CorrectAnswer;
        Answers = answers.Select(a => new QuizAnswerViewModel(this, a, a == question.CorrectAnswer)).ToList();
    }

    public string Text { get; }

    public string CorrectAnswer { get; }

    /// <summary>The correct answer and the incorrect ones, in random order.</summary>
    public IReadOnlyList<QuizAnswerViewModel> Answers { get; }

    /// <summary>Unique name for the question's radio button group.</summary>
    public string GroupName { get; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnswered), nameof(IsAnsweredCorrectly), nameof(Feedback))]
    public partial QuizAnswerViewModel? SelectedAnswer { get; private set; }

    public bool IsAnswered => SelectedAnswer is not null;

    public bool IsAnsweredCorrectly => SelectedAnswer?.IsCorrect == true;

    public string? Feedback => SelectedAnswer switch
    {
        null => null,
        { IsCorrect: true } => "Correct!",
        _ => $"Not quite. The answer is: {CorrectAnswer}",
    };

    internal void Select(QuizAnswerViewModel answer)
    {
        foreach (var other in Answers.Where(a => a != answer))
        {
            other.IsSelected = false;
        }
        SelectedAnswer = answer;
    }
}

public sealed partial class QuizAnswerViewModel : ViewModelBase
{
    private readonly QuizQuestionViewModel _question;

    internal QuizAnswerViewModel(QuizQuestionViewModel question, string text, bool isCorrect)
    {
        _question = question;
        Text = text;
        IsCorrect = isCorrect;
    }

    public string Text { get; }

    public bool IsCorrect { get; }

    public string GroupName => _question.GroupName;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            _question.Select(this);
        }
    }
}
