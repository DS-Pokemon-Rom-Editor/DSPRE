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
            ? null
            : $"{BetaEditors.Count} editors are not available yet.";

        public bool HasBetaNotice => !BetaEditors.Enabled;

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
        public bool CanUsePokemonEditor => IsRomLoaded && HgAllows;
        // PokeFormDataTbl.c is source-only (no packed-ROM equivalent), so this needs the checkout link
        // itself rather than the isHGE/HgAllows gate the other 5 domains use.
        public bool CanUseHgEngineFormEditor => IsRomLoaded && HgEngineProject.IsActive
            && BetaEditors.Allows("HgEngineFormEditorView");
        public bool CanUseMoveEditor    => IsRomLoaded && HgAllows;
        public bool CanUseItemEditor    => IsRomLoaded && HgAllows;
        public bool CanUseTrainerEditor => IsRomLoaded && HgAllows;
        public bool CanUseTrainerSpriteEditor => IsRomLoaded && !isHGE
            && BetaEditors.Allows("TrainerSpriteEditorView");
        public bool CanUseVsSeekerRematchEditor => IsRomLoaded && VsSeekerRematchTable.IsSupported;
        public bool CanUseTrainerFlagBulkEditor => IsRomLoaded && HgAllows;
        public bool CanUseBattleTowerEditor => IsRomLoaded && DSPRE.ROMFiles.BattleTowerTrainerFile.IsAvailable() && DSPRE.ROMFiles.BattleTowerPokemonSetFile.IsAvailable()
            && BetaEditors.Allows("BattleTowerEditorView");
        public bool CanUseStarterEditor => IsRomLoaded && !isHGE && RomInfo.IsStarterEditorAvailable()
            && BetaEditors.Allows("StarterEditorView");
        public bool CanUseDungeonCutinEditor => IsRomLoaded && RomInfo.IsDungeonCutinEditorAvailable()
            && BetaEditors.Allows("DungeonCutinEditorView");
        public bool CanUseTitleScreenEditor => IsRomLoaded && RomInfo.IsTitleScreenEditorAvailable()
            && BetaEditors.Allows("TitleScreenEditorView");
        public bool CanUseTrainerCardEditor => IsRomLoaded && RomInfo.IsTrainerCardEditorAvailable()
            && BetaEditors.Allows("TrainerCardEditorView");
        public bool CanUseWildEditors   => IsRomLoaded && HgAllows;
        // Special Encounters (Safari/Great Marsh-style tables) isn't one of the 5 hg-engine domains
        // DSPRE can read/write from source yet, so it stays blocked regardless of the link, unlike
        // CanUseWildEditors, which covers the actual wild-encounter table hg-engine does own.
        public bool CanUseSpecialEncountersEditor => IsRomLoaded && !isHGE;
        public bool CanUseTrophyGardenEditor => IsRomLoaded && DSPRE.ROMFiles.TrophyGardenEncounterFile.IsAvailable()
            && BetaEditors.Allows("TrophyGardenEditorView");
        public bool IsHgEngineLinked    => HgEngineProject.IsActive;
        // hg-engine's real `make` build, not one of the 5 read/write-covered domains, so this only
        // needs the checkout link itself (like CanUseHgEngineFormEditor), not the HgAllows gate.
        public bool CanCompileRom       => IsRomLoaded && HgEngineProject.IsActive
            && BetaEditors.Allows("CompileRomView");
        public bool IsHgssRom           => IsRomLoaded && gameFamily == GameFamilies.HGSS;

        /// <summary>The Headbutt editor needs an HGSS ROM, and it is still being tried out.</summary>
        public bool CanUseHeadbuttEditor => IsHgssRom && BetaEditors.Allows("HeadbuttEncounterView");
        // Music & Battle Tables: conditional music + VS posters are HGSS, battle-FX combos
        // are Plat+HGSS; nothing in it exists on DP.
        public bool CanUseMiscTables    => IsRomLoaded && gameFamily != GameFamilies.DP;

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
        public const string IdleStatus = "Editors open from the menus, or press Ctrl+P and type an editor's name.";
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
            OnPropertyChanged(nameof(CanUsePokemonEditor));
            OnPropertyChanged(nameof(CanUseHgEngineFormEditor));
            OnPropertyChanged(nameof(CanUseMoveEditor));
            OnPropertyChanged(nameof(CanUseItemEditor));
            OnPropertyChanged(nameof(CanUseTrainerEditor));
            OnPropertyChanged(nameof(CanUseTrainerSpriteEditor));
            OnPropertyChanged(nameof(CanUseVsSeekerRematchEditor));
            OnPropertyChanged(nameof(CanUseTrainerFlagBulkEditor));
            OnPropertyChanged(nameof(CanUseBattleTowerEditor));
            OnPropertyChanged(nameof(CanUseStarterEditor));
            OnPropertyChanged(nameof(CanUseDungeonCutinEditor));
            OnPropertyChanged(nameof(CanUseTitleScreenEditor));
            OnPropertyChanged(nameof(CanUseTrainerCardEditor));
            OnPropertyChanged(nameof(CanUseWildEditors));
            OnPropertyChanged(nameof(CanUseSpecialEncountersEditor));
            OnPropertyChanged(nameof(CanUseTrophyGardenEditor));
            OnPropertyChanged(nameof(IsHgssRom));
            OnPropertyChanged(nameof(CanUseHeadbuttEditor));
            OnPropertyChanged(nameof(CanUseMiscTables));
            OnPropertyChanged(nameof(IsHgEngineLinked));
            OnPropertyChanged(nameof(CanCompileRom));
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
            OnPropertyChanged(nameof(CanUseTrainerEditor));
            OnPropertyChanged(nameof(CanUseTrainerFlagBulkEditor));
            OnPropertyChanged(nameof(CanUseWildEditors));
            OnPropertyChanged(nameof(CanCompileRom));
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
