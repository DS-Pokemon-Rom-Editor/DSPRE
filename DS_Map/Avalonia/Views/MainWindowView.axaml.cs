using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    /// <summary>
    /// Avalonia MainWindow shell (preview). Hosts migrated editors as tabs and
    /// launches the remaining standalone Avalonia editor windows via the menu.
    /// All launch logic is delegated to <see cref="AvaloniaEditorLauncher"/> so the
    /// behaviour stays identical to the WinForms main window.
    /// </summary>
    public partial class MainWindowView : Window
    {
        public MainWindowView()
        {
            InitializeComponent();
        }

        public MainWindowView(MainWindowViewModel vm) : this()
        {
            DataContext = vm;
        }

        // ── File ────────────────────────────────────────────────────────────
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // ── Pokémon ─────────────────────────────────────────────────────────
        private void PokemonEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenPokemonEditor();

        private void MoveDataEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenMoveDataEditor();

        private void TMEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTMEditor();

        private void EggMoveEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenEggMoveEditor();

        private void ItemEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenItemEditor();

        private void ItemTableEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenItemTableEditor();

        private void TradeEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTradeEditor();

        private void TrainerEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTrainerEditor();

        private void TextEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTextEditor();

        private void ScriptEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenScriptEditor();

        private void TableEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTableEditor();

        // ── World ───────────────────────────────────────────────────────────
        private void HeaderEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHeaderEditor();

        private void MapEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenMapEditor();

        private void MatrixEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenMatrixEditor();

        private void EventEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenEventEditor();

        private void NsbtxEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenNsbtxEditor();

        private void FlyWarpEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenFlyWarpEditor();

        private void OverlayEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenOverlayEditor();

        private void OverworldEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenOverworldEditor();

        private void EncountersEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenEncountersEditor();

        // ── Tools ───────────────────────────────────────────────────────────
        private void AddressHelper_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenAddressHelper();

        private void ResearchHelper_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenResearchHelper();

        private void CharMapManager_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenCharMapManager();

        private void Settings_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenSettings();

        private void GlTest_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenGlTest();
    }
}
