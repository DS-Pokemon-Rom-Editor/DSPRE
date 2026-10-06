using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using global::Avalonia.Controls;
using DSPRE.Avalonia;
using DSPRE.HgEngine;
using static DSPRE.RomInfo;
using System.Collections.Generic;

namespace DSPRE.Avalonia.ViewModels.Shell
{
    /// <summary>
    /// ViewModel for the Avalonia <c>MainWindowView</c> shell, the in-progress
    /// replacement for the WinForms main window.
    ///
    /// For now it hosts the editors that have already been ported to Avalonia
    /// <see cref="UserControl"/>s as embedded tabs (currently the Camera Editor),
    /// and exposes ROM state so the menu can launch the remaining editors (which
    /// still open as standalone Avalonia windows) only when a ROM is loaded.
    /// </summary>
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // ── Embedded editor sub-VMs ────────────────────────────────────────────
        public HeaderEditorViewModel HeaderVM { get; }

        // ── ROM state ──────────────────────────────────────────────────────────
        public bool IsRomLoaded => AvaloniaEditorLauncher.IsRomLoaded;

        // ── Per-editor availability (bound by menu items so unsupported editors are
        //    greyed out instead of carrying "(HGSS)"-style labels or failing silently).
        //    hg-engine ROMs: HGE owns/overwrites mon, move, item, trainer and encounter
        //    data, so those editors are disabled (mirrors the WinForms shell's HGE list),
        //    unless a source checkout is linked (HgEngineProject.IsActive), in which case
        //    the 5 covered domains read/write straight from source instead.
        // ── editors still being tried out ─────────────────────────────────────────────────────────

        /// <summary>
        /// Whether an editor may be opened, by the name of its window class.
        /// </summary>
        public BetaLookup Beta { get; } = new BetaLookup();

        /// <summary>
        /// Beta editors are left out of the menus rather than greyed out, so an ordinary run does not
        /// look like it is missing half its tools. A new instance on every read, so raising it re-reads
        /// the indexer after a ROM lets an editor through because it needs it.
        /// </summary>
        public BetaLookup Shown => new BetaLookup();

        /// <summary>Menu parts that only hold beta editors: separators around them and the Audio menu.</summary>
        public bool BetaOn => BetaEditors.Enabled;

        /// <summary>The status bar line when the unfinished editors are switched on.</summary>
        public string BetaNotice => $"Beta features on: {BetaEditors.Count} unfinished editors.";

        public bool HasBetaNotice => BetaEditors.Enabled;

        /// <summary>Tools for working on hg-engine itself, shown when DSPRE starts with --hge-dev.</summary>
        public bool HgeDevOn => DSPRE.HgEngine.HgEngineDev.Enabled;

        /// <summary>Why it is greyed out, or nothing when it is not.</summary>
        public BetaReason BetaNote { get; } = new BetaReason();

        public sealed class BetaLookup
        {
            public bool this[string window] => BetaEditors.Allows(window);
        }

        /// <summary>Whether an editor can open now, by its own availability rule. A new instance on every read, like Shown.</summary>
        public UsableLookup Usable => new UsableLookup();

        public sealed class UsableLookup
        {
            public bool this[string window] => EditorAvailability.Allows(window);
        }

        public sealed class BetaReason
        {
            public string this[string window] => BetaEditors.WhyNot(window);
        }

        private static bool HgAllows => !isHGE || HgEngineProject.IsActive;

        // Without a linked checkout, hg-engine's changed formats and tables would be corrupted by the vanilla
        // readers, so only the map, event, script and text editors stay open.
        public bool HgeUnlinkedAllows => HgAllows;

        /// <summary>Why an editor is greyed out on an hg-engine project with no linked checkout, or nothing.</summary>
        public string HgeUnlinkedNote => HgAllows ? null : HgEngineNote;

        /// <summary>The hg-engine reason before the beta one. A new instance on every read, so raising it re-reads the indexer.</summary>
        public BlockedReason BlockedNote => new BlockedReason(HgeUnlinkedNote);

        public sealed class BlockedReason
        {
            private readonly string _hge;
            public BlockedReason(string hge) { _hge = hge; }
            public string this[string window] => _hge ?? EditorAvailability.WhyNot(window);
        }

