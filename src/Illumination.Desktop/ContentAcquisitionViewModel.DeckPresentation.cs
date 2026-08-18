using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed partial class ContentAcquisitionViewModel
{
    private bool _deckOptionsSubscribed;
    private readonly ObservableCollection<DeckPresentationItem> _existingDeckOptions = [];

    public ObservableCollection<DeckPresentationItem> ExistingDeckOptions
    {
        get
        {
            EnsureDeckOptionSubscription();
            return _existingDeckOptions;
        }
    }

    [ObservableProperty]
    private DeckPresentationItem? _selectedExistingDeckPresentation;

    partial void OnSelectedExistingDeckPresentationChanged(DeckPresentationItem? value)
    {
        if (value is not null && SelectedExistingDeck?.Id != value.Id) SelectedExistingDeck = value.Deck;
    }

    partial void OnSelectedExistingDeckChanged(DeckView? value)
    {
        EnsureDeckOptionSubscription();
        var presentation = _existingDeckOptions.FirstOrDefault(x => x.Id == value?.Id);
        if (SelectedExistingDeckPresentation?.Id != presentation?.Id) SelectedExistingDeckPresentation = presentation;
    }

    private void EnsureDeckOptionSubscription()
    {
        if (_deckOptionsSubscribed) return;
        ExistingDecks.CollectionChanged += ExistingDecksChanged;
        _deckOptionsSubscribed = true;
        RebuildExistingDeckOptions();
    }

    private void ExistingDecksChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildExistingDeckOptions();

    private void RebuildExistingDeckOptions()
    {
        var selectedId = SelectedExistingDeck?.Id;
        _existingDeckOptions.Clear();
        foreach (var option in DeckPresentationLabeler.Label(ExistingDecks)) _existingDeckOptions.Add(option);
        SelectedExistingDeckPresentation = _existingDeckOptions.FirstOrDefault(x => x.Id == selectedId)
            ?? _existingDeckOptions.FirstOrDefault();
    }
}
