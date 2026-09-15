using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using global::Avalonia.Controls;
using DSPRE.Avalonia;
using DSPRE.HgEngine;
using static DSPRE.RomInfo;

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
        /// Whether an editor may be opened, by the name of its window class. Bound from the menu as
        /// Beta[SomethingView] so a beta editor greys out with the rest of the disabled ones.
        /// </summary>
        public BetaLookup Beta { get; } = new BetaLookup();

        /// <summary>
        /// The line in the status bar saying why a good few entries are greyed out. It says only that
        /// they are not ready; how to switch them on is not something to put in front of everybody.
        /// </summary>
        public string BetaNotice => BetaEditors.Enabled
            ? $"Beta features on: {BetaEditors.Count} unfinished editors."
            : $"{BetaEditors.Count} editors are not available yet.";

        public bool HasBetaNotice => true;   // says which mode you are in, either way

        /// <summary>Why it is greyed out, or nothing when it is not.</summary>
        public BetaReason BetaNote { get; } = new BetaReason();

        public sealed class BetaLookup
        {
            public bool this[string window] => BetaEditors.Allows(window);
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
            public string this[string window] => _hge ?? BetaEditors.WhyNot(window);
        }

        public bool CanUseBattleScriptEditor => HgAllows && Beta["BattleScriptEditorView"];
        public bool CanUseFontEditor         => HgAllows && Beta["FontEditorView"];
        public bool CanUseBattleSceneBrowser => HgAllows && Beta["BattleSceneBrowserView"];
        public bool CanUseCellAnimations     => HgAllows && Beta["CellAnimationEditorView"];
        public bool CanUseParticles          => HgAllows && Beta["ParticleLibraryView"];
        public bool CanUseBallCapsules       => HgAllows && Beta["BallCapsuleEditorView"];
        public bool CanUseTilesetBuilder     => HgAllows && Beta["TilesetBuilderView"];
        public bool CanUseAudioEditor        => HgAllows && Beta["AudioEditorView"];
        public bool CanUseProjectChecks      => HgAllows && Beta["ProjectChecksView"];
        public bool CanUseBannerEditor       => HgAllows && Beta["BannerEditorView"];
        public bool CanUseDataExports        => IsRomLoaded && HgAllows;
        public bool CanUsePokemonEditor => IsRomLoaded && HgAllows;
        // PokeFormDataTbl.c is source-only (no packed-ROM equivalent), so this needs the checkout link
        // itself rather than the isHGE/HgAllows gate the other 5 domains use.
        public bool CanUseHgEngineFormEditor => IsRomLoaded && HgEngineProject.IsActive
            && BetaEditors.Allows("HgEngineFormEditorView");
        public bool CanUseBattleScreen  => IsRomLoaded && HgAllows && Beta["BattleScreenEditorView"];

        public bool CanUseMoveEditor    => IsRomLoaded && HgAllows;
        public bool CanUseItemEditor    => IsRomLoaded && HgAllows;
        public bool CanUseMartEditor => IsRomLoaded && !isHGE && RomInfo.IsMartEditorAvailable()
            && BetaEditors.Allows("MartEditorView");
        public string MartEditorNote
        {
            get
            {
                if (!IsRomLoaded) return "Open a ROM first.";
                if (isHGE) return "The Mart Editor is disabled for hg-engine ROMs.";
                if (!RomInfo.IsMartEditorAvailable())
                    return "The Mart Editor currently supports English Diamond, Pearl, Platinum, HeartGold and SoulSilver ROMs.";
                return BetaEditors.WhyNot("MartEditorView");
            }
        }
        public bool CanUseTrainerEditor => IsRomLoaded && HgAllows;
        // hg-engine keeps trainer sprites in its source, which the editor reads and writes once a checkout is linked.
        public bool CanUseTrainerSpriteEditor => IsRomLoaded && (!isHGE || HgEngineProject.IsActive)
            && BetaEditors.Allows("TrainerSpriteEditorView");
        public bool CanUseVsSeekerRematchEditor => IsRomLoaded && HgAllows && VsSeekerRematchTable.IsSupported;
        public bool CanUsePokegearRematchEditor => IsRomLoaded && HgAllows && PokegearRematchTable.IsSupported;
        public bool CanUsePokegearPhoneBook => IsRomLoaded && HgAllows && PokegearPhoneBook.IsSupported && BetaEditors.Allows("PokegearPhoneBookView");
        public bool CanUseTrainerFlagBulkEditor => IsRomLoaded && HgAllows;
        public bool CanUseBattleTowerEditor => IsRomLoaded && HgAllows && DSPRE.ROMFiles.BattleTowerTrainerFile.IsAvailable() && DSPRE.ROMFiles.BattleTowerPokemonSetFile.IsAvailable();
        public bool CanUseStarterEditor => IsRomLoaded && !isHGE && RomInfo.IsStarterEditorAvailable();
        public bool CanUseDungeonCutinEditor => IsRomLoaded && HgAllows && RomInfo.IsDungeonCutinEditorAvailable()
            && BetaEditors.Allows("DungeonCutinEditorView");
        public bool CanUseTitleScreenEditor => IsRomLoaded && HgAllows && RomInfo.IsTitleScreenEditorAvailable()
            && BetaEditors.Allows("TitleScreenEditorView");
        public bool CanUseTrainerCardEditor => IsRomLoaded && HgAllows && RomInfo.IsTrainerCardEditorAvailable()
            && BetaEditors.Allows("TrainerCardEditorView");
        public string TitleScreenEditorNote => EditorNote(
            "TitleScreenEditorView", RomInfo.IsTitleScreenEditorAvailable(),
            "The Title Screen editor is available for HeartGold and SoulSilver ROMs.");
        public string DungeonCutinEditorNote => EditorNote(
            "DungeonCutinEditorView", RomInfo.IsDungeonCutinEditorAvailable(),
            "The Dungeon Cut-in editor is available for English and Spanish HeartGold and SoulSilver ROMs.");
        public string TrainerCardEditorNote => EditorNote(
            "TrainerCardEditorView", RomInfo.IsTrainerCardEditorAvailable(),
            "The Trainer Card editor is not available for this ROM.");
        public bool CanUseBottomScreenEditor => IsRomLoaded && HgAllows && RomInfo.IsBottomScreenEditorAvailable()
            && BetaEditors.Allows("BottomScreenEditorView");
        public string BottomScreenEditorNote => EditorNote(
            "BottomScreenEditorView", RomInfo.IsBottomScreenEditorAvailable(),
            "The Bottom Screen editor is not available for this ROM.");

        private string EditorNote(string window, bool supported, string unsupported)
        {
            if (!IsRomLoaded) return "Open a ROM first.";
            if (!HgAllows) return HgEngineNote;
            string beta = BetaEditors.WhyNot(window);
            return beta ?? (supported ? null : unsupported);
        }
        public bool CanUseWildEditors   => IsRomLoaded && HgAllows;
        // Special Encounters (Safari/Great Marsh-style tables) isn't one of the 5 hg-engine domains
        // DSPRE can read/write from source yet, so it stays blocked regardless of the link, unlike
        // CanUseWildEditors, which covers the actual wild-encounter table hg-engine does own.
        public bool CanUseSpecialEncountersEditor => IsRomLoaded && !isHGE;
        public bool CanUseTrophyGardenEditor => IsRomLoaded && HgAllows && DSPRE.ROMFiles.TrophyGardenEncounterFile.IsAvailable();
        public bool IsHgEngineLinked    => HgEngineProject.IsActive;
        // hg-engine's real `make` build, not one of the 5 read/write-covered domains, so this only
        // needs the checkout link itself (like CanUseHgEngineFormEditor), not the HgAllows gate.
        public bool CanCompileRom       => IsRomLoaded && HgEngineProject.IsActive;

        public bool BuildAndRunCompiles
        {
            get => SettingsManager.Settings?.buildAndRunCompiles ?? true;
            set
            {
                var settings = SettingsManager.Settings;
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
        public bool CanUseHeadbuttEditor => IsHgssRom && HgAllows;
        // Diamond and Pearl only have the battle music table, where it is supported.
        public bool CanUseMiscTables    => IsRomLoaded && HgAllows && (gameFamily != GameFamilies.DP || DSPRE.ROMFiles.BattleMusicTables.IsSupported);

        // ── Busy state while a ROM is being opened/unpacked/saved, or an editor is unpacking its own data ──
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { if (_isBusy != value) { _isBusy = value; OnPropertyChanged(); } }
        }

        private string _busyText = "Opening ROM…";
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
                ? $"DSPRE - {GetGameDisplayName()} (Avalonia preview)"
                : "DSPRE (Avalonia preview)";

        /// <summary>Re-evaluate ROM-dependent state after a ROM is loaded/closed (enables the editor menus + title).</summary>
        public void RefreshRomState()
        {
            HgEngineProject.Refresh();
            OnPropertyChanged(nameof(IsRomLoaded));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(CanUseBattleScreen));
            OnPropertyChanged(nameof(CanUsePokemonEditor));
            OnPropertyChanged(nameof(CanUseHgEngineFormEditor));
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
            OnPropertyChanged(nameof(CanUseTrophyGardenEditor));
            OnPropertyChanged(nameof(IsHgssRom));
            OnPropertyChanged(nameof(CanUseHeadbuttEditor));
            OnPropertyChanged(nameof(CanUseMiscTables));
            OnPropertyChanged(nameof(IsHgEngineLinked));
            OnPropertyChanged(nameof(CanCompileRom));
            OnPropertyChanged(nameof(HgEngineNote));
            OnPropertyChanged(nameof(HgeUnlinkedAllows));
            OnPropertyChanged(nameof(HgeUnlinkedNote));
            OnPropertyChanged(nameof(BlockedNote));
            OnPropertyChanged(nameof(CanUseBattleScriptEditor));
            OnPropertyChanged(nameof(CanUseFontEditor));
            OnPropertyChanged(nameof(CanUseBattleSceneBrowser));
            OnPropertyChanged(nameof(CanUseCellAnimations));
            OnPropertyChanged(nameof(CanUseParticles));
            OnPropertyChanged(nameof(CanUseBallCapsules));
            OnPropertyChanged(nameof(CanUseTilesetBuilder));
            OnPropertyChanged(nameof(CanUseAudioEditor));
            OnPropertyChanged(nameof(CanUseProjectChecks));
            OnPropertyChanged(nameof(CanUseBannerEditor));
            OnPropertyChanged(nameof(CanUseDataExports));
            RefreshRecents();
        }

        /// <summary>Called after the hg-engine link/enable state changes (Link dialog), to refresh the
        /// menu without a full ROM-state pass.</summary>
        public void RefreshHgEngineState()
        {
            OnPropertyChanged(nameof(IsHgEngineLinked));
            OnPropertyChanged(nameof(CanUsePokemonEditor));
            OnPropertyChanged(nameof(CanUseHgEngineFormEditor));
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
            OnPropertyChanged(nameof(CanUseBattleScriptEditor));
            OnPropertyChanged(nameof(CanUseFontEditor));
            OnPropertyChanged(nameof(CanUseBattleSceneBrowser));
            OnPropertyChanged(nameof(CanUseCellAnimations));
            OnPropertyChanged(nameof(CanUseParticles));
            OnPropertyChanged(nameof(CanUseBallCapsules));
            OnPropertyChanged(nameof(CanUseTilesetBuilder));
            OnPropertyChanged(nameof(CanUseAudioEditor));
            OnPropertyChanged(nameof(CanUseProjectChecks));
            OnPropertyChanged(nameof(CanUseBannerEditor));
            OnPropertyChanged(nameof(CanUseDataExports));
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
            OnPropertyChanged(nameof(CanUseTrophyGardenEditor));
            OnPropertyChanged(nameof(CanUseHeadbuttEditor));
            OnPropertyChanged(nameof(CanUseMiscTables));
        }

        // ── Recent projects for the pre-ROM empty state ────────────────────────
        public System.Collections.ObjectModel.ObservableCollection<string> RecentProjects { get; } = new();
        public bool HasRecents => RecentProjects.Count > 0;

        public void RefreshRecents()
        {
            RecentProjects.Clear();
            var recents = SettingsManager.Settings?.recentProjects;
            if (recents != null)
                foreach (var r in recents.Take(5)) RecentProjects.Add(r);
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
