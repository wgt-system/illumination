using Avalonia.Controls;

namespace Illumination.Desktop;

public sealed partial class IlluminationProductSurface : UserControl
{
    public IlluminationProductSurface() => InitializeComponent();

    internal void AttachDesktopInteractions(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        var interactions = new AvaloniaDesktopInteractionService(() => TopLevel.GetTopLevel(this) as Window);
        viewModel.ContentAcquisition.AttachDesktopInteractions(interactions);
        viewModel.ContentCuration.AttachDesktopInteractions(interactions);
        viewModel.LocalData?.AttachDesktopInteractions(interactions);
        viewModel.AttachDeckExportInteractions(interactions);
    }
}
