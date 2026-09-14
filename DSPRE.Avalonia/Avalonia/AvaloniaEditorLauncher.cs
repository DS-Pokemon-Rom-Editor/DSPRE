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

        /// <summary>
        /// Refuses an editor whose whole archive hg-engine rebuilds from source. Silent for a project
        /// with no checkout linked, and for archives it only overwrites part of.
        /// </summary>
        internal static bool BlockedForHgeArchive(string editorName, params DirNames[] dirs)
        {
            foreach (DirNames dir in dirs)
            {
                string why = HgEngineOwnedFiles.RefusalFor(dir);
                if (why == null) continue;

                AppMessages.Info($"{editorName} is not available for this project. {why}", "Built by hg-engine");
                return true;
            }
            return false;
        }

        /// <summary>What the busy overlay says under the title while an archive is being unpacked.</summary>
        private const string UnpackHint =
            "First-time opens unpack the ROM's data and can take a while, especially for a WSL-hosted project.";

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
                // The species list is counted from the personal data, which a fresh project has not unpacked yet.
                await RunBusyAsync("Opening Audio Editor…", UnpackHint,
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.personalPokeData }));

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

        public static void OpenMoveDataEditor(int initialIndex = 0) => _ = OpenMoveDataEditorAsync(initialIndex);

        public static async System.Threading.Tasks.Task OpenMoveDataEditorAsync(int initialIndex = 0)
        {
            if (!IsRomLoaded || BlockedForHge("The Move Data Editor", HgEngineDomain.Moves)) return;

            try
            {
                await RunBusyAsync("Opening Move Data Editor…", UnpackHint,
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.moveData }));
                var view = new MoveDataEditorView();
                if (initialIndex > 0 && view.DataContext is MoveDataEditorViewModel vm)
                    vm.SelectedMoveIndex = initialIndex;   // setter clamps + loads
                view.ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("Couldn't open the Move Data Editor: " + ex.Message, "Move Data Editor");
            }
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
            // waza/be/sub and the WEST animations are edited from the checkout's own sources instead of
            // being refused; only the particle archive has no source view yet.
            if (BlockedForHgeArchive("The Battle Script Editor", DirNames.wazaParticle)) return;
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

        public static void OpenItemEditor(int initialIndex = 1) => _ = OpenItemEditorAsync(initialIndex);

        public static async System.Threading.Tasks.Task OpenItemEditorAsync(int initialIndex = 1)
        {
            if (!IsRomLoaded || BlockedForHge("The Item Editor", HgEngineDomain.Items)) return;

            try
            {
                await RunBusyAsync("Opening Item Editor…", UnpackHint,
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.itemData }));
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.itemIcons });
                var vm = new ItemEditorViewModel(GetItemNames());
                if (initialIndex > 0) vm.SelectedItemIndex = System.Math.Clamp(initialIndex, 0, vm.MaxItemIndex);
                new ItemEditorView(vm).ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("Couldn't open the Item Editor: " + ex.Message, "Item Editor");
            }
        }

        public static void OpenItemTableEditor()
        {
            if (!IsRomLoaded || !IsItemTableEditorAvailable()) return;
            new ItemTableEditorView(new ItemTableEditorViewModel(GetItemNames(), HeaderLists.GetHeaderListBoxNames())).ShowManaged();
        }

        public static void OpenMartEditor()
        {
            if (!IsRomLoaded) return;
            if (RomInfo.isHGE)
            {
                _ = DialogHelper.ShowInfo("The Mart Editor is disabled for hg-engine ROMs.", "Mart Editor");
                return;
            }
            if (!RomInfo.IsMartEditorAvailable())
            {
                _ = DialogHelper.ShowInfo(
                    "The Mart Editor currently supports English Diamond, Pearl, Platinum, HeartGold and SoulSilver ROMs.",
                    "Mart Editor");
                return;
            }
            try
            {
                var vm = new MartEditorViewModel(MartData.LoadCurrent(), GetItemNames());
                new EditorHostWindow("Mart Editor", new MartEditorView(vm), 980, 700).ShowManaged();
            }
            catch (System.Exception ex)
            {
                _ = DialogHelper.ShowError("The Mart Editor could not be opened:" + System.Environment.NewLine + ex.Message,
                    "Mart Editor");
            }
        }

        public static void OpenTradeEditor(int initialIndex = 0) => _ = OpenTradeEditorAsync(initialIndex);

        public static async System.Threading.Tasks.Task OpenTradeEditorAsync(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;

            try
            {
                await RunBusyAsync("Opening Trade Editor…", UnpackHint,
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.tradeData }));
                var view = new TradeEditorView();
                if (initialIndex > 0 && view.DataContext is TradeEditorViewModel vm)
                    _ = vm.ChangeTradeIDAsync(initialIndex);   // async load; freshly opened editor isn't dirty
                view.ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("Couldn't open the Trade Editor: " + ex.Message, "Trade Editor");
            }
        }

        public static void OpenTextEditor(int initialIndex = 0) => _ = OpenTextEditorAsync(initialIndex);

        public static async System.Threading.Tasks.Task OpenTextEditorAsync(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;

            try
            {
                await RunBusyAsync("Opening Text Editor…", UnpackHint,
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.textArchives }));
                new EditorHostWindow("Text Editor",
                    new TextEditorView(new TextEditorViewModel(true) { InitialIndex = initialIndex }),
                    980, 640).ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("Couldn't open the Text Editor: " + ex.Message, "Text Editor");
            }
        }

        public static void OpenStrVarHelp()
        {
            new StrVarHelpView(new StrVarHelpViewModel()).ShowManaged();
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
            // Diamond and Pearl have only the effect combos, and only on supported ROMs.
            if (!IsRomLoaded || (gameFamily == GameFamilies.DP && !DSPRE.ROMFiles.BattleMusicTables.IsSupported)) return;
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

        public static void OpenTmHmBulkEditor() => _ = OpenTmHmBulkEditorAsync();

        public static async System.Threading.Tasks.Task OpenTmHmBulkEditorAsync()
        {
            if (!IsRomLoaded || BlockedForHge("The TM/HM Bulk Editor", HgEngineDomain.Species)) return;

            try
            {
                await RunBusyAsync("Opening TM/HM Bulk Editor…", UnpackHint,
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.personalPokeData, DirNames.evolutions }));
                var vm = new TmHmBulkEditorViewModel(GetPokemonNames());
                new EditorHostWindow("TM/HM Bulk Editor", new TmHmBulkEditorView(vm), 1050, 700).ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("Couldn't open the TM/HM Bulk Editor: " + ex.Message, "TM/HM Bulk Editor");
            }
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

        public static void OpenWildEditor(int initialIndex = 0) => _ = OpenWildEditorAsync(initialIndex);

        public static async System.Threading.Tasks.Task OpenWildEditorAsync(int initialIndex = 0)
        {
            if (!IsRomLoaded || BlockedForHge("The Wild Pokémon Editor", HgEngineDomain.Encounters)) return;

            try
            {
                await RunBusyAsync("Opening Wild Pokémon Editor…", UnpackHint,
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.encounters, DirNames.monIcons }));
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
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("Couldn't open the Wild Pokémon Editor: " + ex.Message, "Wild Pokémon Editor");
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

        public static void OpenTrainerEditor(int initialIndex = 0) => _ = OpenTrainerEditorAsync(initialIndex);

        public static async System.Threading.Tasks.Task OpenTrainerEditorAsync(int initialIndex = 0)
        {
            if (!IsRomLoaded || BlockedForHge("The Trainer Editor", HgEngineDomain.Trainers)) return;

            try
            {
                await RunBusyAsync("Opening Trainer Editor…", UnpackHint,
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties, DirNames.trainerParty, DirNames.trainerGraphics }));
                new TrainerEditorView(new TrainerEditorViewModel(true) { InitialIndex = initialIndex }).ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("Couldn't open the Trainer Editor: " + ex.Message, "Trainer Editor");
            }
        }

        public static void OpenTrainerSpriteEditor(int initialClassIndex = 0) => _ = OpenTrainerSpriteEditorAsync(initialClassIndex);

        public static async System.Threading.Tasks.Task OpenTrainerSpriteEditorAsync(int initialClassIndex = 0)
        {
            if (!IsRomLoaded || BlockedForHge("The Trainer Sprite Editor", HgEngineDomain.TrainerGraphics)) return;

            try
            {
                await RunBusyAsync("Opening Trainer Sprite Editor…", UnpackHint,
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerGraphics }));
                new TrainerSpriteEditorView(new TrainerSpriteEditorViewModel(initialClassIndex)).ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("Couldn't open the Trainer Sprite Editor: " + ex.Message, "Trainer Sprite Editor");
            }
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

        public static void OpenHgEnginePatches()
        {
            if (!IsRomLoaded) return;
            if (!HgEngineProject.IsActive)
            {
                AppMessages.Info("Link an hg-engine checkout to see the patches it applies.",
                    "hg-engine patches");
                return;
            }

            new EditorHostWindow("hg-engine Patches",
                new Views.Tools.HgEnginePatchesView(new HgEnginePatchesViewModel()), 1150, 700).ShowManaged();
        }

        public static void OpenPokegearRematchEditor(int initialRowIndex = -1) => _ = OpenPokegearRematchEditorAsync(initialRowIndex);

        public static async System.Threading.Tasks.Task OpenPokegearRematchEditorAsync(int initialRowIndex = -1)
        {
            if (!IsRomLoaded) return;
            if (!PokegearRematchTable.IsSupported)
            {
                AppMessages.Info("The Pokégear Rematch Editor only supports HeartGold and SoulSilver.",
                    "Not Supported");
                return;
            }

            PokegearRematchViewModel vm = null;
            await RunBusyAsync("Opening Pokégear Rematch Editor…",
                "Reading the rematch table, the phone book and trainer names.",
                () => vm = new PokegearRematchViewModel(initialRowIndex));
            if (vm == null) return;

            new EditorHostWindow("Pokégear Rematch Editor",
                new PokegearRematchView(vm), 950, 640).ShowManaged();
        }

        public static void OpenPokegearPhoneBook(int initialEntry = -1) => _ = OpenPokegearPhoneBookAsync(initialEntry);

        public static async System.Threading.Tasks.Task OpenPokegearPhoneBookAsync(int initialEntry = -1)
        {
            if (!IsRomLoaded) return;
            if (!PokegearPhoneBook.IsSupported)
            {
                AppMessages.Info("The Pokégear Phone Book only exists in HeartGold and SoulSilver.", "Not Supported");
                return;
            }
            if (!BetaEditors.Allows("PokegearPhoneBookView"))
            {
                _ = DialogHelper.ShowInfo(BetaEditors.WhyNot("PokegearPhoneBookView"), "Pokégear Phone Book");
                return;
            }

            PokegearPhoneBookViewModel vm = null;
            await RunBusyAsync("Opening Pokégear Phone Book…",
                "Reading the phone book, contact names, trainers, maps and items.",
                () =>
                {
                    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties });
                    vm = new PokegearPhoneBookViewModel(initialEntry);
                });
            if (vm == null) return;

            new EditorHostWindow("Pokégear Phone Book", new PokegearPhoneBookView(vm), 1320, 700).ShowManaged();
        }

        public static void OpenStarterEditor() => _ = OpenStarterEditorAsync();

        public static async System.Threading.Tasks.Task OpenStarterEditorAsync()
        {
            if (!IsRomLoaded || BlockedForHge("The Starter Pokémon Editor") || !RomInfo.IsStarterEditorAvailable()) return;

            try
            {
                await RunBusyAsync("Opening Starter Editor…", UnpackHint,
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.scripts, DirNames.personalPokeData }));
                new StarterEditorView().ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("Couldn't open the Starter Editor: " + ex.Message, "Starter Editor");
            }
        }

        // ── World / data editors ───────────────────────────────────────────────
        public static void OpenFlyWarpEditor()
        {
            if (!IsRomLoaded) return;
            new FlyEditorView(HeaderLists.GetHeaderListBoxNames()).ShowManaged();
        }

        public static void OpenDungeonCutinEditor()
        {
            if (!IsRomLoaded) return;
            if (!RomInfo.IsDungeonCutinEditorAvailable())
            {
                _ = DialogHelper.ShowInfo(
                    "The Dungeon Cut-in editor is available for English and Spanish HeartGold and SoulSilver ROMs.",
                    "Dungeon Cut-in Editor");
                return;
            }
            if (!BetaEditors.Allows("DungeonCutinEditorView"))
            {
                _ = DialogHelper.ShowInfo(BetaEditors.WhyNot("DungeonCutinEditorView")!, "Dungeon Cut-in Editor");
                return;
            }
            new DungeonCutinEditorView(HeaderLists.GetHeaderListBoxNames()).ShowManaged();
        }

        public static void OpenTitleScreenEditor()
        {
            if (!IsRomLoaded) return;
            if (!RomInfo.IsTitleScreenEditorAvailable())
            {
                _ = DialogHelper.ShowInfo(
                    "The Title Screen editor is available for HeartGold and SoulSilver ROMs.",
                    "Title Screen Editor");
                return;
            }
            if (!BetaEditors.Allows("TitleScreenEditorView"))
            {
                _ = DialogHelper.ShowInfo(BetaEditors.WhyNot("TitleScreenEditorView")!, "Title Screen Editor");
                return;
            }
            new TitleScreenEditorView().ShowManaged();
        }

        /// <summary>Lists every cell animation in the game so one can be picked and opened.</summary>
        public static void OpenCellAnimationPicker() => _ = OpenCellAnimationPickerAsync();

        public static async System.Threading.Tasks.Task OpenCellAnimationPickerAsync()
        {
            if (!IsRomLoaded) return;
            if (!BetaEditors.Allows("CellAnimationEditorView"))
            {
                _ = DialogHelper.ShowInfo(BetaEditors.WhyNot("CellAnimationEditorView")!, "Cell Animations");
                return;
            }
            try
            {
                // Reading every archive to find the animations takes a moment, so the looking happens
                // behind the busy overlay. The window itself is built here, on the thread that owns it.
                var vm = new ViewModels.Graphics.CellAnimationPickerViewModel();
                await RunBusyAsync("Looking for animations…",
                                   "Reading every archive in the ROM", vm.Gather);
                vm.Ready();
                new Views.Graphics.CellAnimationPickerView(vm).ShowManaged();
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenCellAnimationPicker failed: " + ex.Message);
                await DialogHelper.ShowInfo("The animations could not be listed. " + ex.Message,
                                            "Cell Animations");
            }
        }

        /// <summary>
        /// Opens one cell animation for editing. The caller says which files go together, because an
        /// animation on its own has no drawing: it names cells in a layout, which in turn names tiles.
        /// </summary>
        /// <param name="sharedSheet">
        /// A sheet the game loads into sprite memory ahead of this one, or -1. The cells of some screens
        /// count their tiles from where that sheet ends.
        /// </param>
        /// <param name="poketchApp">
        /// The Pokétch application this animation belongs to, or -1. Given one, the editor draws the sprite
        /// inside that application's casing and screen instead of on an empty background, and offers a way
        /// back to it.
        /// </param>
        public static void OpenCellAnimationEditor(RomInfo.DirNames dir, int animation, int cells,
                                                   int sprites, int palette, int paletteRow, string what,
                                                   int sharedSheet = -1, int poketchApp = -1)
            => OpenCellAnimationEditor(Data.ArchiveFiles.Mapped(dir), animation, cells, sprites, palette, paletteRow,
                                       what, sharedSheet, poketchApp);

        /// <summary>The same, for an archive DSPRE does not map, which is read and saved in place.</summary>
        public static void OpenCellAnimationEditor(Data.ArchiveFiles source, int animation, int cells,
                                                   int sprites, int palette, int paletteRow, string what,
                                                   int sharedSheet = -1, int poketchApp = -1)
        {
            if (!IsRomLoaded) return;
            if (animation < 0)
            {
                _ = DialogHelper.ShowInfo("There is no animation file here to open.", "Cell Animation");
                return;
            }
            if (!BetaEditors.Allows("CellAnimationEditorView"))
            {
                _ = DialogHelper.ShowInfo(BetaEditors.WhyNot("CellAnimationEditorView")!, "Cell Animation");
                return;
            }

            try
            {
                var vm = new ViewModels.Graphics.CellAnimationEditorViewModel(
                    source, animation, cells, sprites, palette, paletteRow, what, sharedSheet, poketchApp);
                new Views.Graphics.CellAnimationEditorView(vm).ShowManaged();
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenCellAnimationEditor failed: " + ex.Message);
                _ = DialogHelper.ShowInfo("That animation could not be opened. " + ex.Message,
                                          "Cell Animation");
            }
        }

        /// <param name="poketchApp">
        /// A Pokétch application to open on, or -1 for whatever the window shows by default. An animation
        /// opened from an application uses this to go back to the one it belongs to.
        /// </param>
        public static void OpenBottomScreenEditor(int poketchApp = -1)
        {
            if (!IsRomLoaded) return;
            if (!RomInfo.IsBottomScreenEditorAvailable())
            {
                _ = DialogHelper.ShowInfo(
                    "The Bottom Screen editor is available for Diamond, Pearl, Platinum, HeartGold and SoulSilver ROMs.",
                    "Bottom Screen");
                return;
            }
            if (!BetaEditors.Allows("BottomScreenEditorView"))
            {
                _ = DialogHelper.ShowInfo(BetaEditors.WhyNot("BottomScreenEditorView")!, "Bottom Screen");
                return;
            }

            // A second window on the same screen would leave two views of one thing, each able to edit it.
            // So an open one is brought forward and pointed at the application asked for instead.
            var open = (global::Avalonia.Application.Current?.ApplicationLifetime
                        as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
                       ?.Windows;
            var already = open == null ? null
                : System.Linq.Enumerable.FirstOrDefault(
                    System.Linq.Enumerable.OfType<BottomScreenEditorView>(open));
            if (already != null)
            {
                ShowPoketchApp(already, poketchApp);
                already.Activate();
                return;
            }

            var view = new BottomScreenEditorView();
            ShowPoketchApp(view, poketchApp);
            view.ShowManaged();
        }

        // The list the window offers starts with the casing itself, so an application sits one along from
        // where it is in the Pokétch's own table.
        private static void ShowPoketchApp(BottomScreenEditorView view, int poketchApp)
        {
            if (poketchApp < 0 || view?.DataContext is not ViewModels.Graphics.BottomScreenEditorViewModel vm)
                return;
            int at = System.Array.FindIndex(Data.PoketchApps.All, a => a.Id == poketchApp);
            if (at >= 0) vm.SelectedApp = at + 1;
        }

        public static void OpenTrainerCardEditor()
        {
            if (!IsRomLoaded) return;
            if (!RomInfo.IsTrainerCardEditorAvailable())
            {
                _ = DialogHelper.ShowInfo(
                    "The Trainer Card editor is available for Diamond, Pearl, Platinum, HeartGold and SoulSilver ROMs.",
                    "Trainer Card Editor");
                return;
            }
            if (!BetaEditors.Allows("TrainerCardEditorView"))
            {
                _ = DialogHelper.ShowInfo(BetaEditors.WhyNot("TrainerCardEditorView")!, "Trainer Card Editor");
                return;
            }
            new TrainerCardEditorView().ShowManaged();
        }

        /// <summary>Opens one particle file for editing; <paramref name="changed"/> hears which entry was saved.</summary>
        public static void OpenParticleEditor(DirNames archive, int entry, string what, System.Action<int> changed,
                                              bool orthographic = false)
        {
            if (!IsRomLoaded) return;
            if (gameDirs.ContainsKey(archive)) DSUtils.TryUnpackNarcs(new List<DirNames> { archive });
            OpenParticleEditor(Data.ArchiveFiles.Mapped(archive), entry, what, changed, orthographic);
        }

        /// <summary>The same, for any archive, including ones DSPRE does not map, which are saved in place.</summary>
        public static void OpenParticleEditor(Data.ArchiveFiles source, int entry, string what, System.Action<int> changed,
                                              bool orthographic = false)
        {
            if (!IsRomLoaded) return;
            if (!BetaEditors.Allows("ParticleEditorView"))
            {
                _ = DialogHelper.ShowInfo(BetaEditors.WhyNot("ParticleEditorView")!, "Particles");
                return;
            }
            try
            {
                var vm = new ViewModels.Graphics.ParticleEditorViewModel(source, entry, what, changed, orthographic);
                new Views.Graphics.ParticleEditorView(vm).ShowManaged();
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenParticleEditor failed: " + ex.Message);
                _ = DialogHelper.ShowInfo("These particles could not be opened. " + ex.Message, "Particles");
            }
        }

        /// <summary>Lists every particle file by what it is for.</summary>
        public static void OpenParticleLibrary() => _ = OpenParticleLibraryAsync();

        public static async System.Threading.Tasks.Task OpenParticleLibraryAsync()
        {
            if (!IsRomLoaded) return;
            if (!BetaEditors.Allows("ParticleLibraryView"))
            {
                _ = DialogHelper.ShowInfo(BetaEditors.WhyNot("ParticleLibraryView")!, "Particles");
                return;
            }
            try
            {
                var vm = new ViewModels.Graphics.ParticleLibraryViewModel();
                await RunBusyAsync("Looking for particles…", "Reading every archive in the ROM", () =>
                {
                    DSUtils.TryUnpackNarcs(new List<DirNames> {
                        DirNames.wazaParticle, DirNames.ballParticles, DirNames.wazaEffectScripts, DirNames.wazaEffectSub }
                        .Where(d => gameDirs.ContainsKey(d)).ToList());
                    vm.Gather();
                });
                vm.Ready();
                new Views.Graphics.ParticleLibraryView(vm).ShowManaged();
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenParticleLibrary failed: " + ex.Message);
                await DialogHelper.ShowInfo("The particles could not be listed. " + ex.Message, "Particles");
            }
        }

        public static void OpenBallCapsuleEditor() => _ = OpenBallCapsuleEditorAsync();

        /// <summary>Opens Ball Capsules on one trainer capsule, numbered the way a party entry names it.</summary>
        public static void OpenBallCapsuleEditorAt(int trainerCapsule) => _ = OpenBallCapsuleEditorAsync(trainerCapsule);

        public static async System.Threading.Tasks.Task OpenBallCapsuleEditorAsync(int trainerCapsule = 0)
        {
            if (!IsRomLoaded) return;
            if (!BetaEditors.Allows("BallCapsuleEditorView"))
            {
                _ = DialogHelper.ShowInfo(BetaEditors.WhyNot("BallCapsuleEditorView")!, "Ball Capsules");
                return;
            }
            Dictionary<int, List<string>> usedBy = null;
            try
            {
                await RunBusyAsync("Opening Ball Capsules…", UnpackHint, () =>
                {
                    DSUtils.TryUnpackNarcs(new List<DirNames> {
                        DirNames.personalPokeData, DirNames.pokemonBattleSprites, DirNames.otherPokemonBattleSprites,
                        DirNames.monIcons, DirNames.sealGraphics, DirNames.trainerCapsules, DirNames.fonts, DirNames.windowFrames }
                        .Where(d => gameDirs.ContainsKey(d)).ToList());
                    Data.SendOutGraphics.Unpack();
                    usedBy = Data.TrainerCapsuleCatalog.UsedBy();
                });
                SetMonIconsPalTableAddress();

                string[] names = GetPokemonNamesWithForms(GetPersonalFilesCount());
                var vm = new ViewModels.Graphics.BallCapsuleEditorViewModel(names) { UsedBy = usedBy };
                if (trainerCapsule > 0) vm.CapsuleIndex = trainerCapsule;
                new Views.Graphics.BallCapsuleEditorView(vm).ShowManaged();
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenBallCapsuleEditor failed: " + ex.Message);
                await DialogHelper.ShowError("The Ball Capsule editor could not be opened: " + ex.Message, "Ball Capsules");
            }
        }

        public static async System.Threading.Tasks.Task OpenBannerEditorAsync()
        {
            if (!IsRomLoaded) return;
            if (!RomInfo.IsDsRomProject)
            {
                await DialogHelper.ShowInfo(
                    "Editing the game icon and banner titles requires a ds-rom-format project.\n" +
                    "Use File → Convert to ds-rom format, then reopen this editor.",
                    "ds-rom project required");
                return;
            }
            new BannerEditorView(new ViewModels.Graphics.BannerEditorViewModel()).ShowManaged();
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

        public static void OpenOverworldEditor() => _ = OpenOverworldEditorAsync();

        public static async System.Threading.Tasks.Task OpenOverworldEditorAsync()
        {
            if (!IsRomLoaded) return;

            try
            {
                await RunBusyAsync("Opening Overworld Editor…", UnpackHint,
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.OWSprites }));
                SetOWtable();
                Set3DOverworldsDict();
                ReadOWTable();
                new BtxEditorView(new BtxEditorViewModel(true)).ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("Couldn't open the Overworld Editor: " + ex.Message, "Overworld Editor");
            }
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
                ($"Go to Move animation #{n}", "move animation effect particles", () => OpenBattleScriptEditor(3, n)),
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
        public static void OpenGraphicsBrowser() => _ = OpenGraphicsBrowserAsync();

        public static async System.Threading.Tasks.Task OpenGraphicsBrowserAsync()
        {
            try
            {
                var vm = new ViewModels.Graphics.GraphicsBrowserViewModel(loadImmediately: false);
                await RunBusyAsync("Opening Graphics…", UnpackHint, vm.Scan);
                vm.Publish();
                new Views.Graphics.GraphicsBrowserView(vm).ShowManaged();
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenGraphicsBrowser failed: " + ex.Message);
                await DialogHelper.ShowInfo("The graphics list could not be opened. Open a ROM first.", "Graphics");
            }
        }

        /// <summary>Opens the graphics window already looking at one file.</summary>
        public static void OpenGraphicAt(RomInfo.DirNames archive, int fileIndex, bool preferAssembled = false)
            => _ = OpenGraphicAtAsync(archive, fileIndex, preferAssembled);

        private static async System.Threading.Tasks.Task OpenGraphicAtAsync(RomInfo.DirNames archive, int fileIndex, bool preferAssembled)
        {
            try
            {
                var a = Data.GraphicAssets.All.FirstOrDefault(x => x.Dir == archive);
                if (a == null)
                {
                    await DialogHelper.ShowInfo("That kind of graphic is not one this window lists yet.", "Graphics");
                    return;
                }

                var vm = new ViewModels.Graphics.GraphicsBrowserViewModel(loadImmediately: false);
                await RunBusyAsync("Opening Graphics…", UnpackHint, vm.Scan);
                vm.Publish();
                bool found = vm.JumpTo(a, fileIndex, preferAssembled);
                new Views.Graphics.GraphicsBrowserView(vm).ShowManaged();
                if (!found)
                    vm.Status = "That graphic could not be found in this game, so the whole list is shown instead.";
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenGraphicAt failed: " + ex.Message);
                await DialogHelper.ShowInfo("The graphics list could not be opened. Open a ROM first.", "Graphics");
            }
        }

        public static void OpenTrainerBackSpriteEditor(int initialSprite = 0) => _ = OpenTrainerBackSpriteEditorAsync(initialSprite);

        public static async System.Threading.Tasks.Task OpenTrainerBackSpriteEditorAsync(int initialSprite = 0)
        {
            if (!IsRomLoaded || BlockedForHge("The Trainer Back Sprite Editor", HgEngineDomain.TrainerGraphics)) return;

            try
            {
                await RunBusyAsync("Opening Trainer Back Sprite Editor…", UnpackHint,
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerBackGraphics }));
                new TrainerSpriteEditorView(new TrainerSpriteEditorViewModel(initialSprite, TrainerSpriteSet.Backs)).ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("Couldn't open the Trainer Back Sprite Editor: " + ex.Message, "Trainer Back Sprite Editor");
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
                {
                    var icon = DSPRE.ROMFiles.PokemonIconFiles.Describe(fileIndex);
                    if (icon == null) return null;
                    int entry = icon.EditorId;
                    return ("Pokemon Editor", () => { _ = OpenPokemonEditorAsync(entry); });
                }

                case RomInfo.DirNames.trainerGraphics:
                    return ("Trainer Sprite Editor", () => OpenTrainerSpriteEditor(TrainerGraphicsLayout.ClassOf(fileIndex)));

                case RomInfo.DirNames.trainerBackGraphics:
                    return ("Trainer Back Sprite Editor", () => OpenTrainerBackSpriteEditor(TrainerGraphicsLayout.ClassOf(fileIndex)));

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

                case RomInfo.DirNames.sealGraphics:
                    return ("Ball Capsules", OpenBallCapsuleEditor);

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
            if (BlockedForHgeArchive("The Font Editor", DirNames.fonts)) return;

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

        public static void OpenBattleScreenEditor() => _ = OpenBattleScreenEditorAsync();

        public static async System.Threading.Tasks.Task OpenBattleScreenEditorAsync()
        {
            if (!IsRomLoaded) return;

            if (!BetaEditors.Allows("BattleScreenEditorView"))
            {
                _ = DialogHelper.ShowInfo(BetaEditors.WhyNot("BattleScreenEditorView"), "Battle screen");
                return;
            }
            if (BlockedForHgeArchive("The Battle Screen Editor",
                DirNames.battleObj, DirNames.windowFrames, DirNames.fonts)) return;

            try
            {
                await RunBusyAsync("Opening Battle Screen…", UnpackHint,
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> {
                        DirNames.battleObj, DirNames.battleBg, DirNames.windowFrames, DirNames.fonts }));
                new Views.Battle.BattleScreenEditorView().ShowManaged();
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenBattleScreenEditor failed: " + ex.Message);
                await DialogHelper.ShowInfo("The battle screen could not be opened. " + ex.Message,
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
        public static void OpenModelBrowser() => _ = OpenModelBrowserAsync();

        public static async System.Threading.Tasks.Task OpenModelBrowserAsync()
        {
            try
            {
                // Listing means reading every 3D archive to see what is in it, which is far too much
                // file work to do on the click.
                var vm = new ViewModels.Graphics.ModelBrowserViewModel();
                await RunBusyAsync("Opening Models…", UnpackHint, vm.Scan);
                vm.Publish();
                new Views.Graphics.ModelBrowserView(vm).ShowManaged();
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenModelBrowser failed: " + ex.Message);
                await DialogHelper.ShowInfo("The models list could not be opened. Open a ROM first.", "Models");
            }
        }

        /// <summary>The editor list shown in the command palette (mirrors the main menu).</summary>
        public static List<CommandItem> BuildCommands() => new()
        {
            new() { Name = "Graphics",              Keywords = "sprite picture image texture palette colour color icon font paint draw", Run = OpenGraphicsBrowser },
            new() { Name = "Models and textures",   Keywords = "3d model nsbmd nsbtx building overworld map mesh", Run = OpenModelBrowser },
            new() { Name = "Battle screens",        Keywords = "battle screen gauge hp bar backdrop platform message box touch command", Run = OpenBattleScreenEditor },
            new() { Name = "Battle scenes",         Keywords = "battle scene backdrop terrain platform ground", Run = OpenBattleSceneBrowser },
            new() { Name = "Picture to Background", Keywords = "png tiles tilemap palette background", Run = OpenTilesetBuilder },
            new() { Name = "Title Screen Editor",   Keywords = "logo copyright intro hgss", Run = OpenTitleScreenEditor },
            new() { Name = "Bottom Screen",         Keywords = "touch menu poketch pokétch bottom screen field panel icons poke ball", Run = () => OpenBottomScreenEditor() },
            new() { Name = "Cell Animations",       Keywords = "nanr animation frames cell sprite sequence playback timing", Run = OpenCellAnimationPicker },
            new() { Name = "Dungeon Cutin Editor",  Keywords = "dungeon location splash hgss", Run = OpenDungeonCutinEditor },
            new() { Name = "Trainer Card Editor",   Keywords = "rank front back graphics", Run = OpenTrainerCardEditor },
            new() { Name = "Particles",             Keywords = "spa particle emitter effect move animation seal burst sparkle", Run = OpenParticleLibrary },
            new() { Name = "Ball Capsules",         Keywords = "seal sticker capsule poke ball send out particles effect", Run = OpenBallCapsuleEditor },
            new() { Name = "Audio Editor",          Keywords = "sound cry cries music bgm fanfare sfx song", Run = () => { _ = OpenAudioEditorAsync(); } },
            new() { Name = "Pokémon Editor",        Keywords = "species personal learnset evolution sprite", Run = () => { _ = OpenPokemonEditorAsync(); } },
            new() { Name = "Form Editor (hg-engine)", Keywords = "mega regional alolan galarian gmax gigantamax primal reversion form", Run = OpenHgEngineFormEditor },
            new() { Name = "Move Data Editor",      Keywords = "attack",   Run = () => OpenMoveDataEditor() },
            new() { Name = "TM / HM Editor",        Keywords = "machine",  Run = () => OpenTMEditor() },
            new() { Name = "TM/HM Bulk Editor",     Keywords = "machine compatibility bulk family sync copy", Run = OpenTmHmBulkEditor },
            new() { Name = "Egg Move Editor",       Keywords = "breeding", Run = OpenEggMoveEditor },
            new() { Name = "Battle Script Editor",  Keywords = "move sequence waza be_seq sub_seq effect animation west", Run = () => OpenBattleScriptEditor() },
            new() { Name = "Item Editor",           Run = () => OpenItemEditor() },
            new() { Name = "Mart Editor",           Keywords = "shop store inventory stock poke mart", Run = OpenMartEditor },
            new() { Name = "Item Tables (Pickup, Hidden, Rock Smash)", Keywords = "pickup hidden ground rock smash item table hgss", Run = OpenItemTableEditor },
            new() { Name = "Trade Editor",          Keywords = "in-game",  Run = () => OpenTradeEditor() },
            new() { Name = "Starter Pokémon Editor", Keywords = "turtwig chimchar piplup chikorita cyndaquil totodile rival professor", Run = OpenStarterEditor },
            new() { Name = "Trainer Editor",        Keywords = "battle party", Run = () => OpenTrainerEditor() },
            new() { Name = "Trainer Sprite Editor", Keywords = "class pixel paint", Run = () => OpenTrainerSpriteEditor() },
            new() { Name = "Trainer Back Sprite Editor", Keywords = "player back sprite throw palette animation", Run = () => OpenTrainerBackSpriteEditor() },
            new() { Name = "Vs. Seeker Rematch Editor", Keywords = "rematch trainer encounter chain", Run = () => OpenVsSeekerRematchEditor() },
            new() { Name = "Pokégear Rematch Editor", Keywords = "rematch trainer phone pokegear call hgss", Run = () => OpenPokegearRematchEditor() },
            new() { Name = "Pokégear Phone Book", Keywords = "phone contact number call gift greeting pokegear hgss", Run = () => OpenPokegearPhoneBook() },
            new() { Name = "hg-engine Patches", Keywords = "hook bytereplacement repoint arm9 overlay patch asm", Run = OpenHgEnginePatches },
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
            new() { Name = "Game Icon & Banner",    Keywords = "rom icon ds menu title", Run = () => { _ = OpenBannerEditorAsync(); } },
            new() { Name = "Edit Dropdown Labels",  Keywords = "enum custom", Run = OpenLabelEditor },
            new() { Name = "Validation & Where-Used", Keywords = "check broken references project health", Run = OpenProjectChecks },
            new() { Name = "Settings",              Run = OpenSettings },
            // ── Actions (not editors) ──
            new() { Name = "Toggle theme (Dark / Light)", Keywords = "dark light appearance", Run = ThemeManager.Toggle },
        };
    }
}
