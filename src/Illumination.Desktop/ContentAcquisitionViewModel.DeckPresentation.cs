using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed partial class ContentAcquisitionViewModel
{
    private bool _deckOptionsSubscribed;

    public IReadOnlyList<DeckPresentationItem> ExistingDeckOptions
    {
        get
        {
            EnsureDeckOptionSubscription();
            return DeckPresentationLabeler.Label(ExistingDecks);
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
        OnPropertyChanged(nameof(ExistingDeckOptions));
        var presentation = ExistingDeckOptions.FirstOrDefault(x => x.Id == value?.Id);
        if (SelectedExistingDeckPresentation?.Id != presentation?.Id) SelectedExistingDeckPresentation = presentation;
    }

    private void EnsureDeckOptionSubscription()
    {
        if (_deckOptionsSubscribed) return;
        ExistingDecks.CollectionChanged += ExistingDecksChanged;
        _deckOptionsSubscribed = true;
    }

    private void ExistingDecksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ExistingDeckOptions));
    }
}
