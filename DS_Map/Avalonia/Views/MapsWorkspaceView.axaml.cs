using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia;
using DSPRE.Avalonia.ViewModels;
using DSPRE.Resources;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Views
{
    /// <summary>
    /// Maps workspace: the shared header sidebar + a context strip + map-bound tabs, all over one
    /// <see cref="HeaderEditorViewModel"/>. Every tab embeds the real editor and follows the selected
    /// header's linked file id; the header sidebar's context menu can still pop any of them out into
    /// their own window via <see cref="HeaderEditorViewModel"/>'s OpenXxx methods.
    /// </summary>
    public partial class MapsWorkspaceView : UserControl
    {
        private HeaderEditorViewModel VM => DataContext as HeaderEditorViewModel;
        // Only gates one-time event-subscription wiring (below) — NOT the actual data setup, which must
        // re-run every time a ROM is loaded (including switching to a DIFFERENT rom mid-session), or the
        // header sidebar and every tab stay frozen on whatever ROM was loaded first in the app's lifetime.
        private bool _wiringDone;

        public EventEditorViewModel EventVM { get; } = new EventEditorViewModel(true);
        public MapEditorViewModel MapVM { get; } = new MapEditorViewModel(true);
        public MatrixEditorViewModel MatrixVM { get; } = new MatrixEditorViewModel(true);
        public AreaDataEditorViewModel AreaDataVM { get; } = new AreaDataEditorViewModel(true);
        public ScriptEditorViewModel ScriptsVM { get; } = new ScriptEditorViewModel(true);
        public LevelScriptEditorViewModel LevelScriptsVM { get; } = new LevelScriptEditorViewModel(true);
        public TextEditorViewModel TextVM { get; } = new TextEditorViewModel(true);

        // The Wild Encounters editor needs gameFamily/NARC paths that don't exist at app boot, and its
        // VM type (DPPt vs HGSS) depends on gameFamily, so it's built once inside EnsureSetupAsync
        // instead of via a field initializer + XAML DataContext binding like the other tabs.
        private object _encountersVm;
        private bool _encountersEmbedded;

        public MapsWorkspaceView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e) => await EnsureSetupAsync();

        /// <summary>
        /// Workspace setup. No-ops until a ROM is loaded — the workspace is created at app boot,
        /// before any ROM; <see cref="MainWindowView"/> re-invokes this after EVERY successful load,
        /// including switching to a different ROM mid-session, so the data-refresh portion below
        /// always re-runs (only the event-subscription wiring is one-time, guarded by
        /// <see cref="_wiringDone"/>).
        /// </summary>
        public async System.Threading.Tasks.Task EnsureSetupAsync()
        {
            if (Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null || !AvaloniaEditorLauncher.IsRomLoaded) return;
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return;
            await vm.SetupAsync(owner);

            if (!_wiringDone)
            {
                _wiringDone = true;
                owner.Activated += (_, _) => vm.ReloadLocationNames();
                vm.PropertyChanged += (_, e) =>
                {
                    switch (e.PropertyName)
                    {
                        case nameof(HeaderEditorViewModel.EventFileId): RetargetEvents(); break;
                        case nameof(HeaderEditorViewModel.MatrixId): RetargetMatrix(); break;
                        case nameof(HeaderEditorViewModel.AreaDataId): RetargetAreaData(); break;
                        case nameof(HeaderEditorViewModel.ScriptFileId): RetargetScripts(); break;
                        case nameof(HeaderEditorViewModel.LevelScriptId): RetargetLevelScripts(); break;
                        case nameof(HeaderEditorViewModel.TextArchiveId): RetargetText(); break;
                        case nameof(HeaderEditorViewModel.WildPokemon): RetargetEncounters(); break;
                        case nameof(HeaderEditorViewModel.CurrentHeaderId): MapVM.HeaderId = vm.CurrentHeaderId; break;
                    }
                };
            }

            // Every tab follows the selected header's linked file id — refresh every ROM load (a
            // coincidental same numeric id across two different ROMs must still force a real reload,
            // so reset first rather than relying on the property setters' equality-skip).
            EventVM.InitialIndex = (int)vm.EventFileId;
            MatrixVM.InitialIndex = (int)vm.MatrixId;
            AreaDataVM.InitialIndex = (int)vm.AreaDataId;
            ScriptsVM.InitialIndex = (int)vm.ScriptFileId;
            LevelScriptsVM.InitialIndex = (int)vm.LevelScriptId;
            TextVM.InitialIndex = (int)vm.TextArchiveId;
            MapVM.HeaderId = -1;
            MapVM.HeaderId = vm.CurrentHeaderId;

            // Tabs that latched their no-ROM state at boot get to set up now. Pass our own resolved
            // owner through explicitly: these controls live in non-selected TabItems (Header is the
            // default), so TopLevel.GetTopLevel(this) on them returns null this early — their own
            // EnsureSetupAsync used to silently no-op until the tab was manually visited once, which
            // for Map meant BuildHeaderPreview() built a real stitched model with zero MapLoaded
            // subscribers (GlView never got it — stuck showing its placeholder cube).
            await EventsEmbed.EnsureSetupAsync(owner);
            await MapEmbed.EnsureSetupAsync(owner);
            // Default the embedded Map tab to "This header" (rather than an arbitrary single map) now
            // that SetupAsync has unpacked everything BuildHeaderPreview needs. Reset first so this
            // always forces a rebuild even if it was already 2 from a previous ROM in this session.
            MapVM.ViewModeIndex = 0;
            MapVM.ViewModeIndex = 2;
            await MatrixEmbed.EnsureSetupAsync(owner);
            await AreaDataEmbed.EnsureSetupAsync(owner);
            await ScriptsEmbed.EnsureSetupAsync(owner);
            await LevelScriptsEmbed.EnsureSetupAsync(owner);
            await TextEmbed.EnsureSetupAsync(owner);
            EnsureEncountersEmbedded();
        }

        /// <summary>Point the embedded Event editor at the current header's event file (live if it's already loaded).</summary>
        private void RetargetEvents()
        {
            var vm = VM; if (vm == null) return;
            int id = (int)vm.EventFileId;
            EventVM.InitialIndex = id;                 // used when the Events tab first sets up
            if (EventVM.EventNames.Count > 0)          // already set up → retarget in place
                EventVM.SelectedEventIndex = id;
        }

        private void RetargetMatrix()
        {
            var vm = VM; if (vm == null) return;
            int id = (int)vm.MatrixId;
            MatrixVM.InitialIndex = id;
            if (MatrixVM.MatrixNames.Count > 0) MatrixVM.SelectedMatrixIndex = id;
        }

        private void RetargetAreaData()
        {
            var vm = VM; if (vm == null) return;
            int id = (int)vm.AreaDataId;
            AreaDataVM.InitialIndex = id;
            if (AreaDataVM.AreaNames.Count > 0) AreaDataVM.SelectedIndex = id;
        }

        private void RetargetScripts()
        {
            var vm = VM; if (vm == null) return;
            int id = (int)vm.ScriptFileId;
            ScriptsVM.InitialIndex = id;
            if (ScriptsVM.ScriptNames.Count > 0) ScriptsVM.SelectedScriptIndex = id;
        }

        private void RetargetLevelScripts()
        {
            var vm = VM; if (vm == null) return;
            int id = (int)vm.LevelScriptId;
            LevelScriptsVM.InitialIndex = id;
            if (LevelScriptsVM.ScriptNames.Count > 0) LevelScriptsVM.SelectedScriptIndex = id;
        }

        private void RetargetText()
        {
            var vm = VM; if (vm == null) return;
            int id = (int)vm.TextArchiveId;
            TextVM.InitialIndex = id;
            if (TextVM.ArchiveNames.Count > 0) TextVM.SelectedArchiveIndex = id;
        }

        /// <summary>Builds the Wild Encounters tab's editor. Rebuilds from scratch on EVERY ROM load
        /// (not just the first) — the pokemon names, NARC path and header count are all ROM-specific,
        /// and the VM type itself (DPPt vs HGSS) depends on gameFamily, so a ROM switch that changes
        /// game family needs a genuinely different VM/View, not just a retarget.</summary>
        private void EnsureEncountersEmbedded()
        {
            if (!AvaloniaEditorLauncher.IsRomLoaded) return;
            if (RomInfo.isHGE)
            {
                // hg-engine owns encounter data; editing it here would be overwritten on its next build.
                EncountersTab.Content = new global::Avalonia.Controls.TextBlock
                {
                    Text = "Wild encounters are managed by hg-engine and can't be edited here.\n" +
                           "Edit them through your hg-engine project instead.",
                    Margin = new global::Avalonia.Thickness(16),
                    Opacity = 0.75,
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                };
                _encountersVm = null;
                _encountersEmbedded = true;
                return;
            }
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.encounters, DirNames.monIcons });
                string path = gameDirs[DirNames.encounters].unpackedDir;
                string[] names = GetPokemonNames();
                int headerCount = GetHeaderCount();
                int initial = VM != null && VM.CanOpenEncounters ? (int)VM.WildPokemon : 0;

                if (gameFamily == GameFamilies.DP || gameFamily == GameFamilies.Plat)
                {
                    var evm = new WildEditorDPPtViewModel(path, names, initial, headerCount);
                    _encountersVm = evm;
                    EncountersTab.Content = new WildEditorDPPtView(evm);
                }
                else
                {
                    var evm = new WildEditorHGSSViewModel(path, names, initial, headerCount);
                    _encountersVm = evm;
                    EncountersTab.Content = new WildEditorHGSSView(evm);
                }
                _encountersEmbedded = true;
            }
            catch (System.Exception ex)
            {
                _encountersEmbedded = false;
                _ = DialogHelper.ShowError($"Failed to set up the Wild Encounters editor:\n{ex.Message}", "Wild Encounters");
            }
        }

        /// <summary>Point the embedded Wild Encounters tab at the current header's encounter table.</summary>
        private void RetargetEncounters()
        {
            if (!_encountersEmbedded) { EnsureEncountersEmbedded(); return; }
            var vm = VM;
            if (vm == null || !vm.CanOpenEncounters) return;
            int id = (int)vm.WildPokemon;
            switch (_encountersVm)
            {
                case WildEditorDPPtViewModel dppt: dppt.SelectedEncounterIndex = id; break;
                case WildEditorHGSSViewModel hgss: hgss.SelectedEncounterIndex = id; break;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private void Reset_Click(object sender, RoutedEventArgs e) => VM?.Reset();

        /// <summary>Builds a playable .nds — same flow as the File menu's "Save ROM…", just reachable
        /// without leaving the Maps workspace (this used to be the only visible Save button here, easily
        /// mistaken for "save the whole ROM" when it only ever saved the current header's own fields).</summary>
        private async void SaveRom_Click(object sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindowView main) await main.SaveRomAsync();
        }
    }
}
