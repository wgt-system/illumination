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
    { _content = content; _status = status; _refresh = refresh; }

    public ObservableCollection<EditorHintRow> Hints { get; } = [];
    public ObservableCollection<EditorChoiceRow> DirectChoices { get; } = [];
    public ObservableCollection<EditorChoiceRow> AssistanceChoices { get; } = [];
    public ObservableCollection<string> AcceptedAnswers { get; } = [];
    public IReadOnlyList<LearningItemResponseMode> ResponseModes { get; } = Enum.GetValues<LearningItemResponseMode>();
    public bool IsEditing => _editingId.HasValue;
    public bool IsSelection => ResponseMode == LearningItemResponseMode.Selection;
    public bool IsShortText => ResponseMode == LearningItemResponseMode.ShortText;
    public string FormTitle => IsEditing ? "Edit Learning Item" : "Create Learning Item";

    [ObservableProperty] private string _prompt = string.Empty;
    [ObservableProperty] private string _referenceSolution = string.Empty;
    [ObservableProperty] private LearningItemResponseMode _responseMode = LearningItemResponseMode.SelfAssessed;
    [ObservableProperty] private bool _lowInteractionEligible;
    [ObservableProperty] private string _validationMessage = string.Empty;

    partial void OnResponseModeChanged(LearningItemResponseMode value)
    { OnPropertyChanged(nameof(IsSelection)); OnPropertyChanged(nameof(IsShortText)); }

    public void BeginCreate()
    { _editingId = null; Prompt = string.Empty; ReferenceSolution = string.Empty; ResponseMode = LearningItemResponseMode.SelfAssessed; LowInteractionEligible = false; ValidationMessage = string.Empty; ClearCollections(); OnPropertyChanged(nameof(IsEditing)); OnPropertyChanged(nameof(FormTitle)); }

    public async Task BeginEditAsync(Guid id)
    {
        try
        {
            var item = await _content.GetLearningItemAsync(id); _editingId = id; Prompt = item.Prompt; ReferenceSolution = item.ReferenceSolution; ResponseMode = item.ResponseMode; LowInteractionEligible = item.LowInteractionEligible; ValidationMessage = string.Empty; ClearCollections();
            foreach (var hint in item.Hints) Hints.Add(new(hint.Text));
            foreach (var choice in item.DirectAnswerChoices) DirectChoices.Add(new(choice.Text, choice.IsCorrect, choice.Id));
            foreach (var choice in item.AssistanceAnswerChoices) AssistanceChoices.Add(new(choice.Text, choice.IsCorrect, choice.Id));
            foreach (var answer in item.AcceptedShortAnswers) AcceptedAnswers.Add(answer);
            OnPropertyChanged(nameof(IsEditing)); OnPropertyChanged(nameof(FormTitle));
        }
        catch (Exception ex) { _status(ex.Message); }
    }

    [RelayCommand] private void New() => BeginCreate();
    [RelayCommand] private void Cancel() => BeginCreate();
    [RelayCommand] private void AddHint() => Hints.Add(new(string.Empty));
    [RelayCommand] private void RemoveHint(EditorHintRow row) => Hints.Remove(row);
    [RelayCommand] private void AddDirectChoice() => DirectChoices.Add(new(string.Empty, false, $"choice-{Guid.NewGuid():N}"));
    [RelayCommand] private void RemoveDirectChoice(EditorChoiceRow row) => DirectChoices.Remove(row);
    [RelayCommand] private void AddAssistanceChoice() => AssistanceChoices.Add(new(string.Empty, false, $"assistance-{Guid.NewGuid():N}"));
    [RelayCommand] private void RemoveAssistanceChoice(EditorChoiceRow row) => AssistanceChoices.Remove(row);
    [RelayCommand] private void AddAcceptedAnswer() => AcceptedAnswers.Add(string.Empty);
    [RelayCommand] private void RemoveAcceptedAnswer(string answer) => AcceptedAnswers.Remove(answer);

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidationMessage = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(Prompt) || string.IsNullOrWhiteSpace(ReferenceSolution)) throw new ContentValidationException("Prompt and Reference Solution are required.", new ArgumentException());
            var hints = Hints.Where(x => !string.IsNullOrWhiteSpace(x.Text)).Select(x => new HintInput(x.Text)).ToArray();
            var direct = DirectChoices.Select(x => new AnswerChoiceInput(x.Text, x.IsCorrect, x.Id)).ToArray();
            var assistance = AssistanceChoices.Select(x => new AnswerChoiceInput(x.Text, x.IsCorrect, x.Id)).ToArray();
            var answers = AcceptedAnswers.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            if (_editingId is Guid id) await _content.UpdateLearningItemAsync(id, new UpdateLearningItemCommand(Prompt, ReferenceSolution, ResponseMode, hints, direct, assistance, answers, LowInteractionEligible));
            else await _content.CreateLearningItemAsync(new CreateLearningItemCommand(Prompt, ReferenceSolution, ResponseMode, hints, direct, assistance, answers, LowInteractionEligible));
            await _refresh(); _status(IsEditing ? "Learning Item updated." : "Learning Item created."); BeginCreate();
        }
        catch (Exception ex) { ValidationMessage = ex.Message; _status(ex.Message); }
    }

    private void ClearCollections() { Hints.Clear(); DirectChoices.Clear(); AssistanceChoices.Clear(); AcceptedAnswers.Clear(); }
}

public sealed partial class EditorHintRow(string text) : ObservableObject
{ [ObservableProperty] private string _text = text; }
public sealed partial class EditorChoiceRow(string text, bool isCorrect, string id) : ObservableObject
{ [ObservableProperty] private string _text = text; [ObservableProperty] private bool _isCorrect = isCorrect; public string Id { get; } = id; }