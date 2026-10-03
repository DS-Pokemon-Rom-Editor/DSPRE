using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.Avalonia.ViewModels;
using DSPRE.Resources;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Views.World
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
        // Only gates one-time event-subscription wiring (below), NOT the actual data setup, which must
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
            // Each tab offers its own tour the first time it is on screen.
            foreach (Control tab in new Control[] { HeaderEmbed, MapEmbed, EventsEmbed, MatrixEmbed, AreaDataEmbed, ScriptsEmbed, LevelScriptsEmbed, TextEmbed })
                EditorTours.Attach(tab, tab.GetType().Name);
        }

        public IEnumerable<(string EditorName, IEditorWithUnsavedChanges Editor)> GetEmbeddedEditors()
        {
            var editors = new List<(string, IEditorWithUnsavedChanges)>();
            void Add(string name, IEditorWithUnsavedChanges editor)
            {
                if (editor != null) editors.Add((name, editor));
            }

            Add("Header Editor", VM);
            Add("Map Editor", MapVM);
            Add("Matrix Editor", MatrixVM);
            Add("Area Data Editor", AreaDataVM);
            Add("Event Editor", EventVM);
            Add("Script Editor", ScriptsVM);
            Add("Level Script Editor", LevelScriptsVM);
            Add("Text Editor", TextVM);
            Add("Wild Encounters Editor", _encountersVm as IEditorWithUnsavedChanges);
            return editors;
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e) => await EnsureSetupAsync();

        /// <summary>
        /// Workspace setup. No-ops until a ROM is loaded; the workspace is created at app boot,
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
            // Tabs must unpack this ROM's archives before loading a new header, so header changes wait for setup.
            _settingUp = true;
            try { await SetUpTabsAsync(vm, owner); }
            finally { _settingUp = false; }
        }

        private bool _settingUp;

        private async System.Threading.Tasks.Task SetUpTabsAsync(HeaderEditorViewModel vm, Window owner)
        {
            await vm.SetupAsync(owner);

            if (!_wiringDone)
            {
                _wiringDone = true;
                owner.Activated += (_, _) => vm.ReloadLocationNames();
                vm.LinkedEditsWouldMove = id => TabsLeavingEdits(id).GetEnumerator().MoveNext();
                vm.ConfirmLinkedTabsAsync = async id =>
                {
                    foreach (var (editor, what) in TabsLeavingEdits(id))
                        if (!await RecordSwitchGuard.ConfirmLeaveAsync(editor, owner, what)) return false;
                    return true;
                };
                vm.PropertyChanged += (_, e) =>
                {
                    if (_settingUp) return;
                    switch (e.PropertyName)
                    {
                        case nameof(HeaderEditorViewModel.EventFileId): RetargetEvents(); break;
                        case nameof(HeaderEditorViewModel.MatrixId): RetargetMatrix(); break;
                        case nameof(HeaderEditorViewModel.AreaDataId): RetargetAreaData(); break;
                        case nameof(HeaderEditorViewModel.ScriptFileId): RetargetScripts(); break;
                        case nameof(HeaderEditorViewModel.LevelScriptId): RetargetLevelScripts(); break;
                        case nameof(HeaderEditorViewModel.TextArchiveId): RetargetText(); break;
                        case nameof(HeaderEditorViewModel.WildPokemon): RetargetEncounters(); break;
                        case nameof(HeaderEditorViewModel.CurrentHeaderId): MapVM.HeaderId = vm.CurrentHeaderId; MatrixVM.FocusHeader = vm.CurrentHeaderId; break;
                    }
                };
            }

            // Every tab follows the selected header's linked file id; refresh on every ROM load (a
            // coincidental same numeric id across two different ROMs must still force a real reload,
            // so reset first rather than relying on the property setters' equality-skip).
            EventVM.InitialIndex = (int)vm.EventFileId;
            MatrixVM.InitialIndex = (int)vm.MatrixId;
            MatrixVM.FocusHeader = vm.CurrentHeaderId;
            MatrixVM.OpenHeader = id => vm.GoToHeader(id);
            AreaDataVM.InitialIndex = (int)vm.AreaDataId;
            ScriptsVM.InitialIndex = (int)vm.ScriptFileId;
            LevelScriptsVM.InitialIndex = (int)vm.LevelScriptId;
            // The preview runs what the map runs by itself, so the events tab needs to know which
            // level script file the header points at.
            EventVM.LevelScriptId = (int)vm.LevelScriptId;
            EventVM.TextArchiveId = (int)vm.TextArchiveId;
            TextVM.InitialIndex = (int)vm.TextArchiveId;

            // Tabs that latched their no-ROM state at boot get to set up now. Pass our own resolved
            // owner through explicitly: these controls live in non-selected TabItems (Header is the
            // default), so TopLevel.GetTopLevel(this) on them returns null this early. Their own
            // EnsureSetupAsync used to silently no-op until the tab was manually visited once, which
            // for Map meant BuildHeaderPreview() built a real stitched model with zero MapLoaded
            // subscribers (GlView never got it, so it stayed stuck showing its placeholder cube).
            await EventsEmbed.EnsureSetupAsync(owner);
            await MapEmbed.EnsureSetupAsync(owner);
            // Default the embedded Map tab to "This header" (rather than an arbitrary single map) now
            // that SetupAsync has unpacked everything BuildHeaderPreview needs. Reset first so this
            // always forces a rebuild even if it was already 2 from a previous ROM in this session.
            MapVM.ViewModeIndex = 0;
            MapVM.HeaderId = -1;
            MapVM.HeaderId = vm.CurrentHeaderId;
            MapVM.ViewModeIndex = 2;
            await MatrixEmbed.EnsureSetupAsync(owner);
            await AreaDataEmbed.EnsureSetupAsync(owner);
            await ScriptsEmbed.EnsureSetupAsync(owner);
            await LevelScriptsEmbed.EnsureSetupAsync(owner);
            await TextEmbed.EnsureSetupAsync(owner);
            EnsureEncountersEmbedded();
        }

        /// <summary>Tabs holding unsaved edits to a file other than the one header <paramref name="id"/> links.</summary>
        private IEnumerable<(IEditorWithUnsavedChanges editor, string what)> TabsLeavingEdits(ushort id)
        {
            var h = DSPRE.ROMFiles.MapHeader.GetMapHeader(id);
            if (h == null) yield break;
            if (EventVM.HasUnsavedChanges && EventVM.SelectedEventIndex != h.eventFileID) yield return (EventVM, "event file");
            if (MatrixVM.HasUnsavedChanges && MatrixVM.SelectedMatrixIndex != h.matrixID) yield return (MatrixVM, "matrix");
            if (AreaDataVM.HasUnsavedChanges && AreaDataVM.SelectedIndex != h.areaDataID) yield return (AreaDataVM, "area");
            if (ScriptsVM.HasUnsavedChanges && !ScriptsVM.IsShowingScriptFile(h.scriptFileID)) yield return (ScriptsVM, "script");
            if (LevelScriptsVM.HasUnsavedChanges && LevelScriptsVM.SelectedScriptIndex != h.levelScriptID) yield return (LevelScriptsVM, "level script");
            if (TextVM.HasUnsavedChanges && TextVM.SelectedArchiveIndex != h.textArchiveID) yield return (TextVM, "text archive");
            if (MapVM.HasUnsavedChanges && MapVM.HeaderId != id) yield return (MapVM, "map");
            int wild = _encountersVm switch
            {
                WildEditorDPPtViewModel dppt => dppt.SelectedEncounterIndex,
                WildEditorHGSSViewModel hgss => hgss.SelectedEncounterIndex,
                _ => -1,
            };
            if (_encountersVm is IEditorWithUnsavedChanges enc && enc.HasUnsavedChanges && h.wildPokemon != ushort.MaxValue && wild != h.wildPokemon)
                yield return (enc, "encounter file");
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
            if (ScriptsVM.ScriptNames.Count > 0) ScriptsVM.SelectScriptFile(id);
        }

        private void RetargetLevelScripts()
        {
            var vm = VM; if (vm == null) return;
            int id = (int)vm.LevelScriptId;
            LevelScriptsVM.InitialIndex = id;
            if (LevelScriptsVM.ScriptNames.Count > 0) LevelScriptsVM.SelectedScriptIndex = id;
            EventVM.LevelScriptId = id;
        }

        private void RetargetText()
        {
            var vm = VM; if (vm == null) return;
            int id = (int)vm.TextArchiveId;
            TextVM.InitialIndex = id;
            if (TextVM.ArchiveNames.Count > 0) TextVM.SelectedArchiveIndex = id;
            EventVM.TextArchiveId = id;
        }

        /// <summary>Builds the Wild Encounters tab's editor. Rebuilds from scratch on EVERY ROM load
        /// (not just the first): the pokemon names, NARC path and header count are all ROM-specific,
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
                    Text = "hg-engine manages wild encounters. Edit them in your hg-engine project.",
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
                    EditorTours.Attach((Control)EncountersTab.Content, nameof(WildEditorDPPtView));
                }
                else
                {
                    var evm = new WildEditorHGSSViewModel(path, names, initial, headerCount);
                    _encountersVm = evm;
                    EncountersTab.Content = new WildEditorHGSSView(evm);
                    EditorTours.Attach((Control)EncountersTab.Content, nameof(WildEditorHGSSView));
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

        /// <summary>Opens the current tab's full editor, offering to save tab edits first since it reads from disk.</summary>
        private async void PopOut_Click(object sender, RoutedEventArgs e)
        {
            var vm = VM;
            if (vm == null || !AvaloniaEditorLauncher.IsRomLoaded) return;
            string tab = (MapTabs.SelectedItem as TabItem) is TabItem t ? global::Avalonia.Automation.AutomationProperties.GetName(t) : null;

            IEditorWithUnsavedChanges editor = tab switch
            {
                "Header" => vm,
                "Map" => MapVM,
                "Events" => EventVM,
                "Matrix" => MatrixVM,
                "Area Data" => AreaDataVM,
                "Encounters" => _encountersVm as IEditorWithUnsavedChanges,
                "Scripts" => ScriptsVM,
                "Level Scripts" => LevelScriptsVM,
                "Text" => TextVM,
                _ => null,
            };
            if (editor?.HasUnsavedChanges == true
                && await DialogHelper.AskYesNo("Save this tab's changes first? The window opens what is saved.", "Open in window"))
            {
                string failed = await UnsavedChangesDialog.TrySaveEditorAsync(editor);
                if (failed != null) { await DialogHelper.ShowError("The tab was not saved:\n" + failed, "Open in window"); return; }
            }

            switch (tab)
            {
                case "Header": AvaloniaEditorLauncher.OpenHeaderEditor(vm.CurrentHeaderId); break;
                case "Map": AvaloniaEditorLauncher.OpenMapEditor(MapVM.FocusedMapIndex); break;
                case "Events": AvaloniaEditorLauncher.OpenEventEditor(EventVM.SelectedEventIndex); break;
                case "Matrix": AvaloniaEditorLauncher.OpenMatrixEditor(MatrixVM.SelectedMatrixIndex); break;
                case "Area Data": AvaloniaEditorLauncher.OpenAreaDataEditor(AreaDataVM.SelectedIndex); break;
                case "Encounters":
                    int table = _encountersVm switch
                    {
                        WildEditorDPPtViewModel dppt => dppt.SelectedEncounterIndex,
                        WildEditorHGSSViewModel hgss => hgss.SelectedEncounterIndex,
                        _ => -1,
                    };
                    if (table >= 0) AvaloniaEditorLauncher.OpenWildEditor(table); else vm.OpenEncounters();
                    break;
                // Script lists can be ordered by source path, so the header's own file id is the reliable one.
                case "Scripts": vm.OpenScripts(); break;
                case "Level Scripts": AvaloniaEditorLauncher.OpenLevelScriptEditor(LevelScriptsVM.SelectedScriptIndex); break;
                case "Text": AvaloniaEditorLauncher.OpenTextEditor(TextVM.SelectedArchiveIndex); break;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private void Reset_Click(object sender, RoutedEventArgs e) => VM?.Reset();

        /// <summary>Builds a playable .nds, the same flow as the File menu's "Save ROM…", reachable
        /// without leaving the Maps workspace.</summary>
        private async void SaveRom_Click(object sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindowView main) await main.SaveRomAsync();
        }
    }
}
