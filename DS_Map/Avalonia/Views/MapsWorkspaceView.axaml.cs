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
        private bool _setupDone;

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
        /// One-time workspace setup. No-ops until a ROM is loaded — the workspace is created at app
        /// boot, before any ROM; <see cref="MainWindowView"/> re-invokes this after a successful load.
        /// </summary>
        public async System.Threading.Tasks.Task EnsureSetupAsync()
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null || !AvaloniaEditorLauncher.IsRomLoaded) return;
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return;
            _setupDone = true;
            await vm.SetupAsync(owner);
            owner.Activated += (_, _) => vm.ReloadLocationNames();

            // Every tab follows the selected header's linked file id.
            EventVM.InitialIndex = (int)vm.EventFileId;
            MatrixVM.InitialIndex = (int)vm.MatrixId;
            AreaDataVM.InitialIndex = (int)vm.AreaDataId;
            ScriptsVM.InitialIndex = (int)vm.ScriptFileId;
            LevelScriptsVM.InitialIndex = (int)vm.LevelScriptId;
            TextVM.InitialIndex = (int)vm.TextArchiveId;
            MapVM.HeaderId = vm.CurrentHeaderId;
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

            // Tabs that latched their no-ROM state at boot get to set up now.
            await EventsEmbed.EnsureSetupAsync();
            await MapEmbed.EnsureSetupAsync();
            await MatrixEmbed.EnsureSetupAsync();
            await AreaDataEmbed.EnsureSetupAsync();
            await ScriptsEmbed.EnsureSetupAsync();
            await LevelScriptsEmbed.EnsureSetupAsync();
            await TextEmbed.EnsureSetupAsync();
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

        /// <summary>Builds the Wild Encounters tab's editor the first time a ROM is loaded (it needs
        /// gameFamily/NARC paths that don't exist yet at app boot), then keeps it retargeted after.</summary>
        private void EnsureEncountersEmbedded()
        {
            if (_encountersEmbedded || !AvaloniaEditorLauncher.IsRomLoaded) return;
            _encountersEmbedded = true;
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
    }
}
