using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed partial class ContentAcquisitionViewModel : ObservableObject
{
    private readonly ContentAcquisitionService _service;
    private readonly Func<Task> _refreshContent;
    private readonly Action<string> _reportStatus;
    private IDesktopInteractionService? _desktopInteractions;
    private IReadOnlyList<ContentBundleDiagnostic> _previewBundleDiagnostics = [];
    private IReadOnlyList<ContentBundleDiagnostic> _repairDiagnostics = [];

    public ContentAcquisitionViewModel(
        ContentAcquisitionService service,
        Func<Task> refreshContent,
        Action<string> reportStatus)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _refreshContent = refreshContent ?? throw new ArgumentNullException(nameof(refreshContent));
        _reportStatus = reportStatus ?? throw new ArgumentNullException(nameof(reportStatus));
    }

    public ObservableCollection<DeckView> ExistingDecks { get; } = [];
    public ObservableCollection<ContentOperationRowViewModel> Operations { get; } = [];
    public ObservableCollection<ContentOperationRowViewModel> PrimaryOperations { get; } = [];
    public ObservableCollection<ContentOperationRowViewModel> VisibleTechnicalOperations { get; } = [];
    public ObservableCollection<QualityReviewResultRowViewModel> QualityReviewResults { get; } = [];
    public ObservableCollection<QualityReviewDiagnosticDisplay> QualityReviewDiagnostics { get; } = [];
    public IReadOnlyList<QualityReviewPromptMode> QualityReviewModes { get; } = Enum.GetValues<QualityReviewPromptMode>();
    public IReadOnlyList<PreImportQualityReviewPromptItem> QualityReviewPromptItems { get; private set; } = [];
    public ObservableCollection<AcquisitionDiagnosticDisplay> BundleDiagnostics { get; } = [];

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GeneratePromptCommand))]
    private string _subject = string.Empty;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GeneratePromptCommand))]
    private int _requestedItemCount = 50;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GeneratePromptCommand))]
    private bool _useNewDeck = true;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GeneratePromptCommand))]
    private bool _useExistingDeck;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GeneratePromptCommand))]
    private string _newDeckName = string.Empty;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GeneratePromptCommand))]
    private DeckView? _selectedExistingDeck;

    [ObservableProperty]
    private string _guidance = string.Empty;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasGeneratedPrompt)), NotifyCanExecuteChangedFor(nameof(CopyPromptCommand))]
    private string _generatedPrompt = string.Empty;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ValidateCommand))]
    private string _rawJson = string.Empty;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasRepairPrompt)), NotifyCanExecuteChangedFor(nameof(CopyRepairPromptCommand))]
    private string _repairPrompt = string.Empty;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsInteractionEnabled)), NotifyCanExecuteChangedFor(nameof(GeneratePromptCommand)), NotifyCanExecuteChangedFor(nameof(CopyPromptCommand)), NotifyCanExecuteChangedFor(nameof(LoadJsonFileCommand)), NotifyCanExecuteChangedFor(nameof(ValidateCommand)), NotifyCanExecuteChangedFor(nameof(GenerateRepairPromptCommand)), NotifyCanExecuteChangedFor(nameof(CopyRepairPromptCommand)), NotifyCanExecuteChangedFor(nameof(ImportSelectedCommand))]
    private bool _isBusy;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ImportSelectedCommand)), NotifyCanExecuteChangedFor(nameof(GenerateQualityReviewPromptCommand)), NotifyCanExecuteChangedFor(nameof(PreviewQualityReviewCommand)), NotifyCanExecuteChangedFor(nameof(LoadQualityReviewFileCommand))]
    private bool _hasCurrentPreview;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GenerateRepairPromptCommand))]
    private bool _canGenerateRepairPrompt;

    [ObservableProperty]
    private string _previewSummary = string.Empty;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasImportResult))]
    private string _importResult = string.Empty;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasQualityReviewPrompt)), NotifyCanExecuteChangedFor(nameof(CopyQualityReviewPromptCommand))]
    private string _qualityReviewPrompt = string.Empty;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(PreviewQualityReviewCommand)), NotifyCanExecuteChangedFor(nameof(GenerateQualityReviewPromptCommand))]
    private string _rawQualityReviewJson = string.Empty;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ImportSelectedCommand))]
    private bool _hasQualityReviewPreview;

    [ObservableProperty]
    private string _qualityReviewSummary = string.Empty;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GenerateQualityReviewPromptCommand)), NotifyCanExecuteChangedFor(nameof(PreviewQualityReviewCommand))]
    private bool _currentBundleIsValid;

    [ObservableProperty]
    private QualityReviewPromptMode _qualityReviewMode = QualityReviewPromptMode.Standard;

    [ObservableProperty]
    private bool _showAllTechnicalOperations;

    [ObservableProperty]
    private bool _showImportedDetails;

    public bool HasExistingDecks => ExistingDecks.Count > 0;
    public bool HasGeneratedPrompt => !string.IsNullOrWhiteSpace(GeneratedPrompt);
    public bool HasRepairPrompt => !string.IsNullOrWhiteSpace(RepairPrompt);
    public bool HasImportResult => !string.IsNullOrWhiteSpace(ImportResult);
    public bool HasQualityReviewPrompt => !string.IsNullOrWhiteSpace(QualityReviewPrompt);
    public bool IsInteractionEnabled => !IsBusy;
    public bool HasTechnicalOperations => TechnicalOperationCount > 0;
    public bool HasVisibleTechnicalOperations => VisibleTechnicalOperations.Count > 0;
    public string TechnicalOperationsToggleText => ShowAllTechnicalOperations
        ? "Hide technical operations"
        : $"Show all technical operations ({TechnicalOperationCount})";
    public string ImportedDetailsToggleText => ShowImportedDetails ? "Hide imported-operation details" : "Show imported-operation details";
    public int LearningItemOperationCount { get; private set; }
    public int DeckOperationCount { get; private set; }
    public int AssignmentOperationCount { get; private set; }
    public int TechnicalOperationCount { get; private set; }

    public void AttachDesktopInteractions(IDesktopInteractionService desktopInteractions)
    {
        _desktopInteractions = desktopInteractions ?? throw new ArgumentNullException(nameof(desktopInteractions));
        LoadJsonFileCommand.NotifyCanExecuteChanged();
        LoadQualityReviewFileCommand.NotifyCanExecuteChanged();
        CopyPromptCommand.NotifyCanExecuteChanged();
        CopyQualityReviewPromptCommand.NotifyCanExecuteChanged();
        CopyRepairPromptCommand.NotifyCanExecuteChanged();
    }

    public void UpdateDecks(IEnumerable<DeckView> decks)
    {
        var selectedId = SelectedExistingDeck?.Id;
        ExistingDecks.Clear();
        foreach (var deck in decks) ExistingDecks.Add(deck);
        SelectedExistingDeck = ExistingDecks.FirstOrDefault(x => x.Id == selectedId) ?? ExistingDecks.FirstOrDefault();
        OnPropertyChanged(nameof(HasExistingDecks));
        if (!HasExistingDecks && UseExistingDeck) UseNewDeck = true;
        GeneratePromptCommand.NotifyCanExecuteChanged();
    }

    partial void OnUseNewDeckChanged(bool value)
    {
        if (value) UseExistingDeck = false;
    }

    partial void OnUseExistingDeckChanged(bool value)
    {
        if (value) UseNewDeck = false;
    }

    partial void OnRawJsonChanged(string value) => InvalidatePreview();

    partial void OnRawQualityReviewJsonChanged(string value) => InvalidateQualityReviewPreview();

    [RelayCommand(CanExecute = nameof(CanGeneratePrompt))]
    private async Task GeneratePromptAsync() => await RunBusyAsync(() =>
    {
        var command = new GenerateContentPromptCommand(
            Subject,
            RequestedItemCount,
            NewDeckName: UseNewDeck ? NewDeckName : null,
            ExistingDeckId: UseExistingDeck ? SelectedExistingDeck?.Id : null,
            Guidance: Guidance);
        GeneratedPrompt = _service.GenerateContentPrompt(command).Prompt;
        _reportStatus("Prompt generated.");
        return Task.CompletedTask;
    });

    private bool CanGeneratePrompt() => !IsBusy
        && !string.IsNullOrWhiteSpace(Subject)
        && RequestedItemCount > 0
        && ((UseNewDeck && !string.IsNullOrWhiteSpace(NewDeckName))
            || (UseExistingDeck && SelectedExistingDeck is not null));

    [RelayCommand(CanExecute = nameof(CanCopyPrompt))]
    private async Task CopyPromptAsync() => await CopyAsync(GeneratedPrompt, "Prompt copied.");

    private bool CanCopyPrompt() => !IsBusy && _desktopInteractions is not null && HasGeneratedPrompt;

    [RelayCommand(CanExecute = nameof(CanLoadJsonFile))]
    private async Task LoadJsonFileAsync() => await RunBusyAsync(async () =>
    {
        var loaded = await _desktopInteractions!.LoadJsonFileAsync();
        if (loaded is null) return;
        RawJson = loaded;
        _reportStatus("JSON file loaded. Validate when ready.");
    });

    private bool CanLoadJsonFile() => !IsBusy && _desktopInteractions is not null;

    [RelayCommand(CanExecute = nameof(CanValidate))]
    private async Task ValidateAsync() => await RunBusyAsync(async () =>
    {
        InvalidatePreview();
        var rawJson = RawJson;
        var preview = await _service.PreviewContentBundleAsync(rawJson);
        if (!string.Equals(RawJson, rawJson, StringComparison.Ordinal)) return;
        _previewBundleDiagnostics = preview.Diagnostics;
        _repairDiagnostics = preview.Diagnostics
            .Concat(preview.Operations.SelectMany(operation => operation.Diagnostics))
            .ToArray();
        RestorePreviewDiagnostics();
        foreach (var operation in preview.Operations)
        {
            var row = new ContentOperationRowViewModel(operation, SelectionChanged);
            Operations.Add(row);
            if (row.IsPrimaryContentOperation) PrimaryOperations.Add(row);
        }
        UpdateOperationPresentation();

        HasCurrentPreview = true;
        CurrentBundleIsValid = preview.IsValid;
        CanGenerateRepairPrompt = preview.CanGenerateRepairPrompt && !preview.IsValid;
        var selectable = preview.Operations.Count(x => x.IsSelectable);
        var invalid = preview.Operations.Count - selectable;
        PreviewSummary = $"{LearningItemOperationCount} Learning Items · {DeckOperationCount} Decks · {AssignmentOperationCount} assignments · {preview.Operations.Count} total operations · {selectable} valid · {invalid} invalid";
        _reportStatus($"Bundle validated: {selectable} valid, {invalid} invalid.");
        ImportSelectedCommand.NotifyCanExecuteChanged();
    });

    private bool CanValidate() => !IsBusy && !string.IsNullOrWhiteSpace(RawJson);

    [RelayCommand(CanExecute = nameof(CanGenerateQualityReviewPrompt))]
    private async Task GenerateQualityReviewPromptAsync() => await RunBusyAsync(async () =>
    {
        var generated = await _service.GeneratePreImportQualityReviewPromptAsync(
            new GeneratePreImportQualityReviewPromptCommand(RawJson, QualityReviewMode));
        QualityReviewPrompt = generated.Prompt;
        QualityReviewPromptItems = generated.Items;
        OnPropertyChanged(nameof(QualityReviewPromptItems));
        _reportStatus($"Quality Review prompt generated for {generated.Items.Count} Learning Items.");
    });

    private bool CanGenerateQualityReviewPrompt() => !IsBusy && HasCurrentPreview && CurrentBundleIsValid;

    [RelayCommand(CanExecute = nameof(CanCopyQualityReviewPrompt))]
    private async Task CopyQualityReviewPromptAsync() => await CopyAsync(QualityReviewPrompt, "Quality Review prompt copied.");

    private bool CanCopyQualityReviewPrompt() => !IsBusy && _desktopInteractions is not null && HasQualityReviewPrompt;

    [RelayCommand(CanExecute = nameof(CanPreviewQualityReview))]
    private async Task PreviewQualityReviewAsync() => await RunBusyAsync(async () =>
    {
        InvalidateQualityReviewPreview();
        var preview = await _service.PreviewPreImportQualityReviewAsync(
            new PreviewPreImportQualityReviewCommand(RawJson, RawQualityReviewJson, QualityReviewMode));
        foreach (var diagnostic in preview.Diagnostics) QualityReviewDiagnostics.Add(ToDisplay(diagnostic));
        foreach (var result in preview.Results)
        {
            var operation = result.OperationIndex is { } operationIndex
                ? Operations.FirstOrDefault(candidate => candidate.OperationIndex == operationIndex)
                : null;
            QualityReviewResults.Add(new QualityReviewResultRowViewModel(result, operation, QualityReviewSelectionChanged));
        }
        HasQualityReviewPreview = true;
        var valid = preview.Results.Count(x => x.IsValid);
        var invalid = preview.Results.Count - valid;
        QualityReviewSummary = $"{valid} valid review results · {invalid} invalid";
        _reportStatus($"Quality Review results previewed: {valid} valid, {invalid} invalid.");
    });

    private bool CanPreviewQualityReview() => !IsBusy && HasCurrentPreview && CurrentBundleIsValid && !string.IsNullOrWhiteSpace(RawQualityReviewJson);

    [RelayCommand(CanExecute = nameof(CanLoadQualityReviewFile))]
    private async Task LoadQualityReviewFileAsync() => await RunBusyAsync(async () =>
    {
        var loaded = await _desktopInteractions!.LoadJsonFileAsync();
        if (loaded is null) return;
        RawQualityReviewJson = loaded;
        _reportStatus("Quality Review result loaded. Preview when ready.");
    });

    private bool CanLoadQualityReviewFile() => !IsBusy && _desktopInteractions is not null && HasCurrentPreview;

    [RelayCommand]
    private void SelectAllValidQualityReviews()
    {
        foreach (var result in QualityReviewResults.Where(x => x.IsSelectable)) result.IsSelected = true;
    }

    [RelayCommand]
    private void ClearQualityReviewSelection()
    {
        foreach (var result in QualityReviewResults) result.IsSelected = false;
    }

    [RelayCommand]
    private void SelectAllValid()
    {
        foreach (var operation in Operations.Where(x => x.IsSelectable)) operation.IsSelected = true;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var operation in Operations) operation.IsSelected = false;
    }

    [RelayCommand]
    private void ToggleTechnicalOperations()
    {
        ShowAllTechnicalOperations = !ShowAllTechnicalOperations;
        RebuildVisibleTechnicalOperations();
        OnPropertyChanged(nameof(TechnicalOperationsToggleText));
    }

    [RelayCommand]
    private void ToggleImportedDetails()
    {
        ShowImportedDetails = !ShowImportedDetails;
        OnPropertyChanged(nameof(ImportedDetailsToggleText));
    }

    [RelayCommand(CanExecute = nameof(CanGenerateRepairPromptNow))]
    private async Task GenerateRepairPromptAsync() => await RunBusyAsync(() =>
    {
        RepairPrompt = _service.GenerateRepairPrompt(new GenerateRepairPromptCommand(RawJson, _repairDiagnostics)).Prompt;
        _reportStatus("Repair prompt generated.");
        return Task.CompletedTask;
    });

    private bool CanGenerateRepairPromptNow() => !IsBusy && HasCurrentPreview && CanGenerateRepairPrompt;

    [RelayCommand(CanExecute = nameof(CanCopyRepairPrompt))]
    private async Task CopyRepairPromptAsync() => await CopyAsync(RepairPrompt, "Repair prompt copied.");

    private bool CanCopyRepairPrompt() => !IsBusy && _desktopInteractions is not null && HasRepairPrompt;

    [RelayCommand(CanExecute = nameof(CanImportSelected))]
    private async Task ImportSelectedAsync()
    {
        if (!CanImportSelected()) return;
        IsBusy = true;
        try
        {
            RestorePreviewDiagnostics();
            var selected = Operations.Where(x => x.IsSelected && x.IsSelectable).Select(x => x.OperationIndex).ToArray();
            var selectedReviews = QualityReviewResults.Where(x => x.IsSelected && x.IsSelectable).Select(x => x.ResultIndex).ToArray();
            var acceptedReviews = selectedReviews.Length == 0
                ? null
                : new PreImportQualityReviewSelection(RawQualityReviewJson, QualityReviewMode, selectedReviews);
            var result = await _service.CommitContentBundleAsync(new CommitContentBundleCommand(RawJson, selected, acceptedReviews));
            ImportResult = $"Imported successfully\n{result.CreatedLearningItemIds.Count} Learning Items created · {result.UpdatedLearningItemIds.Count} updated\n{result.CreatedDeckIds.Count} Decks created · {result.UpdatedDeckIds.Count} updated · {result.AppliedMembershipCount} memberships applied";
            HasCurrentPreview = false;
            CurrentBundleIsValid = false;
            HasQualityReviewPreview = false;
            CanGenerateRepairPrompt = false;
            try
            {
                await _refreshContent();
                _reportStatus($"Imported {result.CommittedOperationIndices.Count} operations.");
            }
            catch (Exception exception)
            {
                _reportStatus($"Import succeeded, but content refresh failed: {exception.Message}");
            }
        }
        catch (ContentAcquisitionValidationException exception)
        {
            ImportResult = string.Empty;
            foreach (var diagnostic in exception.Diagnostics) BundleDiagnostics.Add(ToDisplay(diagnostic));
            _reportStatus("Import failed validation. Review the diagnostics.");
        }
        catch (Exception exception)
        {
            ImportResult = string.Empty;
            _reportStatus(exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanImportSelected() => !IsBusy && HasCurrentPreview && Operations.Any(x => x.IsSelectable && x.IsSelected);

    private async Task CopyAsync(string text, string successMessage)
    {
        if (_desktopInteractions is null) return;
        try
        {
            await _desktopInteractions.CopyTextAsync(text);
            _reportStatus(successMessage);
        }
        catch (Exception exception)
        {
            _reportStatus(exception.Message);
        }
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            _reportStatus(exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SelectionChanged() => ImportSelectedCommand.NotifyCanExecuteChanged();

    private void QualityReviewSelectionChanged() => ImportSelectedCommand.NotifyCanExecuteChanged();

    private void RestorePreviewDiagnostics()
    {
        BundleDiagnostics.Clear();
        foreach (var diagnostic in _previewBundleDiagnostics) BundleDiagnostics.Add(ToDisplay(diagnostic));
    }

    private void UpdateOperationPresentation()
    {
        LearningItemOperationCount = Operations.Count(operation => operation.OperationKind is "create_learning_item" or "update_learning_item");
        DeckOperationCount = Operations.Count(operation => operation.OperationKind is "create_deck" or "update_deck");
        AssignmentOperationCount = Operations.Count(operation => operation.OperationKind == "assign_item_to_decks");
        TechnicalOperationCount = Operations.Count(operation => !operation.IsPrimaryContentOperation);
        OnPropertyChanged(nameof(LearningItemOperationCount));
        OnPropertyChanged(nameof(DeckOperationCount));
        OnPropertyChanged(nameof(AssignmentOperationCount));
        OnPropertyChanged(nameof(TechnicalOperationCount));
        OnPropertyChanged(nameof(HasTechnicalOperations));
        OnPropertyChanged(nameof(TechnicalOperationsToggleText));
        RebuildVisibleTechnicalOperations();
    }

    private void RebuildVisibleTechnicalOperations()
    {
        VisibleTechnicalOperations.Clear();
        foreach (var operation in Operations.Where(operation =>
                     !operation.IsPrimaryContentOperation
                     && (ShowAllTechnicalOperations || operation.RequiresAttention)))
            VisibleTechnicalOperations.Add(operation);
        OnPropertyChanged(nameof(HasVisibleTechnicalOperations));
    }

    private void InvalidatePreview()
    {
        HasCurrentPreview = false;
        CanGenerateRepairPrompt = false;
        PreviewSummary = string.Empty;
        RepairPrompt = string.Empty;
        ImportResult = string.Empty;
        CurrentBundleIsValid = false;
        ShowAllTechnicalOperations = false;
        ShowImportedDetails = false;
        _previewBundleDiagnostics = [];
        _repairDiagnostics = [];
        BundleDiagnostics.Clear();
        Operations.Clear();
        PrimaryOperations.Clear();
        VisibleTechnicalOperations.Clear();
        InvalidateQualityReviewPreview();
        LearningItemOperationCount = 0;
        DeckOperationCount = 0;
        AssignmentOperationCount = 0;
        TechnicalOperationCount = 0;
        OnPropertyChanged(nameof(HasTechnicalOperations));
        OnPropertyChanged(nameof(HasVisibleTechnicalOperations));
        OnPropertyChanged(nameof(TechnicalOperationsToggleText));
        OnPropertyChanged(nameof(ImportedDetailsToggleText));
        ImportSelectedCommand.NotifyCanExecuteChanged();
    }

    private void InvalidateQualityReviewPreview()
    {
        HasQualityReviewPreview = false;
        QualityReviewSummary = string.Empty;
        QualityReviewDiagnostics.Clear();
        QualityReviewResults.Clear();
        ImportSelectedCommand.NotifyCanExecuteChanged();
    }

    private static AcquisitionDiagnosticDisplay ToDisplay(ContentBundleDiagnostic diagnostic) =>
        new(diagnostic.Code, diagnostic.Message, diagnostic.OperationIndex is { } index ? $"Operation {index + 1}" : "Bundle");

    private static QualityReviewDiagnosticDisplay ToDisplay(PreImportQualityReviewResultDiagnostic diagnostic) =>
        new(diagnostic.Code, diagnostic.Message, diagnostic.ResultIndex is { } index ? $"Review result {index + 1}" : "Review bundle");
}

public sealed partial class ContentOperationRowViewModel : ObservableObject
{
    private readonly Action _selectionChanged;

    public ContentOperationRowViewModel(ContentBundleOperationPreview preview, Action selectionChanged)
    {
        _selectionChanged = selectionChanged;
        OperationIndex = preview.OperationIndex;
        OperationNumber = preview.OperationIndex + 1;
        OperationKind = preview.OperationType;
        OperationType = FormatOperationType(preview.OperationType);
        Summary = preview.Summary;
        IsSelectable = preview.IsSelectable;
        IsValid = preview.IsValid;
        Diagnostics = string.Join(" · ", preview.Diagnostics.Select(x => x.Message));
        Warnings = string.Join(" · ", preview.Warnings);
        Dependencies = preview.Dependencies.Count == 0 ? string.Empty : "Depends on: " + string.Join(", ", preview.Dependencies);
        _isSelected = preview.IsSelectable;
    }

    public int OperationIndex { get; }
    public int OperationNumber { get; }
    public string? OperationKind { get; }
    public string OperationType { get; }
    public string Summary { get; }
    public bool IsSelectable { get; }
    public bool IsValid { get; }
    public string Diagnostics { get; }
    public string Warnings { get; }
    public string Dependencies { get; }
    public bool HasDiagnostics => !string.IsNullOrWhiteSpace(Diagnostics);
    public bool HasWarnings => !string.IsNullOrWhiteSpace(Warnings);
    public bool HasDependencies => !string.IsNullOrWhiteSpace(Dependencies);
    public bool IsPrimaryContentOperation => OperationKind is "create_learning_item" or "update_learning_item" or "create_deck" or "update_deck";
    public bool RequiresAttention => HasDiagnostics || HasWarnings;

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (!IsSelectable && value)
        {
            IsSelected = false;
            return;
        }
        _selectionChanged();
    }

    private static string FormatOperationType(string? operationType) => operationType switch
    {
        "create_deck" => "Create Deck",
        "update_deck" => "Update Deck",
        "create_learning_item" => "Create Learning Item",
        "update_learning_item" => "Update Learning Item",
        "assign_item_to_decks" => "Assign membership",
        _ => "Invalid operation",
    };
}

public sealed record AcquisitionDiagnosticDisplay(string Code, string Message, string Scope);

public sealed partial class QualityReviewResultRowViewModel : ObservableObject
{
    private readonly Action _selectionChanged;

    public QualityReviewResultRowViewModel(
        PreImportQualityReviewResultPreview result,
        ContentOperationRowViewModel? operation,
        Action selectionChanged)
    {
        _selectionChanged = selectionChanged;
        ResultIndex = result.ResultIndex;
        OperationIndex = result.OperationIndex;
        LocalRef = result.LocalRef ?? "(missing localRef)";
        Prompt = operation?.Summary ?? LocalRef;
        Outcome = result.Outcome switch
        {
            CurationQualityReviewOutcome.Pass => "Pass",
            CurationQualityReviewOutcome.Warning => "Warning",
            CurationQualityReviewOutcome.NeedsReview => "Needs Review",
            _ => "Invalid"
        };
        Findings = result.Findings ?? string.Empty;
        SuggestedCorrection = result.SuggestedCorrection ?? string.Empty;
        Fingerprint = result.ContentFingerprint ?? string.Empty;
        Diagnostics = string.Join(" · ", result.Diagnostics.Select(x => x.Message));
        IsValid = result.IsValid;
        IsSelectable = result.IsValid && result.Outcome is not null;
        _isSelected = IsSelectable;
    }

    public int ResultIndex { get; }
    public int? OperationIndex { get; }
    public string LocalRef { get; }
    public string Prompt { get; }
    public string Outcome { get; }
    public string Findings { get; }
    public string SuggestedCorrection { get; }
    public string Fingerprint { get; }
    public string Diagnostics { get; }
    public bool IsValid { get; }
    public bool IsSelectable { get; }
    public bool HasSuggestedCorrection => !string.IsNullOrWhiteSpace(SuggestedCorrection);
    public bool HasDiagnostics => !string.IsNullOrWhiteSpace(Diagnostics);
    public bool IsNeedsReview => Outcome == "Needs Review";
    public bool IsWarning => Outcome == "Warning";

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (!IsSelectable && value)
        {
            IsSelected = false;
            return;
        }
        _selectionChanged();
    }
}

public sealed record QualityReviewDiagnosticDisplay(string Code, string Message, string Scope);
