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
            // Ctrl+P → quick-open command palette (jump to any editor by name).
            KeyDown += (s, e) =>
            {
                if (e.Key == global::Avalonia.Input.Key.P &&
                    e.KeyModifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Control))
                {
                    AvaloniaEditorLauncher.OpenCommandPalette(this);
                    e.Handled = true;
                }
            };
        }

        private void CommandPalette_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenCommandPalette(this);

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

        private void LevelScriptEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenLevelScriptEditor();

        private void TableEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTableEditor();

        private void HiddenItemsEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHiddenItemsEditor();

        private void PickupTableEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenPickupTableEditor();

        // ── World ───────────────────────────────────────────────────────────
        private void HeaderEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHeaderEditor();

        private void MapEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenMapEditor();

        private void BuildingEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenBuildingEditor();

        private void MatrixEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenMatrixEditor();

        private void EventEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenEventEditor();

        private void NsbtxEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenNsbtxEditor();

        private void AreaDataEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenAreaDataEditor();

        private void FlyWarpEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenFlyWarpEditor();

        private void OverlayEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenOverlayEditor();

        private void OverworldEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenOverworldEditor();

        private void EncountersEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenEncountersEditor();

        private void HeadbuttEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHeadbuttEncounterEditor();

        private void WildEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenWildEditor();

        // ── Tools ───────────────────────────────────────────────────────────
        private void AddressHelper_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenAddressHelper();

        private void ResearchHelper_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenResearchHelper();

        private void CharMapManager_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenCharMapManager();

        private void LabelEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenLabelEditor();

        private void ProjectChecks_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenProjectChecks();

        private void Settings_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenSettings();

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
            => DSPRE.Avalonia.ThemeManager.Toggle();

        private void GlTest_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenGlTest();
    }
}
