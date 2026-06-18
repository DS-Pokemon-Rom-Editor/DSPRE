using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class BattleDisplayEditorView : UserControl
    {
        public BattleDisplayEditorView()
        {
            InitializeComponent();
        }

        private void PlayProgramAnim_Click(object sender, RoutedEventArgs e)
            => (DataContext as BattleDisplayEditorViewModel)?.ToggleProgramAnim();

        private BattleDisplayEditorViewModel VM => DataContext as BattleDisplayEditorViewModel;
        private static ProgramCmdRow Row(object sender) => (sender as Control)?.DataContext as ProgramCmdRow;

        private void AddProgramCmd_Click(object sender, RoutedEventArgs e) => VM?.AddProgramCmd();
        private void SaveProgramScript_Click(object sender, RoutedEventArgs e) => VM?.SaveProgramScript();
        private void ProgramCmdUp_Click(object sender, RoutedEventArgs e) { var r = Row(sender); if (r != null) VM?.MoveProgramCmd(r, -1); }
        private void ProgramCmdDown_Click(object sender, RoutedEventArgs e) { var r = Row(sender); if (r != null) VM?.MoveProgramCmd(r, 1); }
        private void ProgramCmdRemove_Click(object sender, RoutedEventArgs e) { var r = Row(sender); if (r != null) VM?.RemoveProgramCmd(r); }

        private void AddAnimStep_Click(object sender, RoutedEventArgs e)
            => (DataContext as BattleDisplayEditorViewModel)?.AddAnimStep();

        private void RemoveAnimStep_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control c && c.DataContext is AnimPatternStep step)
                (DataContext as BattleDisplayEditorViewModel)?.RemoveAnimStep(step);
        }
    }
}
