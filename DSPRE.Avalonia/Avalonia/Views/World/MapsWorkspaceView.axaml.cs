using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.Avalonia.ViewModels;
using DSPRE.Resources;
using static DSPRE.RomInfo;
using DSPRE.ROMFiles;

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
            MapTabs.SelectionChanged += (_, _) => UpdatePopOut();
            UpdatePopOut();
        }

        // Ctrl+Z / Ctrl+Y undo the tab on show. Listened for on the window, so it also works while nothing
        // inside the tab has focus; the main window has no undo of its own.
        private TopLevel _undoTop;
        protected override void OnAttachedToVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _undoTop = TopLevel.GetTopLevel(this);
            _undoTop?.AddHandler(KeyDownEvent, RouteUndo, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }

        protected override void OnDetachedFromVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            _undoTop?.RemoveHandler(KeyDownEvent, RouteUndo);
            _undoTop = null;
            base.OnDetachedFromVisualTree(e);
        }

        private object SelectedTabEditor() =>
            ((MapTabs.SelectedItem as TabItem) is TabItem t ? global::Avalonia.Automation.AutomationProperties.GetName(t) : null) switch
            {
                "Header" => VM,
                "Map" => MapVM,
                "Events" => EventVM,
                "Matrix" => MatrixVM,
                "Area Data" => AreaDataVM,
                "Encounters" => _encountersVm,
                "Scripts" => ScriptsVM,
                "Level Scripts" => LevelScriptsVM,
                "Text" => TextVM,
                _ => null,
            };

        private void RouteUndo(object sender, global::Avalonia.Input.KeyEventArgs e)
        {
            if (!IsEffectivelyVisible || e.KeyModifiers != global::Avalonia.Input.KeyModifiers.Control
                || (e.Key != global::Avalonia.Input.Key.Z && e.Key != global::Avalonia.Input.Key.Y)) return;
            // A text box keeps its own undo.
            if (e.Source is TextBox || e.Source is AvaloniaEdit.Editing.TextArea) return;
            object editor = SelectedTabEditor();
            if (editor == null) return;
            e.Handled = true;
            if (editor is DSPRE.Avalonia.ISupportsUndo undo)
            {
                if (e.Key == global::Avalonia.Input.Key.Z && undo.CanUndo) undo.Undo();
                else if (e.Key == global::Avalonia.Input.Key.Y && undo.CanRedo) undo.Redo();
            }
        }

        public IEnumerable<(string EditorName, IEditorWithUnsavedChanges Editor)> GetEmbeddedEditors()
        {
            List<(string, IEditorWithUnsavedChanges)> editors = new List<(string, IEditorWithUnsavedChanges)>();
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
            HeaderEditorViewModel vm = VM;
            if (vm == null || !AvaloniaEditorLauncher.IsRomLoaded) return;
            Window owner = TopLevel.GetTopLevel(this) as Window;
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
                vm.OpenInTab = ShowTab;
                vm.LinkedEditsWouldMove = id => TabsLeavingEdits(id).GetEnumerator().MoveNext();
                vm.ConfirmLinkedTabsAsync = async id =>
                {
                    foreach ((IEditorWithUnsavedChanges editor, string what) in TabsLeavingEdits(id))
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
            foreach (Control embed in new Control[] { EventsEmbed, MatrixEmbed, AreaDataEmbed, ScriptsEmbed, LevelScriptsEmbed, TextEmbed })
                LockToHeader(embed);
            if (!_followGuarded)
            {
                _followGuarded = true;
                Follow(EventVM, nameof(EventEditorViewModel.SelectedEventIndex), () => EventVM.SelectedEventIndex, () => (int)vm.EventFileId,
                       id => AvaloniaEditorLauncher.OpenEventEditor(id), RetargetEvents);
                Follow(MatrixVM, nameof(MatrixEditorViewModel.SelectedMatrixIndex), () => MatrixVM.SelectedMatrixIndex, () => (int)vm.MatrixId,
                       id => AvaloniaEditorLauncher.OpenMatrixEditor(id), RetargetMatrix);
                Follow(AreaDataVM, nameof(AreaDataEditorViewModel.SelectedIndex), () => AreaDataVM.SelectedIndex, () => (int)vm.AreaDataId,
                       id => AvaloniaEditorLauncher.OpenAreaDataEditor(id), RetargetAreaData);
                Follow(ScriptsVM, nameof(ScriptEditorViewModel.SelectedScriptIndex), () => ScriptsVM.ShowingScriptFileId, () => (int)vm.ScriptFileId,
                       id => AvaloniaEditorLauncher.OpenScriptEditor(id), RetargetScripts);
                Follow(LevelScriptsVM, nameof(LevelScriptEditorViewModel.SelectedScriptIndex), () => LevelScriptsVM.SelectedScriptIndex, () => (int)vm.LevelScriptId,
                       id => AvaloniaEditorLauncher.OpenLevelScriptEditor(id), RetargetLevelScripts);
                Follow(TextVM, nameof(TextEditorViewModel.SelectedArchiveIndex), () => TextVM.SelectedArchiveIndex, () => (int)vm.TextArchiveId,
                       id => AvaloniaEditorLauncher.OpenTextEditor(id), RetargetText);
            }
        }

        private bool _followGuarded;

        /// <summary>
        /// Keeps a tab on the header's own file. A jump inside the editor (a warp's go-to, next and previous, Add) that
        /// lands on another file opens that file in the editor's own window and puts the tab back.
        /// </summary>
        private void Follow(System.ComponentModel.INotifyPropertyChanged tabVm, string property, System.Func<int> showing,
                            System.Func<int> wanted, System.Action<int> openElsewhere, System.Action putBack)
        {
            tabVm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != property || _settingUp || VM == null) return;
                int now = showing(), want = wanted();
                if (now < 0 || now == want) return;
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (showing() == wanted()) return;
                    openElsewhere(now);
                    putBack();
                });
            };
        }

        /// <summary>
        /// A tab here shows the selected header's own file, so its file picker follows the header instead of being
        /// pickable: editing a file you think belongs to this header but does not is the mistake this prevents.
        /// The tab's Open in window gives the free editor.
        /// </summary>
        private static void LockToHeader(Control embed)
        {
            if (embed?.FindControl<Control>("FilePicker") is not Control picker) return;
            picker.IsEnabled = false;
            ToolTip.SetTip(picker, "Follows the selected header. Open in window to pick another file.");
            ToolTip.SetShowOnDisabled(picker, true);
        }

        private Control _encountersView;
        private readonly TextBlock _noEncounters = new()
        {
            Text = "This header has no wild Pokémon.",
            Margin = new global::Avalonia.Thickness(16),
            Opacity = 0.75,
        };

        /// <summary>Shows the encounter editor, or says there is nothing when the header links no encounter file (255),
        /// rather than leaving the last header's table up.</summary>
        private void ShowEncountersFor(bool hasEncounters)
        {
            if (_encountersView == null) return;
            object want = hasEncounters ? _encountersView : _noEncounters;
            if (!ReferenceEquals(EncountersTab.Content, want)) EncountersTab.Content = want;
        }

        /// <summary>Tabs holding unsaved edits to a file other than the one header <paramref name="id"/> links.</summary>
        private IEnumerable<(IEditorWithUnsavedChanges editor, string what)> TabsLeavingEdits(ushort id)
        {
            MapHeader h = DSPRE.ROMFiles.MapHeader.GetMapHeader(id);
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
            HeaderEditorViewModel vm = VM; if (vm == null) return;
            int id = (int)vm.EventFileId;
            EventVM.InitialIndex = id;                 // used when the Events tab first sets up
            if (EventVM.EventNames.Count > 0)          // already set up → retarget in place
                EventVM.SelectedEventIndex = id;
        }

        private void RetargetMatrix()
        {
            HeaderEditorViewModel vm = VM; if (vm == null) return;
            int id = (int)vm.MatrixId;
            MatrixVM.InitialIndex = id;
            if (MatrixVM.MatrixNames.Count > 0) MatrixVM.SelectedMatrixIndex = id;
        }

        private void RetargetAreaData()
        {
            HeaderEditorViewModel vm = VM; if (vm == null) return;
            int id = (int)vm.AreaDataId;
            AreaDataVM.InitialIndex = id;
            if (AreaDataVM.AreaNames.Count > 0) AreaDataVM.SelectedIndex = id;
        }

        private void RetargetScripts()
        {
            HeaderEditorViewModel vm = VM; if (vm == null) return;
            int id = (int)vm.ScriptFileId;
            ScriptsVM.InitialIndex = id;
            if (ScriptsVM.ScriptNames.Count > 0) ScriptsVM.SelectScriptFile(id);
        }

        private void RetargetLevelScripts()
        {
            HeaderEditorViewModel vm = VM; if (vm == null) return;
            int id = (int)vm.LevelScriptId;
            LevelScriptsVM.InitialIndex = id;
            if (LevelScriptsVM.ScriptNames.Count > 0) LevelScriptsVM.SelectedScriptIndex = id;
            EventVM.LevelScriptId = id;
        }

        private void RetargetText()
        {
            HeaderEditorViewModel vm = VM; if (vm == null) return;
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
                _encountersView = null;
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
                    WildEditorDPPtViewModel evm = new WildEditorDPPtViewModel(path, names, initial, headerCount);
                    _encountersVm = evm;
                    _encountersView = new WildEditorDPPtView(evm);
                    EncountersTab.Content = _encountersView;
                    EditorTours.Attach(_encountersView, nameof(WildEditorDPPtView));
                }
                else
                {
                    WildEditorHGSSViewModel evm = new WildEditorHGSSViewModel(path, names, initial, headerCount);
                    _encountersVm = evm;
                    _encountersView = new WildEditorHGSSView(evm);
                    EncountersTab.Content = _encountersView;
                    EditorTours.Attach(_encountersView, nameof(WildEditorHGSSView));
                }
                _encountersEmbedded = true;
                LockToHeader(_encountersView);
                switch (_encountersVm)
                {
                    case WildEditorDPPtViewModel dppt:
                        Follow(dppt, nameof(WildEditorDPPtViewModel.SelectedEncounterIndex), () => dppt.SelectedEncounterIndex,
                               () => VM != null && VM.CanOpenEncounters ? (int)VM.WildPokemon : dppt.SelectedEncounterIndex,
                               id => AvaloniaEditorLauncher.OpenWildEditor(id), RetargetEncounters);
                        break;
                    case WildEditorHGSSViewModel hgss:
                        Follow(hgss, nameof(WildEditorHGSSViewModel.SelectedEncounterIndex), () => hgss.SelectedEncounterIndex,
                               () => VM != null && VM.CanOpenEncounters ? (int)VM.WildPokemon : hgss.SelectedEncounterIndex,
                               id => AvaloniaEditorLauncher.OpenWildEditor(id), RetargetEncounters);
                        break;
                }
                ShowEncountersFor(VM != null && VM.CanOpenEncounters);
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
            HeaderEditorViewModel vm = VM;
            ShowEncountersFor(vm != null && vm.CanOpenEncounters);
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
            HeaderEditorViewModel vm = VM;
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

        /// <summary>The tab whose automation name is <paramref name="name"/>, or null.</summary>
        private TabItem TabNamed(string name)
        {
            foreach (object item in MapTabs.Items)
                if (item is TabItem t && global::Avalonia.Automation.AutomationProperties.GetName(t) == name) return t;
            return null;
        }

        /// <summary>Selects a tab by name. The header's Open buttons and the file chips land here.</summary>
        private bool ShowTab(string name)
        {
            if (TabNamed(name) is not TabItem tab) return false;
            MapTabs.SelectedItem = tab;
            return true;
        }

        private void Chip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control c && c.Tag is string name) ShowTab(name);
        }

        /// <summary>The pop-out button names the tab it opens, because it only ever opens that one.</summary>
        private void UpdatePopOut()
        {
            string tab = (MapTabs.SelectedItem as TabItem) is TabItem t ? global::Avalonia.Automation.AutomationProperties.GetName(t) : null;
            ToolTip.SetTip(PopOutButton, tab == null ? "Open this tab in its own window" : $"Open {tab} in its own window, on the same map or file");
            global::Avalonia.Automation.AutomationProperties.SetName(PopOutButton, "Open in window");
        }

        /// <summary>Builds a playable .nds, the same flow as the File menu's "Save ROM…", reachable
        /// without leaving the Maps workspace.</summary>
        private async void SaveRom_Click(object sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindowView main) await main.SaveRomAsync();
        }
    }
}