        public bool CanUseBattleScriptEditor => EditorAvailability.Allows("BattleScriptEditorView");
        public bool CanUseMoveBackgrounds => EditorAvailability.Allows("MoveBackgroundEditorView");
        public bool CanUseFontEditor => EditorAvailability.Allows("FontEditorView");
        public bool CanUseBattleSceneBrowser => EditorAvailability.Allows("BattleSceneBrowserView");
        public bool CanUseCellAnimations => EditorAvailability.Allows("CellAnimationEditorView");
        public bool CanUseParticles => EditorAvailability.Allows("ParticleLibraryView");
        public bool CanUseBallCapsules => EditorAvailability.Allows("BallCapsuleEditorView");
        public bool CanUseTilesetBuilder => EditorAvailability.Allows("TilesetBuilderView");
        public bool CanUseAudioEditor => EditorAvailability.Allows("AudioEditorView");
        public bool CanUseProjectChecks => EditorAvailability.Allows("ProjectChecksView");
        public bool CanUseBannerEditor => EditorAvailability.Allows("BannerEditorView");
        public bool CanUseNamingScreenEditor => EditorAvailability.Allows("NamingScreenEditor");
        public bool CanUseDataExports => EditorAvailability.Allows("DataExports");
        // The one editor that stays open on an hg-engine ROM with no checkout linked: it only reads,
        // and reading the ROM is the point of it.
        public bool CanUseHgeRomReview => EditorAvailability.Allows("HgeRomReviewView");
        public bool CanUseDistortionWorld => EditorAvailability.Allows("DistortionWorldView");
        public bool CanUseWildHeldItems => EditorAvailability.Allows("WildHeldItemOddsView");
        public bool CanUseGrowthCurves => EditorAvailability.Allows("GrowthCurveEditorView");
        public bool CanUseBreedingItems => EditorAvailability.Allows("BreedingItemsView");
        public bool CanUseBerryData => EditorAvailability.Allows("BerryDataEditorView");
        public bool CanUseTypeChart => EditorAvailability.Allows("TypeChartEditorView");
        public bool CanUseVsIntroEditor => EditorAvailability.Allows("VsIntroEditorView");
        public bool CanUseWildIntroEditor => EditorAvailability.Allows("WildIntroEditorView");
        public bool CanUseMoveTutors => EditorAvailability.Allows("MoveTutorEditorView");
        public bool CanUseMining => EditorAvailability.Allows("UndergroundMiningView");
        public bool CanUseBpShop => EditorAvailability.Allows("BpShopEditorView");
        public bool CanUseFriendship => EditorAvailability.Allows("FriendshipChangesView");
        public bool CanUseSlotOdds => EditorAvailability.Allows("EncounterSlotOddsView");
        // Tables a game family doesn't have are hidden rather than greyed out.
        public bool HasMoveTutors => !IsRomLoaded || gameFamily != GameFamilies.DP;
        public bool IsDpOrPlatinum => !IsRomLoaded || gameFamily != GameFamilies.HGSS;
        public bool CanUsePokemonEditor => EditorAvailability.Allows("PokemonEditorView");
        // PokeFormDataTbl.c is source-only (no packed-ROM equivalent), so this needs the checkout link
        // itself rather than the isHGE/HgAllows gate the other 5 domains use.
        public bool CanUseHgEngineFormEditor => EditorAvailability.Allows("HgEngineFormEditorView");
        public bool CanUseAbilityFlagsEditor => EditorAvailability.Allows("AbilityFlagsEditorView");
        public bool CanUseBattleScreen => EditorAvailability.Allows("BattleScreenEditorView");

