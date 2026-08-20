using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class ShellStructureTests
{
    [Fact]
    public void Product_surface_is_the_shell_with_explicit_page_views_and_study_default()
    {
        var root = FindRepositoryRoot();
        var surface = ReadProductSurface(root);
        var window = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "MainWindow.axaml"));

        Assert.DoesNotContain("<TabControl", surface);
        Assert.Contains("IsVisible=\"{Binding IsStudyPage}\"", surface);
        Assert.Contains("<desktop:StudyView", surface);
        Assert.Contains("IsVisible=\"{Binding IsInsightsPage}\"", surface);
        Assert.Contains("<desktop:InsightsView", surface);
        Assert.Contains("<desktop:DecksView", surface);
        Assert.Contains("<desktop:LibraryView", surface);
        Assert.Contains("<desktop:ImportView", surface);
        Assert.True(surface.IndexOf("Study", StringComparison.Ordinal) < surface.IndexOf("Decks", StringComparison.Ordinal));

        Assert.Contains("<desktop:IlluminationProductSurface", window);
        Assert.DoesNotContain("<desktop:StudyView", window);
        Assert.DoesNotContain("LocalData.DatabaseLocation", window);

        var insights = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "InsightsView.axaml"));
        Assert.Contains("SuspendCommand", insights);
        Assert.Contains("ReactivateCommand", insights);
        Assert.Contains("MarkMasteredCommand", insights);
        Assert.Contains("UnmarkMasteredCommand", insights);
    }

    [Fact]
    public void Insights_navigation_refreshes_the_derived_read_model_when_opened()
    {
        var root = FindRepositoryRoot();
        var surface = ReadProductSurface(root);
        var coherence = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "MainWindowViewModel.RuntimeCoherence.cs"));

        Assert.Contains("Command=\"{Binding OpenInsightsCommand}\"", surface);
        Assert.Contains("SelectedPage = DesktopPage.Insights", coherence);
        Assert.Contains("await Insights.RefreshAsync()", coherence);
    }

    [Fact]
    public void Study_page_does_not_expose_unaccepted_session_resume_controls()
    {
        var root = FindRepositoryRoot();
        var study = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "StudyView.axaml"));

        Assert.DoesNotContain("UnfinishedStudySessions", study);
        Assert.DoesNotContain("ResumeStudySessionCommand", study);
        Assert.DoesNotContain("FinishStoredStudySessionCommand", study);
    }

    [Fact]
    public void Local_data_controls_expose_the_accepted_v08_backup_policy_without_restore_semantics()
    {
        var root = FindRepositoryRoot();
        var surface = ReadProductSurface(root);
        var composition = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "DesktopComposition.cs"));

        Assert.Contains("LocalData.DatabaseLocation", surface);
        Assert.Contains("LocalData.BackupDirectoryInput", surface);
        Assert.Contains("LocalData.ApplyBackupDirectoryCommand", surface);
        Assert.Contains("LocalData.ResetBackupDirectoryCommand", surface);
        Assert.Contains("Backup now", surface);
        Assert.Contains("Export backup", surface);
        Assert.Contains("five most recent rolling backups", surface);
        Assert.Contains("Content imports are backed up before commit", surface);
        Assert.Contains("LocalDataSettingsStore", composition);
        Assert.Contains("LocalSqliteAutomaticBackupPolicy", composition);
        Assert.Contains("BackupBeforeContentAcquisitionPersistence", composition);
        Assert.DoesNotContain("Restore backup", surface);
        Assert.DoesNotContain("pending restore", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LocalSqliteRestoreService", composition);
        Assert.False(File.Exists(Path.Combine(root, "src", "Illumination.Infrastructure", "Persistence", "LocalSqliteRestoreService.cs")));
    }

    [Fact]
    public void Import_page_exposes_a_real_drop_surface()
    {
        var root = FindRepositoryRoot();
        var import = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "ImportView.axaml"));
        Assert.Contains("DragDrop.AllowDrop=\"True\"", import);
        Assert.Contains("DragDrop.Drop=\"OnBundleDrop\"", import);
        Assert.Contains("DragDrop.DragOver=\"OnBundleDragOver\"", import);
    }

    private static string ReadProductSurface(string root) =>
        File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "IlluminationProductSurface.axaml"));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Illumination.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the Illumination repository root.");
    }
}
