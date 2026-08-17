using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class MoveDataEditorView : Window
    {
        private MoveDataEditorViewModel ViewModel => (MoveDataEditorViewModel)DataContext;

        public MoveDataEditorView()
        {
            InitializeComponent();
            DataContext = new MoveDataEditorViewModel();
            // VM owns the bound Title (+ "*" marker); chrome adds Ctrl+S + the close guard.
            EditorWindowChrome.Attach(this, ViewModel, manageTitle: false, onClosed: ViewModel.Detach);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await ViewModel.SaveCommand();

        private void Undo_Click(object sender, RoutedEventArgs e) => ViewModel.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => ViewModel.Redo();

        private async void Export_Click(object sender, RoutedEventArgs e)
            => await ViewModel.ExportCommand(this);

        private async void Import_Click(object sender, RoutedEventArgs e)
            => await ViewModel.ImportCommand(this);

        private async void AddMove_Click(object sender, RoutedEventArgs e)
            => await ViewModel.AddNewMoveAsync(this);

        // Opens the battle-script editor at this move's waza_seq entry (archive 0 = Move scripts).
        private void EditMoveScript_Click(object sender, RoutedEventArgs e)
            => DSPRE.Avalonia.AvaloniaEditorLauncher.OpenBattleScriptEditor(0, ViewModel.SelectedMoveIndex);
    }
}
