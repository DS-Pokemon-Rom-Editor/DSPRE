using System.Collections.Generic;
using DSPRE.Avalonia.ViewModels;
using DSPRE.Avalonia.Views;
using DSPRE.HgEngine;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using System.Linq;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Centralised launch points for the already-migrated Avalonia editor windows.
    ///
    /// These mirror the per-editor launch logic that currently lives inline in the
    /// WinForms <c>Main Window.cs</c> handlers. Keeping them here lets the new
    /// Avalonia <see cref="Views.Shell.MainWindowView"/> open the same editors without
    /// duplicating the NARC-unpack + data-sourcing steps, and gives the WinForms
    /// side a single place to delegate to once it is retired.
    ///
    /// Every launcher is a no-op when no ROM is loaded, so callers can wire them to
    /// menu items without re-checking ROM state at each call site.
    /// </summary>
    public static class AvaloniaEditorLauncher
    {
        /// <summary>True once a ROM has been opened and <see cref="RomInfo"/> populated.</summary>
        public static bool IsRomLoaded => gameFamily != GameFamilies.NULL;

        /// <summary>Guard for editors whose data hg-engine owns/overwrites (mon, move, item,
        /// trainer, encounter data). The menu items are greyed out too, but this is the real
        /// chokepoint: it also covers the Ctrl+P palette and the header-tree context menu.
        /// Editors covering one of the 5 domains DSPRE can now read/write straight from a linked
        /// hg-engine checkout (<paramref name="ownedDomain"/>) are unblocked once that link is active;
        /// everything else stays blocked, since DSPRE would otherwise write to a ROM copy hg-engine's
        /// next build silently overwrites.</summary>
        private static bool BlockedForHge(string editorName, HgEngineDomain? ownedDomain = null)
        {
            if (!RomInfo.isHGE) return false;
            if (ownedDomain.HasValue && HgEngineProject.IsActive) return false;
            AppMessages.Info(editorName + " is disabled for hg-engine ROMs: hg-engine manages this " +
                "data itself and would overwrite any changes made here on its next build." +
                (ownedDomain.HasValue ? " Link your hg-engine checkout (File > Link hg-engine checkout…) to edit it from source instead." : ""),
                "Not available with hg-engine");
            return true;
        }

        /// <summary>Runs unpack-heavy file I/O off the UI thread behind the app's busy overlay. Only pass plain file I/O, not UI/bitmap work.</summary>
        private static async System.Threading.Tasks.Task RunBusyAsync(string busyText, string busyHint, System.Action work)
        {
            var app = global::Avalonia.Application.Current?.ApplicationLifetime
                as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
            var vm = app?.MainWindow?.DataContext as MainWindowViewModel;
            if (vm != null) { vm.BusyText = busyText; vm.BusyHint = busyHint; vm.IsBusy = true; }
            try { await System.Threading.Tasks.Task.Run(work); }
            finally { if (vm != null) vm.IsBusy = false; }
        }

        /// <summary>
        /// Everything in the ROM that makes a noise: the cries, the music, the fanfares and the sound
        /// effects, listed out of the sound archive's own names.
        /// </summary>
        /// <param name="showCryFor">A species to open on, for the Pokemon editor's own Edit cry button.
        /// Zero opens on nothing in particular.</param>
        public static async System.Threading.Tasks.Task OpenAudioEditorAsync(int showCryFor = 0)
        {
            if (!IsRomLoaded) return;

            try
            {
                string[] names;
                try { names = GetPokemonNamesWithForms(GetPersonalFilesCount()); }
                catch { names = System.Array.Empty<string>(); }

                await System.Threading.Tasks.Task.Yield();
                var vm = new ViewModels.Audio.AudioEditorViewModel(names);
                if (showCryFor > 0) vm.ShowCryFor(showCryFor);
                new Views.Audio.AudioEditorView(vm).ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("The Audio Editor could not be opened:" + System.Environment.NewLine + ex.Message, "Audio Editor");
            }
        }

        // ── Pokémon-related editors ────────────────────────────────────────────
        public static async System.Threading.Tasks.Task OpenPokemonEditorAsync(int initialMon = 1)
        {
            if (!IsRomLoaded || BlockedForHge("The Pokémon Editor", HgEngineDomain.Species)) return;

            try
            {
                await RunBusyAsync("Opening Pokémon Editor…",
                    "First-time opens unpack the ROM's data and can take a while, especially for a WSL-hosted project.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> {
                        DirNames.personalPokeData, DirNames.learnsets,
                        DirNames.evolutions, DirNames.monIcons }));
                SetMonIconsPalTableAddress();

                // Full Pokémon name list (base + alt forms this ROM actually has data for + extras)
                string[] fullList = GetPokemonNamesWithForms(GetPersonalFilesCount());
                string[] moveNames = GetAttackNames();

                int mon = System.Math.Clamp(initialMon, 0, System.Math.Max(0, fullList.Length - 1));
                var vm = new PokemonEditorViewModel(fullList, moveNames, initialMon: mon);
                new PokemonEditorView(vm).ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("Couldn't open the Pokémon Editor: " + ex.Message, "Pokémon Editor");
            }
        }

        /// <summary>hg-engine-only: which form species exist per base Pokémon (data/PokeFormDataTbl.c).
        /// No packed-ROM equivalent, so unlike the other 5 domains this needs neither a narc unpack nor
        /// isHGE/BlockedForHge gating: it simply doesn't exist without a linked, active checkout.</summary>
        public static void OpenHgEngineFormEditor()
        {
            if (!IsRomLoaded || !HgEngineProject.IsActive) return;
            var vm = new HgEngineFormEditorViewModel(GetPokemonNames());
            new HgEngineFormEditorView(vm).ShowManaged();
        }

        public static void OpenMoveDataEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded || BlockedForHge("The Move Data Editor", HgEngineDomain.Moves)) return;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.moveData });
            var view = new MoveDataEditorView();
            if (initialIndex > 0 && view.DataContext is MoveDataEditorViewModel vm)
                vm.SelectedMoveIndex = initialIndex;   // setter clamps + loads
            view.ShowManaged();
        }

        public static void OpenTMEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            var view = new TMEditorView();
            if (initialIndex > 0 && view.DataContext is TMEditorViewModel vm)
                vm.SelectedMachineIndex = initialIndex;   // setter loads the machine
            view.ShowManaged();
        }

        public static void OpenEggMoveEditor()
        {
            if (!IsRomLoaded) return;
            new EggMoveEditorView().ShowManaged();
        }

        /// <summary>Opens the battle-script editor (waza_seq / be_seq / sub_seq / WEST move-animation). When opened
        /// from the Move editor, <paramref name="archive"/>=0 + <paramref name="entryIndex"/>=move number jumps
        /// straight to that move's script.</summary>
        public static void OpenBattleScriptEditor(int archive = 0, int entryIndex = 0)
        {
            if (!IsRomLoaded) return;
            var vm = new BattleScriptEditorViewModel();
            var view = new BattleScriptEditorView { DataContext = vm };
            if (vm.IsAvailable)
            {
                vm.ArchiveIndex = System.Math.Clamp(archive, 0, 3);   // setter rebuilds the entry list
                if (entryIndex > 0 && vm.FileItems.Count > 0)
                    vm.SelectedFileIndex = System.Math.Min(entryIndex, vm.FileItems.Count - 1);
            }
            new EditorHostWindow("Battle Scripts", view, 1320, 800).ShowManaged();
        }

        public static void OpenItemEditor(int initialIndex = 1)
        {
            if (!IsRomLoaded || BlockedForHge("The Item Editor", HgEngineDomain.Items)) return;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.itemData });
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.itemIcons });
            var vm = new ItemEditorViewModel(GetItemNames());
            if (initialIndex > 0) vm.SelectedItemIndex = System.Math.Clamp(initialIndex, 0, vm.MaxItemIndex);
            new ItemEditorView(vm).ShowManaged();
        }

        public static void OpenItemTableEditor()
        {
            if (!IsRomLoaded || !IsItemTableEditorAvailable()) return;
            new ItemTableEditorView(new ItemTableEditorViewModel(GetItemNames(), HeaderLists.GetHeaderListBoxNames())).ShowManaged();
        }

        public static void OpenTradeEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.tradeData });
            var view = new TradeEditorView();
            if (initialIndex > 0 && view.DataContext is TradeEditorViewModel vm)
                _ = vm.ChangeTradeIDAsync(initialIndex);   // async load; freshly opened editor isn't dirty
            view.ShowManaged();
        }

        public static void OpenTextEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.textArchives });
            new EditorHostWindow("Text Editor",
                new TextEditorView(new TextEditorViewModel(true) { InitialIndex = initialIndex }),
                980, 640).ShowManaged();
        }

        public static void OpenScriptEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            new EditorHostWindow("Rotom Script Editor",
                new ScriptEditorView(new ScriptEditorViewModel(true) { InitialIndex = initialIndex }),
                980, 760).ShowManaged();
        }

        public static void OpenLevelScriptEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            new EditorHostWindow("Level Script Editor",
                new LevelScriptEditorView(new LevelScriptEditorViewModel(true) { InitialIndex = initialIndex }),
                720, 560).ShowManaged();
        }

        public static void OpenTableEditor()
        {
            // Nothing in this editor (conditional music / battle-FX combos / VS posters) exists on DP.
            if (!IsRomLoaded || gameFamily == GameFamilies.DP) return;
            new TableEditorView(new TableEditorViewModel(HeaderLists.GetHeaderListBoxNames())).ShowManaged();
        }

        public static void OpenEncountersEditor()
        {
            if (!IsRomLoaded || BlockedForHge("The Special Encounters editor")) return;
            new EncountersEditorView(new EncountersEditorViewModel(true)).ShowManaged();
        }

        public static void OpenHeadbuttEncounterEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded || gameFamily != GameFamilies.HGSS) return;
            new HeadbuttEncounterView(new HeadbuttEncounterViewModel(true) { InitialIndex = initialIndex }).ShowManaged();
        }

        public static void OpenTmHmBulkEditor()
        {
            if (!IsRomLoaded || BlockedForHge("The TM/HM Bulk Editor", HgEngineDomain.Species)) return;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.personalPokeData, DirNames.evolutions });
            var vm = new TmHmBulkEditorViewModel(GetPokemonNames());
            new EditorHostWindow("TM/HM Bulk Editor", new TmHmBulkEditorView(vm), 1050, 700).ShowManaged();
        }

        public static void OpenBattleTowerEditor()
        {
            if (!IsRomLoaded || !BattleTowerTrainerFile.IsAvailable() || !BattleTowerPokemonSetFile.IsAvailable())
            {
                if (IsRomLoaded) AppMessages.Warning("Battle Tower data was not found for this game.", "Not Available");
                return;
            }
            new EditorHostWindow("Battle Tower Editor",
                new BattleTowerEditorView(new BattleTowerEditorViewModel()),
                1000, 700).ShowManaged();
        }

        public static void OpenTrophyGardenEditor()
        {
            if (!IsRomLoaded || !TrophyGardenEncounterFile.IsAvailable()) return;
            new EditorHostWindow("Trophy Garden Editor",
                new TrophyGardenEditorView(new TrophyGardenEditorViewModel()),
                700, 500).ShowManaged();
        }

        public static void OpenWildEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded || BlockedForHge("The Wild Pokémon Editor", HgEngineDomain.Encounters)) return;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.encounters, DirNames.monIcons });
            string path = gameDirs[DirNames.encounters].unpackedDir;
            string[] names = GetPokemonNames();
            int headerCount = GetHeaderCount();
            // Standalone instances aren't shared with the embedded Maps-workspace tab, so each one must
            // unsubscribe its own AppEvents.NamesChanged hook when its window closes (EditorHostWindow has
            // no generic post-close hook for this; the embedded tab's single long-lived instance never
            // needs Detach at all, matching every other embedded-editor VM's lifetime).
            if (gameFamily == GameFamilies.DP || gameFamily == GameFamilies.Plat)
            {
                var vm = new WildEditorDPPtViewModel(path, names, initialIndex, headerCount);
                var window = new EditorHostWindow("Wild Pokémon Editor (DPPt)", new WildEditorDPPtView(vm), 900, 680);
                window.Closed += (_, _) => vm.Detach();
                window.ShowManaged();
            }
            else
            {
                var vm = new WildEditorHGSSViewModel(path, names, initialIndex, headerCount);
                var window = new EditorHostWindow("Wild Pokémon Editor (HGSS)", new WildEditorHGSSView(vm), 900, 680);
                window.Closed += (_, _) => vm.Detach();
                window.ShowManaged();
            }
        }

        public static void OpenHeaderEditor(int initialIndex = -1)
        {
            if (!IsRomLoaded) return;
            new EditorHostWindow("Header Editor",
                new HeaderEditorView(new HeaderEditorViewModel(true) { InitialHeaderId = initialIndex })).ShowManaged();
        }

        public static void OpenCameraEditor()
        {
            if (!IsRomLoaded) return;
            new EditorHostWindow("Camera Editor", new CameraEditorView(new CameraEditorViewModel(true))).ShowManaged();
        }

        public static void OpenTrainerEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded || BlockedForHge("The Trainer Editor", HgEngineDomain.Trainers)) return;
            DSUtils.TryUnpackNarcs(new List<DirNames> {
                DirNames.trainerProperties, DirNames.trainerParty, DirNames.trainerGraphics });
            new TrainerEditorView(new TrainerEditorViewModel(true) { InitialIndex = initialIndex }).ShowManaged();
        }

        public static void OpenTrainerSpriteEditor(int initialClassIndex = 0)
        {
            if (!IsRomLoaded || BlockedForHge("The Trainer Sprite Editor")) return;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerGraphics });
            new TrainerSpriteEditorView(new TrainerSpriteEditorViewModel(initialClassIndex)).ShowManaged();
        }

        public static void OpenTrainerFlagBulkEditor() => _ = OpenTrainerFlagBulkEditorAsync();

        // Reads one file per trainer, so it takes a visible moment on a full ROM. Built off the UI
        // thread behind the busy overlay, since neither this VM nor VsSeeker's touches any UI type.
        public static async System.Threading.Tasks.Task OpenTrainerFlagBulkEditorAsync()
        {
            if (!IsRomLoaded || BlockedForHge("The Trainer Flag Bulk Editor", HgEngineDomain.Trainers)) return;

            TrainerFlagBulkEditorViewModel vm = null;
            await RunBusyAsync("Opening Trainer Flag Bulk Editor…",
                "Reading every trainer's AI flags.",
                () =>
                {
                    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties });
                    vm = new TrainerFlagBulkEditorViewModel();
                });
            if (vm == null) return;

            new EditorHostWindow("Trainer Flag Bulk Editor",
                new TrainerFlagBulkEditorView(vm), 1050, 700).ShowManaged();
        }

        public static void OpenVsSeekerRematchEditor(int initialRowIndex = -1) => _ = OpenVsSeekerRematchEditorAsync(initialRowIndex);

        public static async System.Threading.Tasks.Task OpenVsSeekerRematchEditorAsync(int initialRowIndex = -1)
        {
            if (!IsRomLoaded) return;
            if (!VsSeekerRematchTable.IsSupported)
            {
                AppMessages.Info("The Vs. Seeker Rematch Editor only supports Diamond, Pearl and Platinum (English).",
                    "Not Supported");
                return;
            }

            VsSeekerRematchViewModel vm = null;
            await RunBusyAsync("Opening Vs. Seeker Rematch Editor…",
                "Reading the rematch table and trainer names.",
                () => vm = new VsSeekerRematchViewModel(initialRowIndex));
            if (vm == null) return;

            new EditorHostWindow("Vs. Seeker Rematch Editor",
                new VsSeekerRematchView(vm), 900, 600).ShowManaged();
        }

        public static void OpenStarterEditor()
        {
            if (!IsRomLoaded || BlockedForHge("The Starter Pokémon Editor") || !RomInfo.IsStarterEditorAvailable()) return;
            // scripts: rival/tag-battle team patches + DP/Pt held item; personalPokeData: primary-type lookup for text patch.
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.scripts, DirNames.personalPokeData });
            new StarterEditorView().ShowManaged();
        }

        // ── World / data editors ───────────────────────────────────────────────
        public static void OpenFlyWarpEditor()
        {
            if (!IsRomLoaded) return;
            new FlyEditorView(HeaderLists.GetHeaderListBoxNames()).ShowManaged();
        }

        public static void OpenDungeonCutinEditor()
        {
            if (!IsRomLoaded || !RomInfo.IsDungeonCutinEditorAvailable()) return;
            new DungeonCutinEditorView(HeaderLists.GetHeaderListBoxNames()).ShowManaged();
        }

        public static void OpenTitleScreenEditor()
        {
            if (!IsRomLoaded || !RomInfo.IsTitleScreenEditorAvailable()) return;
            new TitleScreenEditorView().ShowManaged();
        }

        public static void OpenTrainerCardEditor()
        {
            if (!IsRomLoaded || !RomInfo.IsTrainerCardEditorAvailable()) return;
            new TrainerCardEditorView().ShowManaged();
        }

        public static void OpenSpawnEditor()
        {
            if (!IsRomLoaded) return;
            new SpawnEditorView(new SpawnEditorViewModel(HeaderLists.GetHeaderListBoxNames())).ShowManaged();
        }

        public static void OpenHeaderSearch()
        {
            if (!IsRomLoaded) return;
            new HeaderSearchView(new HeaderSearchViewModel(true)).ShowManaged();
        }

        public static void OpenMapEditor()
        {
            if (!IsRomLoaded) return;
            var vm = new MapEditorViewModel(true);
            var window = new EditorHostWindow("Map Editor", new MapEditorView(vm), 1200, 720);
            window.Closed += (_, _) => vm.Detach();
            window.ShowManaged();
        }

        public static void OpenBuildingEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            new BuildingEditorView(new BuildingEditorViewModel(true) { InitialIndex = initialIndex }).ShowManaged();
        }

        public static void OpenMatrixEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            new EditorHostWindow("Matrix Editor",
                new MatrixEditorView(new MatrixEditorViewModel(true) { InitialIndex = initialIndex }),
                860, 640).ShowManaged();
        }

        public static void OpenEventEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            new EditorHostWindow("Event Editor",
                new EventEditorView(new EventEditorViewModel(true) { InitialIndex = initialIndex }),
                1200, 720).ShowManaged();
        }

        public static void OpenEventEditorWithOverworld(int eventFileId, int owIndex)
        {
            if (!IsRomLoaded) return;
            new EditorHostWindow("Event Editor",
                new EventEditorView(new EventEditorViewModel(true) { InitialIndex = eventFileId, InitialOverworldIndex = owIndex }),
                1200, 720).ShowManaged();
        }

        public static void OpenNsbtxEditor()
        {
            if (!IsRomLoaded) return;
            new NsbtxEditorView(new NsbtxEditorViewModel(true)).ShowManaged();
        }

        public static void OpenAreaDataEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            new EditorHostWindow("Area Data Editor",
                new AreaDataEditorView(new AreaDataEditorViewModel(true) { InitialIndex = initialIndex }),
                520, 380).ShowManaged();
        }

        public static void OpenOverlayEditor()
        {
            if (!IsRomLoaded) return;
            new OverlayEditorView().ShowManaged();
        }

        public static void OpenOverworldEditor()
        {
            if (!IsRomLoaded) return;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.OWSprites });
            SetOWtable();
            Set3DOverworldsDict();
            ReadOWTable();
            new BtxEditorView(new BtxEditorViewModel(true)).ShowManaged();
        }

        // ── Tools ──────────────────────────────────────────────────────────────
        public static void OpenAddressHelper()
        {
            if (!IsRomLoaded) return;
            new AddressHelperView().ShowManaged();
        }

        public static void OpenResearchHelper()
        {
            if (!IsRomLoaded) return;
            new ResearchHelperView(new ResearchHelperViewModel(true)).ShowManaged();
        }

        public static void OpenCharMapManager()
        {
            if (!IsRomLoaded) return;
            new CharMapManagerView().ShowManaged();
        }

        public static void OpenSettings()
        {
            // Settings do not require a loaded ROM.
            new SettingsWindowView().ShowManaged();
        }

        public static void OpenHgEngineLink()
        {
            if (!IsRomLoaded) return;
            new HgEngineLinkView().ShowManaged();
        }

        public static void OpenLabelEditor()
        {
            // Needs a ROM for project-scoped overrides (workDir); global scope works regardless.
            if (!IsRomLoaded) return;
            new LabelEditorView().ShowManaged();
        }

        public static void OpenProjectChecks()
        {
            if (!IsRomLoaded) return;
            new ProjectChecksView().ShowManaged();
        }

        public static void OpenPatchToolbox()
        {
            // Writes to the ROM binary (ARM9 / overlays / NARCs). Native Avalonia UI over the shared
            // PatchToolboxDialog apply-logic, so it runs identical code to the WinForms dialog.
            if (!IsRomLoaded) return;
            new PatchToolboxView().ShowManaged();
        }

        public static void OpenCustomCommandManager()
        {
            // Manages the custom script-command databases. Still a WinForms tool (self-contained, file-based);
            // reused directly over the shared Win32 pump until a native port exists.
            if (!IsRomLoaded) return;
            new CustomScrcmdManagerView(new CustomScrcmdManagerViewModel(true)).ShowManaged();
        }

        public static void OpenGlTest()
        {
            // No ROM required; verifies the Avalonia OpenGL pipeline (3D rebuild slice 1).
            new GlTestView().ShowManaged();
        }

        // ── Command palette (quick-open) ────────────────────────────────────────
        /// <summary>Opens the Ctrl+P quick-open palette over the given window.</summary>
        public static void OpenCommandPalette(global::Avalonia.Controls.Window owner)
        {
            var vm = new CommandPaletteViewModel(BuildCommands(), DynamicCommands);
            var view = new CommandPaletteView(vm);
            if (owner != null) view.ShowDialog(owner); else view.ShowManaged();
        }

        /// <summary>
        /// Query-specific palette entries: once the user types a number, offer to open the indexable editors
        /// straight at that file (e.g. "event 42" → "Go to Event file #42"). The text either side of the
        /// number filters which jumps appear, so "42" alone lists them all and "script 42" narrows to one.
        /// </summary>
        private static IEnumerable<CommandItem> DynamicCommands(string query)
        {
            if (!IsRomLoaded || string.IsNullOrWhiteSpace(query)) yield break;
            var m = System.Text.RegularExpressions.Regex.Match(query, @"\d+");
            if (!m.Success) yield break;
            int n = int.Parse(m.Value);

            // What the user typed besides the number (minus "go to"), used to filter the jump list.
            string rest = query.Remove(m.Index, m.Length)
                               .Replace("go to", "", System.StringComparison.OrdinalIgnoreCase)
                               .Replace("goto", "", System.StringComparison.OrdinalIgnoreCase)
                               .Trim();

            (string label, string keywords, System.Action run)[] jumps =
            {
                ($"Go to Pokémon #{n}",        "pokemon species mon personal", () => { _ = OpenPokemonEditorAsync(n); }),
                ($"Go to Move #{n}",           "move attack",                  () => OpenMoveDataEditor(n)),
                ($"Go to TM / HM #{n}",        "tm hm machine",                () => OpenTMEditor(n)),
                ($"Go to Item #{n}",           "item",                         () => OpenItemEditor(n)),
                ($"Go to Trainer #{n}",        "trainer battle party",         () => OpenTrainerEditor(n)),
                ($"Go to Trade #{n}",          "trade in-game",                () => OpenTradeEditor(n)),
                ($"Go to Header #{n}",         "header map",                   () => OpenHeaderEditor(n)),
                ($"Go to Building #{n}",       "building model",               () => OpenBuildingEditor(n)),
                ($"Go to Headbutt file #{n}",  "headbutt tree",                () => OpenHeadbuttEncounterEditor(n)),
                ($"Go to Event file #{n}",     "event warp trigger overworld", () => OpenEventEditor(n)),
                ($"Go to Script #{n}",         "script",                       () => OpenScriptEditor(n)),
                ($"Go to Level Script #{n}",   "level script",                 () => OpenLevelScriptEditor(n)),
                ($"Go to Text archive #{n}",   "text string message archive",  () => OpenTextEditor(n)),
                ($"Go to Matrix #{n}",         "matrix world grid",            () => OpenMatrixEditor(n)),
                ($"Go to Area Data #{n}",      "area data tileset",            () => OpenAreaDataEditor(n)),
                ($"Go to Wild encounters #{n}","wild encounter grass surf",    () => OpenWildEditor(n)),
            };

            foreach (var (label, keywords, run) in jumps)
                if (rest.Length == 0
                    || label.Contains(rest, System.StringComparison.OrdinalIgnoreCase)
                    || keywords.Contains(rest, System.StringComparison.OrdinalIgnoreCase))
                    yield return new CommandItem { Name = label, Run = run };
        }

        /// <summary>Opens the one place that lists every 2D graphic in the game.</summary>
        public static void OpenGraphicsBrowser()
        {
            try
            {
                new Views.Graphics.GraphicsBrowserView(new ViewModels.Graphics.GraphicsBrowserViewModel()).ShowManaged();
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenGraphicsBrowser failed: " + ex.Message);
                _ = DialogHelper.ShowInfo("The graphics list could not be opened. Open a ROM first.", "Graphics");
            }
        }

        /// <summary>Opens the graphics window already looking at one file.</summary>
        public static void OpenGraphicAt(RomInfo.DirNames archive, int fileIndex)
        {
            try
            {
                var a = Data.GraphicAssets.All.FirstOrDefault(x => x.Dir == archive);
                if (a == null)
                {
                    _ = DialogHelper.ShowInfo("That kind of graphic is not one this window lists yet.", "Graphics");
                    return;
                }

                var vm = new ViewModels.Graphics.GraphicsBrowserViewModel();
                bool found = vm.JumpTo(a, fileIndex);
                new Views.Graphics.GraphicsBrowserView(vm).ShowManaged();
                if (!found)
                    vm.Status = "That graphic could not be found in this game, so the whole list is shown instead.";
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenGraphicAt failed: " + ex.Message);
                _ = DialogHelper.ShowInfo("The graphics list could not be opened. Open a ROM first.", "Graphics");
            }
        }

        /// <summary>The editor that owns a graphic, and what to call it.</summary>
        public static (string Name, System.Action Open)? EditorForGraphic(RomInfo.DirNames archive, int fileIndex)
        {
            switch (archive)
            {
                case RomInfo.DirNames.pokemonBattleSprites:
                    return ("Pokemon Editor", () => { _ = OpenPokemonEditorAsync(fileIndex / 6); });

                case RomInfo.DirNames.monIcons:
                    // Seven files come before the run of one per Pokemon.
                    int species = fileIndex - 7;
                    if (species < 0) return null;
                    return ("Pokemon Editor", () => { _ = OpenPokemonEditorAsync(species); });

                case RomInfo.DirNames.trainerGraphics:
                    return ("Trainer Sprite Editor", () => OpenTrainerSpriteEditor(fileIndex / 5));

                case RomInfo.DirNames.itemIcons:
                {
                    int item = ItemUsingDrawing(fileIndex);
                    if (item < 0) return null;
                    return ("Item Editor", () => OpenItemEditor(item));
                }

                case RomInfo.DirNames.battleBg:
                    return ("Battle scenes", OpenBattleSceneBrowser);

                case RomInfo.DirNames.dungeonCutinGraphics:
                    return ("Dungeon Cutin Editor", OpenDungeonCutinEditor);

                case RomInfo.DirNames.trainerCardGraphics:
                    return ("Trainer Card Editor", OpenTrainerCardEditor);

                default:
                    return null;
            }
        }

        /// <summary>The first item that uses a drawing, since several can share one.</summary>
        private static int ItemUsingDrawing(int fileIndex)
        {
            try
            {
                var names = RomInfo.GetItemNames();
                for (int item = 0; item < names.Length; item++)
                    if (Data.GraphicAssets.DrawingForItem(item) == fileIndex) return item;
            }
            catch { }
            return -1;
        }

        /// <summary>
        /// Opens the battle scenes list: the scenery every place in the game fights on, with the header
        /// number that chooses it.
        /// </summary>
        /// <summary>Opens the window that turns a picture into a background.</summary>
        public static void OpenTilesetBuilder()
        {
            try
            {
                new Views.Graphics.TilesetBuilderView().ShowManaged();
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenTilesetBuilder failed: " + ex.Message);
                _ = DialogHelper.ShowInfo("That window could not be opened.", "Picture to Background");
            }
        }

        public static void OpenFontEditor()
        {
            try
            {
                new Views.Graphics.FontEditorView().ShowManaged();
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenFontEditor failed: " + ex.Message);
                _ = DialogHelper.ShowInfo("The fonts could not be opened. Open a ROM first.",
                                          "Font Editor");
            }
        }

        public static void OpenBattleScreenEditor()
        {
            // Diamond and Pearl lay the battle screen out differently enough that this editor cannot
            // read it, and it was failing to open at all rather than saying so.
            if (DSPRE.RomInfo.gameFamily == DSPRE.RomInfo.GameFamilies.DP)
            {
                _ = DialogHelper.ShowInfo(
                    "The battle screen is only read on Platinum, HeartGold and SoulSilver so far. "
                  + "Diamond and Pearl keep its pieces elsewhere.", "Battle screen");
                return;
            }

            try
            {
                new Views.Battle.BattleScreenEditorView().ShowManaged();
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenBattleScreenEditor failed: " + ex.Message);
                _ = DialogHelper.ShowInfo("The battle screen could not be opened. Open a ROM first.",
                                          "Battle screen");
            }
        }

        public static void OpenBattleSceneBrowser()
        {
            try
            {
                new Views.Battle.BattleSceneBrowserView(new ViewModels.Battle.BattleSceneBrowserViewModel()).ShowManaged();
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenBattleSceneBrowser failed: " + ex.Message);
                _ = DialogHelper.ShowInfo("The battle scenes could not be opened. Open a ROM first.",
                                          "Battle scenes");
            }
        }

        /// <summary>Opens the models and textures list, which is its own place, not part of the flat one.</summary>
        public static void OpenModelBrowser()
        {
            try
            {
                new Views.Graphics.ModelBrowserView(new ViewModels.Graphics.ModelBrowserViewModel()).ShowManaged();
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenModelBrowser failed: " + ex.Message);
                _ = DialogHelper.ShowInfo("The models list could not be opened. Open a ROM first.", "Models");
            }
        }

        /// <summary>The editor list shown in the command palette (mirrors the main menu).</summary>
        public static List<CommandItem> BuildCommands() => new()
        {
            new() { Name = "Graphics",              Keywords = "sprite picture image texture palette colour color icon font paint draw", Run = OpenGraphicsBrowser },
            new() { Name = "Models and textures",   Keywords = "3d model nsbmd nsbtx building overworld map mesh", Run = OpenModelBrowser },
            new() { Name = "Battle screens",        Keywords = "battle screen gauge hp bar backdrop platform message box touch command", Run = OpenBattleScreenEditor },
            new() { Name = "Battle scenes",         Keywords = "battle scene backdrop terrain platform ground", Run = OpenBattleSceneBrowser },
            new() { Name = "Audio Editor",          Keywords = "sound cry cries music bgm fanfare sfx song", Run = () => { _ = OpenAudioEditorAsync(); } },
            new() { Name = "Pokémon Editor",        Keywords = "species personal learnset evolution sprite", Run = () => { _ = OpenPokemonEditorAsync(); } },
            new() { Name = "Form Editor (hg-engine)", Keywords = "mega regional alolan galarian gmax gigantamax primal reversion form", Run = OpenHgEngineFormEditor },
            new() { Name = "Move Data Editor",      Keywords = "attack",   Run = () => OpenMoveDataEditor() },
            new() { Name = "TM / HM Editor",        Keywords = "machine",  Run = () => OpenTMEditor() },
            new() { Name = "TM/HM Bulk Editor",     Keywords = "machine compatibility bulk family sync copy", Run = OpenTmHmBulkEditor },
            new() { Name = "Egg Move Editor",       Keywords = "breeding", Run = OpenEggMoveEditor },
            new() { Name = "Battle Script Editor",  Keywords = "move sequence waza be_seq sub_seq effect animation west", Run = () => OpenBattleScriptEditor() },
            new() { Name = "Item Editor",           Run = () => OpenItemEditor() },
            new() { Name = "Item Tables (Pickup, Hidden, Rock Smash)", Keywords = "pickup hidden ground rock smash item table hgss", Run = OpenItemTableEditor },
            new() { Name = "Trade Editor",          Keywords = "in-game",  Run = () => OpenTradeEditor() },
            new() { Name = "Starter Pokémon Editor", Keywords = "turtwig chimchar piplup chikorita cyndaquil totodile rival professor", Run = OpenStarterEditor },
            new() { Name = "Trainer Editor",        Keywords = "battle party", Run = () => OpenTrainerEditor() },
            new() { Name = "Trainer Sprite Editor", Keywords = "class pixel paint", Run = () => OpenTrainerSpriteEditor() },
            new() { Name = "Vs. Seeker Rematch Editor", Keywords = "rematch trainer encounter chain", Run = () => OpenVsSeekerRematchEditor() },
            new() { Name = "Trainer Flag Bulk Editor", Keywords = "ai double battle bulk", Run = OpenTrainerFlagBulkEditor },
            new() { Name = "Text Editor",           Keywords = "string archive message", Run = () => OpenTextEditor() },
            new() { Name = "Script Editor",         Run = () => OpenScriptEditor() },
            new() { Name = "Level Script Editor",   Run = () => OpenLevelScriptEditor() },
            new() { Name = "Music & Battle Tables", Keywords = "table conditional music battle effects combo vs poster", Run = OpenTableEditor },
            new() { Name = "Header Editor",         Keywords = "map header", Run = () => OpenHeaderEditor() },
            new() { Name = "Camera Editor",         Keywords = "angle map header", Run = OpenCameraEditor },
            new() { Name = "Map Editor",            Keywords = "3d model buildings", Run = OpenMapEditor },
            new() { Name = "Building Editor",       Run = () => OpenBuildingEditor() },
            new() { Name = "Matrix Editor",         Keywords = "world grid", Run = () => OpenMatrixEditor() },
            new() { Name = "Event Editor",          Keywords = "overworld warp trigger spawn", Run = () => OpenEventEditor() },
            new() { Name = "Fly / Warp Editor",     Run = OpenFlyWarpEditor },
            new() { Name = "Spawn Point Editor",    Keywords = "start position new game", Run = OpenSpawnEditor },
            new() { Name = "Advanced Header Search", Keywords = "find filter query field", Run = OpenHeaderSearch },
            new() { Name = "Overlay Editor",        Run = OpenOverlayEditor },
            new() { Name = "Overworld Sprites (BTX)", Run = OpenOverworldEditor },
            new() { Name = "NSBTX Texture Editor",  Keywords = "texture", Run = OpenNsbtxEditor },
            new() { Name = "Area Data Editor",      Keywords = "tileset", Run = () => OpenAreaDataEditor() },
            new() { Name = "Wild Pokémon Editor",   Keywords = "encounter grass surf", Run = () => OpenWildEditor() },
            new() { Name = "Special Encounters",    Keywords = "bug contest marsh honey safari", Run = OpenEncountersEditor },
            new() { Name = "Headbutt Editor",       Keywords = "tree hgss", Run = () => OpenHeadbuttEncounterEditor() },
            new() { Name = "Trophy Garden Editor",  Keywords = "daily pokemon backlot dp plat", Run = OpenTrophyGardenEditor },
            new() { Name = "Battle Tower Editor",   Keywords = "tower trainer set party rental", Run = OpenBattleTowerEditor },
            new() { Name = "Address Helper",        Run = OpenAddressHelper },
            new() { Name = "Research Helper",       Run = OpenResearchHelper },
            new() { Name = "Char Map Manager",      Keywords = "text encoding", Run = OpenCharMapManager },
            new() { Name = "Font Editor",           Keywords = "font letter glyph character typeface text", Run = OpenFontEditor },
            new() { Name = "Edit Dropdown Labels",  Keywords = "enum custom", Run = OpenLabelEditor },
            new() { Name = "Validation & Where-Used", Keywords = "check broken references project health", Run = OpenProjectChecks },
            new() { Name = "Settings",              Run = OpenSettings },
            // ── Actions (not editors) ──
            new() { Name = "Toggle theme (Dark / Light)", Keywords = "dark light appearance", Run = ThemeManager.Toggle },
        };
    }
}
