using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed partial class LearningItemEditorViewModel : ObservableObject
{
    private readonly ContentManagementService _content;
    private readonly Action<string> _status;
    private readonly Func<Task> _refresh;
    private Guid? _editingId;

    public LearningItemEditorViewModel(ContentManagementService content, Action<string> status, Func<Task> refresh)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
    }

    public ObservableCollection<EditorHintRow> Hints { get; } = [];
    public ObservableCollection<EditorChoiceRow> DirectChoices { get; } = [];
    public ObservableCollection<EditorChoiceRow> AssistanceChoices { get; } = [];
    public ObservableCollection<EditorTextRow> AcceptedAnswers { get; } = [];
    public IReadOnlyList<LearningItemResponseMode> ResponseModes { get; } = Enum.GetValues<LearningItemResponseMode>();

    public bool IsEditing => _editingId.HasValue;
    public bool IsCreating => !IsEditing;
    public bool IsSelection => ResponseMode == LearningItemResponseMode.Selection;
    public bool IsShortText => ResponseMode == LearningItemResponseMode.ShortText;
    public bool HasAdvisorySuggestion => !string.IsNullOrWhiteSpace(AdvisorySuggestion);
    public string FormTitle => IsEditing ? "Edit Learning Item" : "Create Learning Item";

    [ObservableProperty] private string _prompt = string.Empty;
    [ObservableProperty] private string _referenceSolution = string.Empty;
    [ObservableProperty] private LearningItemResponseMode _responseMode = LearningItemResponseMode.SelfAssessed;
    [ObservableProperty] private bool _lowInteractionEligible;
    [ObservableProperty] private DeckPresentationItem? _selectedDeckPresentation;
    [ObservableProperty] private string _validationMessage = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasAdvisorySuggestion))]
    private string _advisorySuggestion = string.Empty;

    partial void OnResponseModeChanged(LearningItemResponseMode value)
    {
        OnPropertyChanged(nameof(IsSelection));
        OnPropertyChanged(nameof(IsShortText));
    }

    public void BeginCreate()
    {
        _editingId = null;
        Prompt = string.Empty;
        ReferenceSolution = string.Empty;
        ResponseMode = LearningItemResponseMode.SelfAssessed;
        LowInteractionEligible = false;
        SelectedDeckPresentation = null;
        ValidationMessage = string.Empty;
        AdvisorySuggestion = string.Empty;
        ClearCollections();
        NotifyEditorModeChanged();
    }

    public async Task BeginEditAsync(Guid id)
    {
        try
        {
            var item = await _content.GetLearningItemAsync(id);
            _editingId = id;
            Prompt = item.Prompt;
            ReferenceSolution = item.ReferenceSolution;
            ResponseMode = item.ResponseMode;
            LowInteractionEligible = item.LowInteractionEligible;
            SelectedDeckPresentation = null;
            ValidationMessage = string.Empty;
            AdvisorySuggestion = string.Empty;
            ClearCollections();
            foreach (var hint in item.Hints) Hints.Add(new(hint.Text));
            foreach (var choice in item.DirectAnswerChoices) DirectChoices.Add(new(choice.Text, choice.IsCorrect, choice.Id));
            foreach (var choice in item.AssistanceAnswerChoices) AssistanceChoices.Add(new(choice.Text, choice.IsCorrect, choice.Id));
            foreach (var answer in item.AcceptedShortAnswers) AcceptedAnswers.Add(new(answer));
            NotifyEditorModeChanged();
        }
        catch (Exception ex)
        {
            _status(ex.Message);
        }
    }

    public void ShowAdvisorySuggestion(string suggestion)
    {
        AdvisorySuggestion = suggestion?.Trim() ?? string.Empty;
    }

    [RelayCommand] private void New() => BeginCreate();
    [RelayCommand] private void Cancel() => BeginCreate();
    [RelayCommand] private void DismissAdvisorySuggestion() => AdvisorySuggestion = string.Empty;

    [RelayCommand] private void AddHint() => Hints.Add(new(string.Empty));
    [RelayCommand] private void RemoveHint(EditorHintRow row) => Hints.Remove(row);
    [RelayCommand] private void MoveHintUp(EditorHintRow row) => MoveUp(Hints, row);
    [RelayCommand] private void MoveHintDown(EditorHintRow row) => MoveDown(Hints, row);

    [RelayCommand] private void AddDirectChoice() => DirectChoices.Add(new(string.Empty, false, $"choice-{Guid.NewGuid():N}"));
    [RelayCommand] private void RemoveDirectChoice(EditorChoiceRow row) => DirectChoices.Remove(row);
    [RelayCommand] private void MoveDirectChoiceUp(EditorChoiceRow row) => MoveUp(DirectChoices, row);
    [RelayCommand] private void MoveDirectChoiceDown(EditorChoiceRow row) => MoveDown(DirectChoices, row);

    [RelayCommand] private void AddAssistanceChoice() => AssistanceChoices.Add(new(string.Empty, false, $"assistance-{Guid.NewGuid():N}"));
    [RelayCommand] private void RemoveAssistanceChoice(EditorChoiceRow row) => AssistanceChoices.Remove(row);
    [RelayCommand] private void MoveAssistanceChoiceUp(EditorChoiceRow row) => MoveUp(AssistanceChoices, row);
    [RelayCommand] private void MoveAssistanceChoiceDown(EditorChoiceRow row) => MoveDown(AssistanceChoices, row);

    [RelayCommand] private void AddAcceptedAnswer() => AcceptedAnswers.Add(new(string.Empty));
    [RelayCommand] private void RemoveAcceptedAnswer(EditorTextRow row) => AcceptedAnswers.Remove(row);
    [RelayCommand] private void MoveAcceptedAnswerUp(EditorTextRow row) => MoveUp(AcceptedAnswers, row);
    [RelayCommand] private void MoveAcceptedAnswerDown(EditorTextRow row) => MoveDown(AcceptedAnswers, row);

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidationMessage = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(Prompt) || string.IsNullOrWhiteSpace(ReferenceSolution))
                throw new ContentValidationException("Question / task and reference answer are required.", new ArgumentException());

            var hints = Hints.Where(x => !string.IsNullOrWhiteSpace(x.Text)).Select(x => new HintInput(x.Text)).ToArray();
            var direct = ResponseMode == LearningItemResponseMode.Selection
                ? DirectChoices.Select(x => new AnswerChoiceInput(x.Text, x.IsCorrect, x.Id)).ToArray()
                : [];
            var assistance = AssistanceChoices.Select(x => new AnswerChoiceInput(x.Text, x.IsCorrect, x.Id)).ToArray();
            var answers = ResponseMode == LearningItemResponseMode.ShortText
                ? AcceptedAnswers.Where(x => !string.IsNullOrWhiteSpace(x.Text)).Select(x => x.Text).ToArray()
                : [];

            var wasEditing = IsEditing;
            if (_editingId is Guid id)
            {
                await _content.UpdateLearningItemAsync(id, new UpdateLearningItemCommand(
                    Prompt, ReferenceSolution, ResponseMode, hints, direct, assistance, answers, LowInteractionEligible));
            }
            else
            {
                var created = await _content.CreateLearningItemAsync(new CreateLearningItemCommand(
                    Prompt, ReferenceSolution, ResponseMode, hints, direct, assistance, answers, LowInteractionEligible));
                if (SelectedDeckPresentation is { } deck)
                    await _content.AddLearningItemToDeckAsync(deck.Id, created.Id);
            }

            await _refresh();
            _status(wasEditing ? "Learning Item updated." : SelectedDeckPresentation is { } selectedDeck
                ? $"Learning Item created in '{selectedDeck.DisplayName}'."
                : "Learning Item created.");
            BeginCreate();
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
            _status(ex.Message);
        }
    }

    private void NotifyEditorModeChanged()
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(IsCreating));
        OnPropertyChanged(nameof(FormTitle));
    }

    private static void MoveUp<T>(ObservableCollection<T> items, T item)
    {
        var index = items.IndexOf(item);
        if (index > 0) items.Move(index, index - 1);
    }

    private static void MoveDown<T>(ObservableCollection<T> items, T item)
    {
        var index = items.IndexOf(item);
        if (index >= 0 && index < items.Count - 1) items.Move(index, index + 1);
    }

    private void ClearCollections()
    {
        Hints.Clear();
        DirectChoices.Clear();
        AssistanceChoices.Clear();
        AcceptedAnswers.Clear();
    }
}

public sealed partial class EditorHintRow(string text) : ObservableObject
{
    [ObservableProperty] private string _text = text;
}

public sealed partial class EditorChoiceRow(string text, bool isCorrect, string id) : ObservableObject
{
    [ObservableProperty] private string _text = text;
    [ObservableProperty] private bool _isCorrect = isCorrect;
    public string Id { get; } = id;
}

public sealed partial class EditorTextRow(string text) : ObservableObject
{
    [ObservableProperty] private string _text = text;
}
