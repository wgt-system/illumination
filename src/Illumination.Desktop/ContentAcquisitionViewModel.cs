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

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ImportSelectedCommand))]
    private bool _hasCurrentPreview;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GenerateRepairPromptCommand))]
    private bool _canGenerateRepairPrompt;

    [ObservableProperty]
    private string _previewSummary = string.Empty;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasImportResult))]
    private string _importResult = string.Empty;

    public bool HasExistingDecks => ExistingDecks.Count > 0;
    public bool HasGeneratedPrompt => !string.IsNullOrWhiteSpace(GeneratedPrompt);
    public bool HasRepairPrompt => !string.IsNullOrWhiteSpace(RepairPrompt);
    public bool HasImportResult => !string.IsNullOrWhiteSpace(ImportResult);
    public bool IsInteractionEnabled => !IsBusy;

    public void AttachDesktopInteractions(IDesktopInteractionService desktopInteractions)
    {
        _desktopInteractions = desktopInteractions ?? throw new ArgumentNullException(nameof(desktopInteractions));
        LoadJsonFileCommand.NotifyCanExecuteChanged();
        CopyPromptCommand.NotifyCanExecuteChanged();
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
            Operations.Add(new ContentOperationRowViewModel(operation, SelectionChanged));
        }

        HasCurrentPreview = true;
        CanGenerateRepairPrompt = preview.CanGenerateRepairPrompt && !preview.IsValid;
        var selectable = preview.Operations.Count(x => x.IsSelectable);
        var invalid = preview.Operations.Count - selectable;
        PreviewSummary = $"{selectable} valid · {invalid} invalid";
        _reportStatus($"Bundle validated: {selectable} valid, {invalid} invalid.");
        ImportSelectedCommand.NotifyCanExecuteChanged();
    });

    private bool CanValidate() => !IsBusy && !string.IsNullOrWhiteSpace(RawJson);

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
            var result = await _service.CommitContentBundleAsync(new CommitContentBundleCommand(RawJson, selected));
            ImportResult = $"Imported successfully\n{result.CreatedLearningItemIds.Count} Learning Items created · {result.UpdatedLearningItemIds.Count} updated\n{result.CreatedDeckIds.Count} Decks created · {result.UpdatedDeckIds.Count} updated · {result.AppliedMembershipCount} memberships applied";
            HasCurrentPreview = false;
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

    private void RestorePreviewDiagnostics()
    {
        BundleDiagnostics.Clear();
        foreach (var diagnostic in _previewBundleDiagnostics) BundleDiagnostics.Add(ToDisplay(diagnostic));
    }

    private void InvalidatePreview()
    {
        HasCurrentPreview = false;
        CanGenerateRepairPrompt = false;
        PreviewSummary = string.Empty;
        RepairPrompt = string.Empty;
        ImportResult = string.Empty;
        _previewBundleDiagnostics = [];
        _repairDiagnostics = [];
        BundleDiagnostics.Clear();
        Operations.Clear();
        ImportSelectedCommand.NotifyCanExecuteChanged();
    }

    private static AcquisitionDiagnosticDisplay ToDisplay(ContentBundleDiagnostic diagnostic) =>
        new(diagnostic.Code, diagnostic.Message, diagnostic.OperationIndex is { } index ? $"Operation {index + 1}" : "Bundle");
}

public sealed partial class ContentOperationRowViewModel : ObservableObject
{
    private readonly Action _selectionChanged;

    public ContentOperationRowViewModel(ContentBundleOperationPreview preview, Action selectionChanged)
    {
        _selectionChanged = selectionChanged;
        OperationIndex = preview.OperationIndex;
        OperationNumber = preview.OperationIndex + 1;
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