        public bool CanUseMoveEditor => EditorAvailability.Allows("MoveDataEditorView");
        public bool CanUseItemEditor => EditorAvailability.Allows("ItemEditorView");
        public bool CanUseMartEditor => EditorAvailability.Allows("MartEditorView");
        public string MartEditorNote => EditorAvailability.WhyNot("MartEditorView");
        public bool CanUseTrainerEditor => EditorAvailability.Allows("TrainerEditorView");
        // hg-engine keeps trainer sprites in its source, which the editor reads and writes once a checkout is linked.
        public bool CanUseTrainerSpriteEditor => EditorAvailability.Allows("TrainerSpriteEditorView");
        public bool CanUseVsSeekerRematchEditor => EditorAvailability.Allows("VsSeekerRematchView");
        public bool CanUsePokegearRematchEditor => EditorAvailability.Allows("PokegearRematchView");
        public bool CanUsePokegearPhoneBook => EditorAvailability.Allows("PokegearPhoneBookView");
        public bool CanUseTrainerFlagBulkEditor => EditorAvailability.Allows("TrainerFlagBulkEditorView");
        public bool CanUseBattleTowerEditor => EditorAvailability.Allows("BattleTowerEditorView");
        public bool CanUseStarterEditor => EditorAvailability.Allows("StarterEditorView");
        public bool CanUseDungeonCutinEditor => EditorAvailability.Allows("DungeonCutinEditorView");
        public bool CanUseTitleScreenEditor => EditorAvailability.Allows("TitleScreenEditorView");
        public bool CanUseTrainerCardEditor => EditorAvailability.Allows("TrainerCardEditorView");
        public string TitleScreenEditorNote => EditorAvailability.WhyNot("TitleScreenEditorView");
        public string DungeonCutinEditorNote => EditorAvailability.WhyNot("DungeonCutinEditorView");
        public string TrainerCardEditorNote => EditorAvailability.WhyNot("TrainerCardEditorView");
        public bool CanUseBottomScreenEditor => EditorAvailability.Allows("BottomScreenEditorView");
        public string BottomScreenEditorNote => EditorAvailability.WhyNot("BottomScreenEditorView");

        public bool CanUseWildEditors => EditorAvailability.Allows("WildEditorView");
        // With a linked hg-engine checkout only its Headbutt tab is safe; hg-engine builds the rest itself.
        public bool CanUseSpecialEncountersEditor => IsRomLoaded && (!isHGE || (IsHgssRom && HgEngineProject.IsActive));
        public bool IsHgEngineLinked    => HgEngineProject.IsActive;
        // hg-engine's real `make` build, not one of the 5 read/write-covered domains, so this only
        // needs the checkout link itself (like CanUseHgEngineFormEditor), not the HgAllows gate.
        public bool CanCompileRom       => IsRomLoaded && HgEngineProject.IsActive;

        public bool BuildAndRunCompiles
        {
            get => SettingsManager.Settings?.buildAndRunCompiles ?? true;
            set
            {
                DspreSettings settings = SettingsManager.Settings;
                if (settings == null || settings.buildAndRunCompiles == value) return;
                settings.buildAndRunCompiles = value;
                SettingsManager.Save();
                OnPropertyChanged();
            }
        }

        /// <summary>Why Compile ROM and the source-backed editors are greyed out, or nothing when they are not.</summary>
        public string HgEngineNote =>
            !IsRomLoaded ? "Open a ROM first."
            : !isHGE ? "This ROM is not an hg-engine build."
            : !HgEngineProject.IsLinked ? "No hg-engine checkout is linked to this project."
            : !HgEngineProject.Enabled ? "The linked hg-engine checkout is switched off for this project."
            : "Runs the linked checkout's real make build and produces test.nds.";
        public bool IsHgssRom           => IsRomLoaded && gameFamily == GameFamilies.HGSS;

        /// <summary>The Headbutt editor needs an HGSS ROM, and it is still being tried out.</summary>
        // Diamond and Pearl only have the battle music table, where it is supported.
        public bool CanUseMiscTables => EditorAvailability.Allows("TableEditorView");

