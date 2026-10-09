using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class MoveDataEditorView : Window
    {
        private MoveDataEditorViewModel ViewModel => (MoveDataEditorViewModel)DataContext;

        public MoveDataEditorView()
        {
            InitializeComponent();
            EditMoveScriptButton.IsVisible = BetaEditors.Allows("BattleScriptEditorView");
            DataContext = new MoveDataEditorViewModel();
            // VM owns the bound Title (+ "*" marker); chrome adds Ctrl+S + the close guard.
            EditorWindowChrome.Attach(this, ViewModel, manageTitle: false, onClosed: ViewModel.Detach);
            ViewModel.NeedsExpansion += (kind, listed) => _ = OfferExpansionAsync(kind, listed);
        }

        /// <summary>Changing a list means moving it out of the game's overlay first, which its toolbox patch does.</summary>
        private async System.Threading.Tasks.Task OfferExpansionAsync(DSPRE.ROMFiles.MoveCategoryTable.Kind kind, bool listed)
        {
            string name = DSPRE.ROMFiles.MoveCategoryTable.NameOf(kind);
            try
            {
                string key = kind == DSPRE.ROMFiles.MoveCategoryTable.Kind.Punching ? "punchingMovesExpanded" : "soundMovesExpanded";
                await PatchHandover.OfferAsync(key, $"Expand the {name} move list", $"Changing the {name} move list", "Move Data Editor");
            }
            finally
            {
                // The binding drops a re-raised value it already read, so the boxes are set back from the lists directly.
                PunchingBox.IsChecked = ViewModel.IsPunching;
                SoundBox.IsChecked = ViewModel.IsSound;
                ViewModel.RefreshCategories();
            }
        }

        private static DSPRE.ROMFiles.MoveCategoryTable.Kind KindOf(object sender) =>
            sender is Control { Tag: "Sound" } ? DSPRE.ROMFiles.MoveCategoryTable.Kind.Sound : DSPRE.ROMFiles.MoveCategoryTable.Kind.Punching;

        private async void Guide_Click(object sender, RoutedEventArgs e)
            => await Launcher.LaunchUriAsync(new System.Uri(DSPRE.ROMFiles.MoveCategoryTable.GuideUrl(KindOf(sender))));

        // One list window per list; a second click brings the open one forward.
        private void ManageList_Click(object sender, RoutedEventArgs e)
        {
            DSPRE.ROMFiles.MoveCategoryTable.Kind kind = KindOf(sender);
            foreach (Window w in OwnedWindows)
                if (w is MoveListView open && open.Kind == kind) { open.Activate(); return; }
            new MoveListView(ViewModel, kind).Show(this);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await ViewModel.SaveCommand();

        private void Discard_Click(object sender, RoutedEventArgs e) => ViewModel.DiscardChanges();

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
