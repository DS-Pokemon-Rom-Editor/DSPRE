using System.Collections.Generic;
using DSPRE.Avalonia.ViewModels;
using DSPRE.Avalonia.Views;
using DSPRE.Resources;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Centralised launch points for the already-migrated Avalonia editor windows.
    ///
    /// These mirror the per-editor launch logic that currently lives inline in the
    /// WinForms <c>Main Window.cs</c> handlers. Keeping them here lets the new
    /// Avalonia <see cref="Views.MainWindowView"/> open the same editors without
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

        // ── Pokémon-related editors ────────────────────────────────────────────
        public static void OpenPokemonEditor(int initialMon = 1)
        {
            if (!IsRomLoaded) return;

            DSUtils.TryUnpackNarcs(new List<DirNames> {
                DirNames.personalPokeData, DirNames.learnsets,
                DirNames.evolutions, DirNames.monIcons });
            SetMonIconsPalTableAddress();

            // Build full Pokémon name list (base + alt forms + extras)
            string[] pokeNames = GetPokemonNames();
            var fullList = new List<string>(pokeNames);
            foreach (var extra in PokeDatabase.PersonalData.personalExtraFiles)
                fullList.Add(fullList[extra.monId] + " - " + extra.description);
            int count = GetPersonalFilesCount();
            for (int i = fullList.Count; i < count; i++) fullList.Add($"Extra entry {i}");

            string[] moveNames = GetAttackNames();

            int mon = System.Math.Clamp(initialMon, 0, System.Math.Max(0, fullList.Count - 1));
            var vm = new PokemonEditorViewModel(fullList.ToArray(), moveNames, initialMon: mon);
            new PokemonEditorView(vm).Show();
        }

        public static void OpenMoveDataEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.moveData });
            var view = new MoveDataEditorView();
            if (initialIndex > 0 && view.DataContext is MoveDataEditorViewModel vm)
                vm.SelectedMoveIndex = initialIndex;   // setter clamps + loads
            view.Show();
        }

        public static void OpenTMEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            var view = new TMEditorView();
            if (initialIndex > 0 && view.DataContext is TMEditorViewModel vm)
                vm.SelectedMachineIndex = initialIndex;   // setter loads the machine
            view.Show();
        }

        public static void OpenEggMoveEditor()
        {
            if (!IsRomLoaded) return;
            new EggMoveEditorView().Show();
        }

        public static void OpenItemEditor(int initialIndex = 1)
        {
            if (!IsRomLoaded) return;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.itemData });
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.itemIcons });
            var vm = new ItemEditorViewModel(GetItemNames());
            if (initialIndex > 0) vm.SelectedItemIndex = System.Math.Clamp(initialIndex, 0, vm.MaxItemIndex);
            new ItemEditorView(vm).Show();
        }

        public static void OpenItemTableEditor()
        {
            if (!IsRomLoaded || !IsItemTableEditorAvailable()) return;
            new ItemTableEditorView(new ItemTableEditorViewModel(GetItemNames())).Show();
        }

        public static void OpenTradeEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.tradeData });
            var view = new TradeEditorView();
            if (initialIndex > 0 && view.DataContext is TradeEditorViewModel vm)
                _ = vm.ChangeTradeIDAsync(initialIndex);   // async load; freshly opened editor isn't dirty
            view.Show();
        }

        public static void OpenTextEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.textArchives });
            new TextEditorView(new TextEditorViewModel(true) { InitialIndex = initialIndex }).Show();
        }

        public static void OpenScriptEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            new ScriptEditorView(new ScriptEditorViewModel(true) { InitialIndex = initialIndex }).Show();
        }

        public static void OpenLevelScriptEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            new LevelScriptEditorView(new LevelScriptEditorViewModel(true) { InitialIndex = initialIndex }).Show();
        }

        public static void OpenTableEditor()
        {
            if (!IsRomLoaded || EditorPanels.headerEditor == null) return;
            new TableEditorView(new TableEditorViewModel(EditorPanels.headerEditor.headerListBoxNames)).Show();
        }

        public static void OpenHiddenItemsEditor()
        {
            if (!IsRomLoaded) return;
            new HiddenItemsEditorView(new HiddenItemsEditorViewModel(true)).Show();
        }

        public static void OpenPickupTableEditor()
        {
            if (!IsRomLoaded) return;
            new PickupTableEditorView(new PickupTableEditorViewModel(true)).Show();
        }

        public static void OpenEncountersEditor()
        {
            if (!IsRomLoaded) return;
            new EncountersEditorView(new EncountersEditorViewModel(true)).Show();
        }

        public static void OpenHeadbuttEncounterEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            new HeadbuttEncounterView(new HeadbuttEncounterViewModel(true) { InitialIndex = initialIndex }).Show();
        }

        public static void OpenWildEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.encounters, DirNames.monIcons });
            string path = gameDirs[DirNames.encounters].unpackedDir;
            string[] names = GetPokemonNames();
            int headerCount = GetHeaderCount();
            if (gameFamily == GameFamilies.DP || gameFamily == GameFamilies.Plat)
                new WildEditorDPPtView(new WildEditorDPPtViewModel(path, names, initialIndex, headerCount)).Show();
            else
                new WildEditorHGSSView(new WildEditorHGSSViewModel(path, names, initialIndex, headerCount)).Show();
        }

        public static void OpenHeaderEditor(int initialIndex = -1)
        {
            if (!IsRomLoaded) return;
            new EditorHostWindow("Header Editor",
                new HeaderEditorView(new HeaderEditorViewModel(true) { InitialHeaderId = initialIndex })).Show();
        }

        public static void OpenTrainerEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            DSUtils.TryUnpackNarcs(new List<DirNames> {
                DirNames.trainerProperties, DirNames.trainerParty, DirNames.trainerGraphics });
            new TrainerEditorView(new TrainerEditorViewModel(true) { InitialIndex = initialIndex }).Show();
        }

        // ── World / data editors ───────────────────────────────────────────────
        public static void OpenFlyWarpEditor()
        {
            if (!IsRomLoaded || EditorPanels.headerEditor == null) return;
            new FlyEditorView(EditorPanels.headerEditor.headerListBoxNames).Show();
        }

        public static void OpenMapEditor()
        {
            if (!IsRomLoaded) return;
            new MapEditorView(new MapEditorViewModel(true)).Show();
        }

        public static void OpenBuildingEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            new BuildingEditorView(new BuildingEditorViewModel(true) { InitialIndex = initialIndex }).Show();
        }

        public static void OpenMatrixEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            new MatrixEditorView(new MatrixEditorViewModel(true) { InitialIndex = initialIndex }).Show();
        }

        public static void OpenEventEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            new EventEditorView(new EventEditorViewModel(true) { InitialIndex = initialIndex }).Show();
        }

        public static void OpenNsbtxEditor()
        {
            if (!IsRomLoaded) return;
            new NsbtxEditorView(new NsbtxEditorViewModel(true)).Show();
        }

        public static void OpenAreaDataEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            new AreaDataEditorView(new AreaDataEditorViewModel(true) { InitialIndex = initialIndex }).Show();
        }

        public static void OpenOverlayEditor()
        {
            if (!IsRomLoaded) return;
            new OverlayEditorView().Show();
        }

        public static void OpenOverworldEditor()
        {
            if (!IsRomLoaded) return;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.OWSprites });
            SetOWtable();
            Set3DOverworldsDict();
            ReadOWTable();
            new BtxEditorView(new BtxEditorViewModel(true)).Show();
        }

        // ── Tools ──────────────────────────────────────────────────────────────
        public static void OpenAddressHelper()
        {
            if (!IsRomLoaded) return;
            new AddressHelperView().Show();
        }

        public static void OpenResearchHelper()
        {
            if (!IsRomLoaded) return;
            new ResearchHelperView(new ResearchHelperViewModel(true)).Show();
        }

        public static void OpenCharMapManager()
        {
            if (!IsRomLoaded) return;
            new CharMapManagerView().Show();
        }

        public static void OpenSettings()
        {
            // Settings do not require a loaded ROM.
            new SettingsWindowView().Show();
        }

        public static void OpenLabelEditor()
        {
            // Needs a ROM for project-scoped overrides (workDir); global scope works regardless.
            if (!IsRomLoaded) return;
            new LabelEditorView().Show();
        }

        public static void OpenProjectChecks()
        {
            if (!IsRomLoaded) return;
            new ProjectChecksView().Show();
        }

        public static void OpenGlTest()
        {
            // No ROM required — verifies the Avalonia OpenGL pipeline (3D rebuild slice 1).
            new GlTestView().Show();
        }

        // ── Command palette (quick-open) ────────────────────────────────────────
        /// <summary>Opens the Ctrl+P quick-open palette over the given window.</summary>
        public static void OpenCommandPalette(global::Avalonia.Controls.Window owner)
        {
            var vm = new CommandPaletteViewModel(BuildCommands(), DynamicCommands);
            var view = new CommandPaletteView(vm);
            if (owner != null) view.ShowDialog(owner); else view.Show();
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

            // What the user typed besides the number (minus "go to") — used to filter the jump list.
            string rest = query.Remove(m.Index, m.Length)
                               .Replace("go to", "", System.StringComparison.OrdinalIgnoreCase)
                               .Replace("goto", "", System.StringComparison.OrdinalIgnoreCase)
                               .Trim();

            (string label, string keywords, System.Action run)[] jumps =
            {
                ($"Go to Pokémon #{n}",        "pokemon species mon personal", () => OpenPokemonEditor(n)),
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

        /// <summary>The editor list shown in the command palette (mirrors the main menu).</summary>
        public static List<CommandItem> BuildCommands() => new()
        {
            new() { Name = "Pokémon Editor",        Keywords = "species personal learnset evolution sprite", Run = () => OpenPokemonEditor() },
            new() { Name = "Move Data Editor",      Keywords = "attack",   Run = () => OpenMoveDataEditor() },
            new() { Name = "TM / HM Editor",        Keywords = "machine",  Run = () => OpenTMEditor() },
            new() { Name = "Egg Move Editor",       Keywords = "breeding", Run = OpenEggMoveEditor },
            new() { Name = "Item Editor",           Run = () => OpenItemEditor() },
            new() { Name = "Item Table Editor",     Keywords = "pickup mart", Run = OpenItemTableEditor },
            new() { Name = "Trade Editor",          Keywords = "in-game",  Run = () => OpenTradeEditor() },
            new() { Name = "Trainer Editor",        Keywords = "battle party", Run = () => OpenTrainerEditor() },
            new() { Name = "Text Editor",           Keywords = "string archive message", Run = () => OpenTextEditor() },
            new() { Name = "Script Editor",         Run = () => OpenScriptEditor() },
            new() { Name = "Level Script Editor",   Run = () => OpenLevelScriptEditor() },
            new() { Name = "Table Editor",          Run = OpenTableEditor },
            new() { Name = "Hidden Items Editor",   Keywords = "hgss",     Run = OpenHiddenItemsEditor },
            new() { Name = "Pickup Table Editor",   Run = OpenPickupTableEditor },
            new() { Name = "Header Editor",         Keywords = "map header", Run = () => OpenHeaderEditor() },
            new() { Name = "Map Editor",            Keywords = "3d model buildings", Run = OpenMapEditor },
            new() { Name = "Building Editor",       Run = () => OpenBuildingEditor() },
            new() { Name = "Matrix Editor",         Keywords = "world grid", Run = () => OpenMatrixEditor() },
            new() { Name = "Event Editor",          Keywords = "overworld warp trigger spawn", Run = () => OpenEventEditor() },
            new() { Name = "Fly / Warp Editor",     Run = OpenFlyWarpEditor },
            new() { Name = "Overlay Editor",        Run = OpenOverlayEditor },
            new() { Name = "Overworld Sprites (BTX)", Run = OpenOverworldEditor },
            new() { Name = "NSBTX Texture Editor",  Keywords = "texture", Run = OpenNsbtxEditor },
            new() { Name = "Area Data Editor",      Keywords = "tileset", Run = () => OpenAreaDataEditor() },
            new() { Name = "Wild Pokémon Editor",   Keywords = "encounter grass surf", Run = () => OpenWildEditor() },
            new() { Name = "Special Encounters",    Keywords = "bug contest marsh honey safari", Run = OpenEncountersEditor },
            new() { Name = "Headbutt Editor",       Keywords = "tree hgss", Run = () => OpenHeadbuttEncounterEditor() },
            new() { Name = "Address Helper",        Run = OpenAddressHelper },
            new() { Name = "Research Helper",       Run = OpenResearchHelper },
            new() { Name = "Char Map Manager",      Keywords = "text encoding", Run = OpenCharMapManager },
            new() { Name = "Edit Dropdown Labels",  Keywords = "enum custom", Run = OpenLabelEditor },
            new() { Name = "Validation & Where-Used", Keywords = "check broken references project health", Run = OpenProjectChecks },
            new() { Name = "Settings",              Run = OpenSettings },
            // ── Actions (not editors) ──
            new() { Name = "Toggle theme (Dark / Light)", Keywords = "dark light appearance", Run = ThemeManager.Toggle },
        };
    }
}