        // ── Busy state while a ROM is being opened/unpacked/saved, or an editor is unpacking its own data ──
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                ShowFacts(value);
            }
        }

        private string _busyFact = "";
        /// <summary>A Pokémon fact on the loading card, changing while it stays up.</summary>
        public string BusyFact
        {
            get => _busyFact;
            private set { if (_busyFact != value) { _busyFact = value; OnPropertyChanged(); } }
        }

        private global::Avalonia.Threading.DispatcherTimer _factTimer;

        private void ShowFacts(bool on)
        {
            _factTimer?.Stop();
            if (!on) return;
            BusyFact = PokeFacts.Next();
            _factTimer ??= new global::Avalonia.Threading.DispatcherTimer { Interval = PokeFacts.Interval };
            _factTimer.Tick -= NextFact;
            _factTimer.Tick += NextFact;
            _factTimer.Start();
        }

        private void NextFact(object sender, System.EventArgs e) => BusyFact = PokeFacts.Next();

        private string _busyText = "";
        public string BusyText
        {
            get => _busyText;
            set { if (_busyText != value) { _busyText = value; OnPropertyChanged(); } }
        }

        private string _busyHint = "First-time opens unpack the ROM and can take a little while.";
        public string BusyHint
        {
            get => _busyHint;
            set { if (_busyHint != value) { _busyHint = value; OnPropertyChanged(); } }
        }

        // ── Live status-bar line ───────────────────────────────────────────────
        public const string IdleStatus = "Open an editor from the menus, or press Ctrl+P and type its name.";
        private string _statusText = IdleStatus;
        public string StatusText
        {
            get => _statusText;
            set { if (_statusText != value) { _statusText = value; OnPropertyChanged(); } }
        }

        public string Title =>
            IsRomLoaded
                ? $"DSPRE - {GetGameDisplayName()}{ProjectNameSuffix} (Avalonia preview)"
                : "DSPRE (Avalonia preview)";

        /// <summary>" - name" of the open project folder, so two open projects of the same game can be told apart.</summary>
        private static string ProjectNameSuffix
        {
            get
            {
                string dir = RomInfo.workDir?.TrimEnd(System.IO.Path.DirectorySeparatorChar, '/');
                if (string.IsNullOrEmpty(dir)) return "";
                string name = System.IO.Path.GetFileName(dir);
                const string contents = "_DSPRE_contents";
                if (name.EndsWith(contents, System.StringComparison.OrdinalIgnoreCase)) name = name[..^contents.Length];
                return string.IsNullOrEmpty(name) ? "" : " - " + name;
            }
        }

        /// <summary>Re-evaluate ROM-dependent state after a ROM is loaded/closed (enables the editor menus + title).</summary>
        public void RefreshRomState()
        {
            HgEngineProject.Refresh();
            OnPropertyChanged(nameof(IsRomLoaded));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(CanUseBattleScreen));
            OnPropertyChanged(nameof(CanUsePokemonEditor));
            OnPropertyChanged(nameof(CanUseHgEngineFormEditor));
            OnPropertyChanged(nameof(CanUseAbilityFlagsEditor));
            OnPropertyChanged(nameof(CanUseMoveEditor));
            OnPropertyChanged(nameof(CanUseItemEditor));
            OnPropertyChanged(nameof(CanUseMartEditor));
            OnPropertyChanged(nameof(MartEditorNote));
            OnPropertyChanged(nameof(CanUseTrainerEditor));
            OnPropertyChanged(nameof(CanUseTrainerSpriteEditor));
            OnPropertyChanged(nameof(CanUseVsSeekerRematchEditor));
            OnPropertyChanged(nameof(CanUsePokegearRematchEditor));
            OnPropertyChanged(nameof(CanUsePokegearPhoneBook));
            OnPropertyChanged(nameof(CanUseTrainerFlagBulkEditor));
            OnPropertyChanged(nameof(CanUseBattleTowerEditor));
            OnPropertyChanged(nameof(CanUseStarterEditor));
            OnPropertyChanged(nameof(CanUseDungeonCutinEditor));
            OnPropertyChanged(nameof(CanUseTitleScreenEditor));
            OnPropertyChanged(nameof(CanUseTrainerCardEditor));
            OnPropertyChanged(nameof(DungeonCutinEditorNote));
            OnPropertyChanged(nameof(TitleScreenEditorNote));
            OnPropertyChanged(nameof(TrainerCardEditorNote));
            OnPropertyChanged(nameof(CanUseBottomScreenEditor));
            OnPropertyChanged(nameof(BottomScreenEditorNote));
            OnPropertyChanged(nameof(CanUseWildEditors));
            OnPropertyChanged(nameof(CanUseSpecialEncountersEditor));
            OnPropertyChanged(nameof(IsHgssRom));
            OnPropertyChanged(nameof(CanUseMiscTables));
            OnPropertyChanged(nameof(IsHgEngineLinked));
            OnPropertyChanged(nameof(CanCompileRom));
            OnPropertyChanged(nameof(HgEngineNote));
            OnPropertyChanged(nameof(HgeUnlinkedAllows));
            OnPropertyChanged(nameof(HgeUnlinkedNote));
            OnPropertyChanged(nameof(BlockedNote));
            OnPropertyChanged(nameof(Shown));
            OnPropertyChanged(nameof(Usable));
            OnPropertyChanged(nameof(CanUseBattleScriptEditor));
            OnPropertyChanged(nameof(CanUseMoveBackgrounds));
            OnPropertyChanged(nameof(CanUseFontEditor));
            OnPropertyChanged(nameof(CanUseBattleSceneBrowser));
            OnPropertyChanged(nameof(CanUseCellAnimations));
            OnPropertyChanged(nameof(CanUseParticles));
            OnPropertyChanged(nameof(CanUseBallCapsules));
            OnPropertyChanged(nameof(CanUseTilesetBuilder));
            OnPropertyChanged(nameof(CanUseAudioEditor));
            OnPropertyChanged(nameof(CanUseProjectChecks));
            OnPropertyChanged(nameof(CanUseBannerEditor));
            OnPropertyChanged(nameof(CanUseNamingScreenEditor));
            OnPropertyChanged(nameof(CanUseDataExports));
            OnPropertyChanged(nameof(CanUseHgeRomReview));
            OnPropertyChanged(nameof(CanUseDistortionWorld));
            OnPropertyChanged(nameof(CanUseWildHeldItems));
            OnPropertyChanged(nameof(CanUseGrowthCurves));
            OnPropertyChanged(nameof(CanUseBreedingItems));
            OnPropertyChanged(nameof(CanUseBerryData));
            OnPropertyChanged(nameof(CanUseTypeChart));
            OnPropertyChanged(nameof(CanUseVsIntroEditor));
            OnPropertyChanged(nameof(CanUseWildIntroEditor));
            OnPropertyChanged(nameof(CanUseMoveTutors));
            OnPropertyChanged(nameof(CanUseMining));
            OnPropertyChanged(nameof(CanUseBpShop));
            OnPropertyChanged(nameof(CanUseFriendship));
            OnPropertyChanged(nameof(CanUseSlotOdds));
            OnPropertyChanged(nameof(HasMoveTutors));
            OnPropertyChanged(nameof(IsDpOrPlatinum));
            RefreshRecents();
        }

        /// <summary>Called after the hg-engine link/enable state changes (Link dialog), to refresh the
        /// menu without a full ROM-state pass.</summary>
        public void RefreshHgEngineState()
        {
            OnPropertyChanged(nameof(IsHgEngineLinked));
            OnPropertyChanged(nameof(CanUsePokemonEditor));
            OnPropertyChanged(nameof(CanUseHgEngineFormEditor));
            OnPropertyChanged(nameof(CanUseAbilityFlagsEditor));
            OnPropertyChanged(nameof(CanUseMoveEditor));
            OnPropertyChanged(nameof(CanUseItemEditor));
            OnPropertyChanged(nameof(CanUseMartEditor));
            OnPropertyChanged(nameof(MartEditorNote));
            OnPropertyChanged(nameof(CanUseTrainerEditor));
            OnPropertyChanged(nameof(CanUseTrainerSpriteEditor));
            OnPropertyChanged(nameof(CanUseTrainerFlagBulkEditor));
            OnPropertyChanged(nameof(CanUseWildEditors));
            OnPropertyChanged(nameof(CanCompileRom));
            OnPropertyChanged(nameof(HgEngineNote));
            OnPropertyChanged(nameof(HgeUnlinkedAllows));
            OnPropertyChanged(nameof(HgeUnlinkedNote));
            OnPropertyChanged(nameof(BlockedNote));
            OnPropertyChanged(nameof(Shown));
            OnPropertyChanged(nameof(Usable));
            OnPropertyChanged(nameof(CanUseBattleScriptEditor));
            OnPropertyChanged(nameof(CanUseMoveBackgrounds));
            OnPropertyChanged(nameof(CanUseFontEditor));
            OnPropertyChanged(nameof(CanUseBattleSceneBrowser));
            OnPropertyChanged(nameof(CanUseCellAnimations));
            OnPropertyChanged(nameof(CanUseParticles));
            OnPropertyChanged(nameof(CanUseBallCapsules));
            OnPropertyChanged(nameof(CanUseTilesetBuilder));
            OnPropertyChanged(nameof(CanUseAudioEditor));
            OnPropertyChanged(nameof(CanUseProjectChecks));
            OnPropertyChanged(nameof(CanUseBannerEditor));
            OnPropertyChanged(nameof(CanUseNamingScreenEditor));
            OnPropertyChanged(nameof(CanUseDataExports));
            OnPropertyChanged(nameof(CanUseHgeRomReview));
            OnPropertyChanged(nameof(CanUseDistortionWorld));
            OnPropertyChanged(nameof(CanUseWildHeldItems));
            OnPropertyChanged(nameof(CanUseGrowthCurves));
            OnPropertyChanged(nameof(CanUseBreedingItems));
            OnPropertyChanged(nameof(CanUseBerryData));
            OnPropertyChanged(nameof(CanUseTypeChart));
            OnPropertyChanged(nameof(CanUseVsIntroEditor));
            OnPropertyChanged(nameof(CanUseWildIntroEditor));
            OnPropertyChanged(nameof(CanUseMoveTutors));
            OnPropertyChanged(nameof(CanUseMining));
            OnPropertyChanged(nameof(CanUseBpShop));
            OnPropertyChanged(nameof(CanUseFriendship));
            OnPropertyChanged(nameof(CanUseSlotOdds));
            OnPropertyChanged(nameof(HasMoveTutors));
            OnPropertyChanged(nameof(IsDpOrPlatinum));
            OnPropertyChanged(nameof(CanUseBattleScreen));
            OnPropertyChanged(nameof(CanUseVsSeekerRematchEditor));
            OnPropertyChanged(nameof(CanUsePokegearRematchEditor));
            OnPropertyChanged(nameof(CanUsePokegearPhoneBook));
            OnPropertyChanged(nameof(CanUseBattleTowerEditor));
            OnPropertyChanged(nameof(CanUseStarterEditor));
            OnPropertyChanged(nameof(CanUseDungeonCutinEditor));
            OnPropertyChanged(nameof(CanUseTitleScreenEditor));
            OnPropertyChanged(nameof(CanUseTrainerCardEditor));
            OnPropertyChanged(nameof(DungeonCutinEditorNote));
            OnPropertyChanged(nameof(TitleScreenEditorNote));
            OnPropertyChanged(nameof(TrainerCardEditorNote));
            OnPropertyChanged(nameof(CanUseBottomScreenEditor));
            OnPropertyChanged(nameof(BottomScreenEditorNote));
            OnPropertyChanged(nameof(CanUseSpecialEncountersEditor));
            OnPropertyChanged(nameof(CanUseMiscTables));
        }

        // ── Recent projects for the pre-ROM empty state ────────────────────────
        public System.Collections.ObjectModel.ObservableCollection<string> RecentProjects { get; } = new();
        public bool HasRecents => RecentProjects.Count > 0;

        public void RefreshRecents()
        {
            RecentProjects.Clear();
            List<string> recents = SettingsManager.Settings?.recentProjects;
            if (recents != null)
                foreach (string r in recents.Take(5)) RecentProjects.Add(r);
            OnPropertyChanged(nameof(HasRecents));
        }

        // ── Design-time constructor ────────────────────────────────────────────
        public MainWindowViewModel()
        {
            HeaderVM = new HeaderEditorViewModel();
        }

        // ── Runtime constructor ────────────────────────────────────────────────
        public MainWindowViewModel(bool runtime)
        {
            HeaderVM = new HeaderEditorViewModel(runtime);
            RefreshRecents();
        }
    }
}
