using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class DesktopApiHardeningTests
{
    [Fact]
    public void Drop_handlers_use_current_Avalonia_data_transfer_boundary()
    {
        var root = FindRepositoryRoot();
        var importCodeBehind = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "ImportView.axaml.cs"));
        var mainWindowCodeBehind = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "MainWindow.axaml.cs"));
        var helper = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "DroppedFileDataTransfer.cs"));

        Assert.DoesNotContain("e.Data.GetFileNames", importCodeBehind);
        Assert.DoesNotContain("e.Data.GetFileNames", mainWindowCodeBehind);
        Assert.Contains("DroppedFileDataTransfer.GetLocalPaths", importCodeBehind);
        Assert.Contains("DroppedFileDataTransfer.GetLocalPaths", mainWindowCodeBehind);
        Assert.Contains("e.DataTransfer.TryGetFiles()", helper);
        Assert.Contains("TryGetLocalPath()", helper);
        Assert.Contains("item.Dispose()", helper);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Illumination.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the Illumination repository root.");
    }
}
