using System.ComponentModel;
using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed partial class MainWindowViewModel
{
    private Guid? _armedLearningItemDeleteId;

    partial void OnSelectedDeckChanged(DeckView? oldValue, DeckView? newValue)
    {
        if (oldValue?.Id != newValue?.Id) DeleteDeckArmed = false;
    }

    partial void OnDeleteItemArmedChanged(bool value)
    {
        if (!value)
        {
            _armedLearningItemDeleteId = null;
            ContentCuration.PropertyChanged -= OnContentCurationPropertyChanged;
            return;
        }

        if (SessionIsActive)
        {
            DeleteItemArmed = false;
            return;
        }

        _armedLearningItemDeleteId = ContentCuration.SelectedItem?.Id;
        ContentCuration.PropertyChanged -= OnContentCurationPropertyChanged;
        ContentCuration.PropertyChanged += OnContentCurationPropertyChanged;
    }

    partial void OnSessionIsActiveChanged(bool value)
    {
        if (value) DeleteItemArmed = false;
    }

    private void OnContentCurationPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ContentCurationViewModel.SelectedItem)
            && _armedLearningItemDeleteId != ContentCuration.SelectedItem?.Id)
        {
            DeleteItemArmed = false;
        }
    }
}
