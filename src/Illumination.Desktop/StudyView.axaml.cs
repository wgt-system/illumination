using Avalonia.Controls;
using Avalonia.Input;

namespace Illumination.Desktop;

public partial class StudyView : UserControl
{
    public StudyView()
    {
        InitializeComponent();
        KeyDown += OnStudyKeyDown;
    }

    private void OnStudyKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !vm.SessionIsActive) return;

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox or ComboBox or NumericUpDown) return;

        var command = e.Key switch
        {
            Key.D1 => vm.GradeNochmalCommand,
            Key.D2 => vm.GradeSchwerCommand,
            Key.D3 => vm.GradeUnsicherCommand,
            Key.D4 => vm.GradeGutCommand,
            Key.D5 => vm.GradeLeichtCommand,
            Key.Space => vm.RevealSolutionCommand,
            Key.H => vm.RevealHintCommand,
            Key.A => vm.RevealAssistanceCommand,
            _ => null,
        };

        if (command?.CanExecute(null) != true) return;
        command.Execute(null);
        e.Handled = true;
    }
}
