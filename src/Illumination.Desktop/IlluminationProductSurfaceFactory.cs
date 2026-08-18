using Avalonia.Controls;

namespace Illumination.Desktop;

public static class IlluminationProductSurfaceFactory
{
    public static async Task<Control> CreateAsync()
    {
        var viewModel = await DesktopComposition.CreateAsync();
        var surface = new IlluminationProductSurface
        {
            DataContext = viewModel,
        };
        surface.AttachDesktopInteractions(viewModel);
        return surface;
    }
}
