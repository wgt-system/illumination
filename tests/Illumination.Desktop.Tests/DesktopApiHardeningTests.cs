using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class DesktopApiHardeningTests
{
    [Fact]
    public void Import_drop_handler_uses_current_Avalonia_data_transfer_boundary()
    {
        var root = FindRepositoryRoot();
        var importCodeBehind = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "ImportView.axaml.cs"));
        var mainWindowCodeBehind = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "MainWindow.axaml.cs"));
        var helper = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "DroppedFileDataTransfer.cs"));

        Assert.DoesNotContain("e.Data.GetFileNames", importCodeBehind);
        Assert.Contains("DroppedFileDataTransfer.GetLocalPaths", importCodeBehind);
        Assert.Contains("e.DataTransfer.TryGetFiles()", helper);
        Assert.Contains("TryGetLocalPath()", helper);
        Assert.Contains("item.Dispose()", helper);

        Assert.DoesNotContain("DragEventArgs", mainWindowCodeBehind);
        Assert.DoesNotContain("DroppedFileDataTransfer", mainWindowCodeBehind);
    }

    [Fact]
    public void Product_surface_resolves_interactions_from_the_actual_containing_top_level()
    {
        var root = FindRepositoryRoot();
        var surfaceCodeBehind = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "IlluminationProductSurface.axaml.cs"));
        var appCodeBehind = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "App.axaml.cs"));

        Assert.Contains("TopLevel.GetTopLevel(this) as Window", surfaceCodeBehind);
        Assert.Contains("AttachDesktopInteractions", surfaceCodeBehind);
        Assert.DoesNotContain("new AvaloniaDesktopInteractionService", appCodeBehind);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Illumination.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the Illumination repository root.");
    }
}
