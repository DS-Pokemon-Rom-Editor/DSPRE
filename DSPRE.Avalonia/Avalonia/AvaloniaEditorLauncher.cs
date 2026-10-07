using System.Collections.Generic;
using DSPRE.Avalonia.ViewModels;
using DSPRE.Avalonia.Views;
using DSPRE.HgEngine;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using System.Linq;
using Avalonia.Controls;
using System.Text.RegularExpressions;
using DSPRE.Avalonia.Data;

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

        /// <summary>
        /// The one chokepoint for opening an editor, shared with the menus through <see cref="EditorAvailability"/>: it
        /// also covers the Ctrl+P palette and the header-tree context menu. Says why, except when no ROM is open.
        /// </summary>
        internal static bool Refused(string editor)
        {
            string why = EditorAvailability.WhyNot(editor);
            if (why == null) return false;
            if (EditorAvailability.RomOpen || why != EditorAvailability.NoRom)
                AppMessages.Info(why, EditorAvailability.TitleOf(editor));
            return true;
        }

        /// <summary>Runs unpack-heavy file I/O off the UI thread behind the app's busy overlay. Only pass plain file I/O, not UI/bitmap work.</summary>
        /// <summary>" at NAME" for a busy title when an editor opens on a particular record, or nothing.</summary>
        private static string At(System.Func<string[]> names, int index)
        {
            try
            {
                string[] all = names();
                return index >= 0 && index < all.Length && !string.IsNullOrWhiteSpace(all[index]) ? " at " + all[index].Trim() : "";
            }
            catch { return ""; }
        }

        private static System.Threading.Tasks.Task RunBusyAsync(string busyText, string busyHint, System.Action work)
        => BusyOverlay.RunAsync(busyText, busyHint, work);

        /// <summary>
        /// Everything in the ROM that makes a noise: the cries, the music, the fanfares and the sound
        /// effects, listed out of the sound archive's own names.
        /// </summary>
        /// <param name="showCryFor">A species to open on, for the Pokemon editor's own Edit cry button.
        /// Zero opens on nothing in particular.</param>
        public static async System.Threading.Tasks.Task OpenAudioEditorAsync(int showCryFor = 0)
        {
            if (Refused("AudioEditorView")) return;

            try
            {
                // The species list is counted from the personal data, which a fresh project has not unpacked yet.
                await RunBusyAsync("Opening Audio Editor…", "Reading the cries, music and sound effects.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.personalPokeData }));

                string[] names;
                try { names = GetPokemonNamesWithForms(GetPersonalFilesCount()); }
                catch { names = System.Array.Empty<string>(); }

                await System.Threading.Tasks.Task.Yield();
                AudioEditorViewModel vm = new ViewModels.Audio.AudioEditorViewModel(names);
                if (showCryFor > 0) vm.ShowCryFor(showCryFor);
                new Views.Audio.AudioEditorView(vm).ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("The Audio Editor could not be opened:" + System.Environment.NewLine + ex.Message, "Audio Editor");
            }
        }

        // ── Pokémon-related editors ────────────────────────────────────────────
        public static void OpenPokemonEditor(int species) => _ = OpenPokemonEditorAsync(species);

        public static async System.Threading.Tasks.Task OpenPokemonEditorAsync(int initialMon = 1)
        {
            if (Refused("PokemonEditorView")) return;

            try
            {
                await RunBusyAsync("Opening Pokémon Editor" + (initialMon != 1 ? At(GetPokemonNames, initialMon) : "") + "…",
                    "Reading species data, learnsets, evolutions and sprites.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> {
                        DirNames.personalPokeData, DirNames.learnsets,
                        DirNames.evolutions, DirNames.monIcons }));
                SetMonIconsPalTableAddress();

                // Full Pokémon name list (base + alt forms this ROM actually has data for + extras)
                string[] fullList = GetPokemonNamesWithForms(GetPersonalFilesCount());
                string[] moveNames = GetAttackNames();

                int mon = System.Math.Clamp(initialMon, 0, System.Math.Max(0, fullList.Length - 1));
                PokemonEditorViewModel vm = new PokemonEditorViewModel(fullList, moveNames, initialMon: mon);
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
            if (Refused("HgEngineFormEditorView")) return;
            HgEngineFormEditorViewModel vm = new HgEngineFormEditorViewModel(GetPokemonNames());
            new HgEngineFormEditorView(vm).ShowManaged();
        }

        /// <summary>Which battle background files each move-effect background draws with.</summary>
        public static void OpenMoveBackgroundEditor()
        {
            if (Refused("MoveBackgroundEditorView")) return;
            new Views.Battle.MoveBackgroundEditorView(new ViewModels.Battle.MoveBackgroundEditorViewModel(load: true)).ShowManaged();
        }

        /// <summary>hg-engine-only: each ability's battle flags (data/AbilityFlags.c).</summary>
        public static void OpenAbilityFlagsEditor()
        {
            if (Refused("AbilityFlagsEditorView")) return;
            new AbilityFlagsEditorView(new AbilityFlagsEditorViewModel(load: true)).ShowManaged();
        }

        public static void OpenMoveDataEditor(int initialIndex = 0) => _ = OpenMoveDataEditorAsync(initialIndex);

        public static async System.Threading.Tasks.Task OpenMoveDataEditorAsync(int initialIndex = 0)
        {
            if (Refused("MoveDataEditorView")) return;

            try
            {
                await RunBusyAsync("Opening Move Data Editor" + (initialIndex > 0 ? At(GetAttackNames, initialIndex) : "") + "…", "Reading every move.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.moveData }));
                MoveDataEditorView view = new MoveDataEditorView();
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
            if (Refused("TMEditorView")) return;
            TMEditorView view = new TMEditorView();
            if (initialIndex > 0 && view.DataContext is TMEditorViewModel vm)
                vm.SelectedMachineIndex = initialIndex;   // setter loads the machine
            view.ShowManaged();
        }

        public static void OpenEggMoveEditor()
        {
            // hg-engine rebuilds egg moves (a/2/2/9) from data/learnsets/learnsets.json.
            if (Refused("EggMoveEditorView")) return;
            new EggMoveEditorView().ShowManaged();
        }

        /// <summary>From the Move editor, archive 0 and the move number jump straight to that move's script.</summary>
        public static void OpenBattleScriptEditor(int archive = 0, int entryIndex = 0)
        {
            // Refused too while hg-engine builds the particle archive, which has no source view yet.
            if (Refused("BattleScriptEditorView")) return;
            BattleScriptEditorViewModel vm = new BattleScriptEditorViewModel();
            BattleScriptEditorView view = new BattleScriptEditorView { DataContext = vm };
            if (vm.IsAvailable)
            {
                vm.ArchiveIndex = System.Math.Clamp(archive, 0, 3);   // setter rebuilds the entry list
                if (entryIndex > 0 && vm.FileItems.Count > 0)
                    vm.SelectedFileIndex = System.Math.Min(entryIndex, vm.FileItems.Count - 1);
            }
            new EditorHostWindow("Battle scripts", view, 1320, 800).ShowManaged();
        }

        public static void OpenItemEditor(int initialIndex = 1) => _ = OpenItemEditorAsync(initialIndex);

        public static async System.Threading.Tasks.Task OpenItemEditorAsync(int initialIndex = 1)
        {
            if (Refused("ItemEditorView")) return;

            try
            {
                await RunBusyAsync("Opening Item Editor" + (initialIndex > 1 ? At(GetItemNames, initialIndex) : "") + "…", "Reading every item and its icon.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.itemData }));
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.itemIcons });
                ItemEditorViewModel vm = new ItemEditorViewModel(GetItemNames());
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
            if (Refused("ItemTableEditorView")) return;
            new ItemTableEditorView(new ItemTableEditorViewModel(GetItemNames(), HeaderLists.GetHeaderListBoxNames())).ShowManaged();
        }

        public static void OpenMartEditor()
        {
            if (Refused("MartEditorView")) return;
            if (BringForward<MartEditorView, MartEditorViewModel>(_ => { })) return;
            try
            {
                MartEditorViewModel vm = new MartEditorViewModel(MartData.LoadCurrent(), GetItemNames());
                new EditorHostWindow("Mart editor", new MartEditorView(vm), 980, 700).ShowManaged();
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
            if (Refused("TradeEditorView")) return;

            try
            {
                await RunBusyAsync("Opening Trade Editor…", "Reading the in-game trades.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.tradeData }));
                TradeEditorView view = new TradeEditorView();
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
                await RunBusyAsync("Opening Text Editor…", "Reading every text archive.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.textArchives }));
                new EditorHostWindow("Text editor",
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

        /// <param name="scriptNumber">The jump-table slot to scroll to in that file once it loads;
        /// 0 opens on the file alone.</param>
        public static void OpenScriptEditor(int initialIndex = 0, int scriptNumber = 0)
        {
            if (!IsRomLoaded) return;
            new EditorHostWindow("Rotom script editor",
                new ScriptEditorView(new ScriptEditorViewModel(true) { InitialIndex = initialIndex, InitialScriptNumber = scriptNumber }),
                980, 760).ShowManaged();
        }

        /// <summary>
        /// Opens the Script Editor on a stored script number: a common script in the file that holds it, any other
        /// number in the paired script file, which is only looked up when needed. Returns a status line.
        /// </summary>
        public static string GoToScript(int scriptNumber, System.Func<int> pairedScriptFile) =>
            GoToScript(scriptNumber, pairedScriptFile, out _);

        public static string GoToScript(int scriptNumber, System.Func<int> pairedScriptFile, out bool opened)
        {
            opened = false;
            CommonScriptId.Result result = CommonScriptId.Resolve(gameFamily, scriptNumber);
            if (result.Kind == CommonScriptId.Kind.Discrepancy)
                return $"Script {scriptNumber} is a Common Script in an ambiguous range ({result.RangeLower}-{result.RangeUpper}); it is one of: {string.Join(", ", result.CandidateArchives)}.";
            if (result.Kind == CommonScriptId.Kind.Resolved)
            {
                OpenScriptEditor(result.ScriptArchiveId, result.ManualUserId);
                opened = true;
                return $"Common Script {result.ManualUserId} lives in script file {result.ScriptArchiveId}.";
            }
            // Below the common-script ranges the number is the paired file's own jump-table slot.
            int file = pairedScriptFile?.Invoke() ?? -1;
            if (file < 0) return $"No header links a script file here, so script {scriptNumber} can't be opened.";
            OpenScriptEditor(file, scriptNumber);
            opened = true;
            return $"Opened script {scriptNumber} of script file {file}.";
        }

        public static void OpenLevelScriptEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            new EditorHostWindow("Level script editor",
                new LevelScriptEditorView(new LevelScriptEditorViewModel(true) { InitialIndex = initialIndex }),
                720, 560).ShowManaged();
        }

        public static void OpenTableEditor()
        {
            // Diamond and Pearl have only the effect combos, and only on supported ROMs.
            if (Refused("TableEditorView")) return;
            if (BringForwardWindow<TableEditorView>()) return;
            new TableEditorView(new TableEditorViewModel(HeaderLists.GetHeaderListBoxNames())).ShowManaged();
        }

        /// <param name="headbuttFile">Opens on the Headbutt tab at this file; -1 opens on the first tab.</param>
        public static void OpenSpecialEncountersEditor(int headbuttFile = -1)
        {
            if (!IsRomLoaded) return;
            // On hg-engine only Headbutt, and Safari once a checkout is linked, are edited from source here.
            bool headbuttOnly = RomInfo.isHGE;
            if (headbuttOnly && (gameFamily != GameFamilies.HGSS || Refused("SpecialEncountersEditorView"))) return;
            new SpecialEncountersEditorView(new SpecialEncountersEditorViewModel(headbuttOnly, headbuttFile)).ShowManaged();
        }

        /// <summary>Opens a small editor for one of the fixed game tables, or says why this ROM can't.</summary>
        /// <param name="whyNot">Run only after the ROM and hg-engine checks: some checks unpack or decompress files.</param>
        private static void OpenTableEditor<TView>(string title, System.Func<string> whyNot, System.Func<TView> make, double width, double height,
            double minWidth = 420, double minHeight = 220)
            where TView : global::Avalonia.Controls.Control
        {
            // Before anything reads the ROM: loading a table can decompress its overlay.
            if (Refused(typeof(TView).Name)) return;
            // Two windows on one table would each keep their own saved copy and overwrite each other.
            IReadOnlyList<Window> open = (global::Avalonia.Application.Current?.ApplicationLifetime
                        as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Windows;
            EditorHostWindow already = open?.OfType<EditorHostWindow>().FirstOrDefault(w => w.Content is TView);
            if (already != null)
            {
                if (already.WindowState == global::Avalonia.Controls.WindowState.Minimized) already.WindowState = global::Avalonia.Controls.WindowState.Normal;
                already.Activate();
                return;
            }
            try
            {
                if (whyNot() is string why) { _ = DialogHelper.ShowInfo(why, title); return; }
                EditorHostWindow window = new EditorHostWindow(title, make(), width, height)
                {
                    MinWidth = System.Math.Min(width, minWidth),
                    MinHeight = System.Math.Min(height, minHeight),
                };
                window.ShowManaged();
            }
            catch (System.Exception ex) when (ex is System.IO.IOException || ex is System.IO.InvalidDataException || ex is System.InvalidOperationException
                                              || ex is System.ArgumentException || ex is System.UnauthorizedAccessException)
            {
                _ = DialogHelper.ShowError(title + " could not be opened:\n" + ex.Message, title);
            }
        }

        public static void OpenWildHeldItems() => OpenTableEditor("Wild Held Items", WildHeldItemOdds.WhyNot,
            () => new WildHeldItemOddsView(new WildHeldItemOddsViewModel(true)), 640, 360, 460, 340);

        public static void OpenGrowthCurves() => OpenTableEditor("Growth Curves", GrowthTable.WhyNot,
            () => new GrowthCurveEditorView(new GrowthCurveEditorViewModel(true)), 900, 680, 700, 460);

        public static void OpenFriendshipChanges() => OpenTableEditor("Friendship Changes", FriendshipTable.WhyNot,
            () => new FriendshipChangesView(new FriendshipChangesViewModel(true)), 760, 480, 640, 440);

        public static void OpenEncounterSlotOdds() => OpenTableEditor("Encounter Slot Odds", EncounterSlotOdds.WhyNot,
            () => new EncounterSlotOddsView(new EncounterSlotOddsViewModel(true)), 560, 660, 440, 480);

        public static void OpenBreedingItems() => OpenTableEditor("Breeding Items", IncenseBreedingTable.WhyNot,
            () => new BreedingItemsView(new BreedingItemsViewModel(true)), 820, 440, 790, 360);

        public static void OpenBerryData() => OpenTableEditor("Berry Data", BerryData.WhyNot,
            () => new BerryDataEditorView(new BerryDataEditorViewModel(true)), 800, 580, 760, 480);

        public static void OpenTypeChart() => OpenTableEditor("Type Chart", TypeChart.WhyNot,
            () => new TypeChartEditorView(new TypeChartEditorViewModel(true)), 1080, 780);

        public static void OpenMoveTutors() => OpenTableEditor("Move Tutors", MoveTutorData.WhyNot,
            () => new MoveTutorEditorView(new MoveTutorEditorViewModel(true)), 1000, 680, 820, 420);

        public static void OpenBpShop() => OpenTableEditor("Battle Point Shop", BpShopData.WhyNot,
            () => new BpShopEditorView(new BpShopEditorViewModel(true)), 1000, 640, 760, 420);

        public static void OpenUndergroundMining() => OpenTableEditor("Underground Mining", MiningTable.WhyNot,
            () => new UndergroundMiningView(new UndergroundMiningViewModel(true)), 1000, 700, 900, 420);

        public static void OpenTmHmBulkEditor() => _ = OpenTmHmBulkEditorAsync();

        public static async System.Threading.Tasks.Task OpenTmHmBulkEditorAsync()
        {
            // It writes personal data, but hg-engine takes machine compatibility from data/learnsets/learnsets.json.
            if (Refused("TmHmBulkEditorView")) return;

            try
            {
                await RunBusyAsync("Opening TM/HM Bulk Editor…", "Reading which Pokémon learn each TM and HM.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.personalPokeData, DirNames.evolutions }));
                TmHmBulkEditorViewModel vm = new TmHmBulkEditorViewModel(GetPokemonNames());
                EditorHostWindow window = new EditorHostWindow("TM/HM bulk editor", new TmHmBulkEditorView(vm), 1050, 700);
                window.Closed += (_, _) => vm.Detach();
                window.ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("Couldn't open the TM/HM Bulk Editor: " + ex.Message, "TM/HM Bulk Editor");
            }
        }

        public static void OpenBattleTowerEditor()
        {
            if (Refused("BattleTowerEditorView")) return;
            new EditorHostWindow("Battle Tower editor",
                new BattleTowerEditorView(new BattleTowerEditorViewModel()),
                1000, 700).ShowManaged();
        }

        public static void OpenWildEditor(int initialIndex = 0) => _ = OpenWildEditorAsync(initialIndex);

        public static async System.Threading.Tasks.Task OpenWildEditorAsync(int initialIndex = 0)
        {
            if (Refused("WildEditorView")) return;

            try
            {
                await RunBusyAsync("Opening Wild Pokémon Editor…", "Reading this game's encounter files.",
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
                    WildEditorDPPtViewModel vm = new WildEditorDPPtViewModel(path, names, initialIndex, headerCount);
                    EditorHostWindow window = new EditorHostWindow("Wild Pokémon editor (DPPt)", new WildEditorDPPtView(vm), 1000, 680) { MinWidth = 960, MinHeight = 520 };
                    window.Closed += (_, _) => vm.Detach();
                    window.ShowManaged();
                }
                else
                {
                    WildEditorHGSSViewModel vm = new WildEditorHGSSViewModel(path, names, initialIndex, headerCount);
                    EditorHostWindow window = new EditorHostWindow("Wild Pokémon editor (HGSS)", new WildEditorHGSSView(vm), 1000, 680) { MinWidth = 960, MinHeight = 520 };
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
            if (BringForward<HeaderEditorView, HeaderEditorViewModel>(vm => { if (initialIndex >= 0) vm.GoToHeader(initialIndex); })) return;
            HeaderEditorViewModel model = new HeaderEditorViewModel(true) { InitialHeaderId = initialIndex };
            EditorHostWindow window = new EditorHostWindow("Header editor", new HeaderEditorView(model));
            window.Closed += (_, _) => model.Detach();
            window.ShowManaged();
        }

        // Each copy of these windows saves everything it shows, so a second one would save over the first.
        private static bool BringForwardWindow<TWindow>() where TWindow : global::Avalonia.Controls.Window
        {
            IReadOnlyList<Window> open = (global::Avalonia.Application.Current?.ApplicationLifetime
                        as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Windows;
            TWindow window = open?.OfType<TWindow>().FirstOrDefault();
            if (window == null) return false;
            if (window.WindowState == global::Avalonia.Controls.WindowState.Minimized) window.WindowState = global::Avalonia.Controls.WindowState.Normal;
            window.Activate();
            return true;
        }

        // One standalone window per world editor, since a second copy of the same file would go stale and save over the first.
        private static bool BringForward<TView, TModel>(System.Action<TModel> goTo) where TModel : class
        {
            IReadOnlyList<Window> open = (global::Avalonia.Application.Current?.ApplicationLifetime
                        as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Windows;
            EditorHostWindow host = open?.OfType<EditorHostWindow>().FirstOrDefault(w => w.Content is TView);
            if (host == null) return false;
            if (host.WindowState == global::Avalonia.Controls.WindowState.Minimized) host.WindowState = global::Avalonia.Controls.WindowState.Normal;
            host.Activate();
            if ((host.Content as global::Avalonia.Controls.Control)?.DataContext is TModel vm) goTo(vm);
            return true;
        }

        /// <summary>The mugshots special trainer battles open with, and which classes get which intro and music.</summary>
        public static void OpenVsIntroEditor() => OpenVsIntroEditor(-1);

        /// <summary>Opens the VS intro editor on a trainer class's own intro (trainer class metadata patch).</summary>
        public static void OpenVsIntroEditor(int trainerClass) => _ = OpenIntroEditorAsync<VsIntroEditorView, VsIntroEditorViewModel>(
            VsIntroEditorViewModel.Title, "Reading the intro tables, their art and the trainer names.",
            () => new VsIntroEditorViewModel { StartClass = trainerClass }, vm => vm.Load(), vm => vm.Ready(), vm => new VsIntroEditorView(vm),
            1320, 780, vm => { if (trainerClass >= 0) vm.ShowClassRecord(trainerClass); }, minWidth: 1180);

        /// <summary>Which wild Pokémon get their own battle intro, and the music of the wild intros.</summary>
        public static void OpenWildIntroEditor() => _ = OpenIntroEditorAsync<WildIntroEditorView, WildIntroEditorViewModel>(
            WildIntroEditorViewModel.Title, "Reading the intro tables and the Pokémon names.",
            () => new WildIntroEditorViewModel(), vm => vm.Load(), vm => vm.Ready(), vm => new WildIntroEditorView(vm), 820, 620);

        // Both intro editors read the same tables, each one window at most, so neither edits a stale copy of its own bytes.
        private static async System.Threading.Tasks.Task OpenIntroEditorAsync<TView, TModel>(string title, string busyHint,
            System.Func<TModel> make, System.Action<TModel> load, System.Action<TModel> ready, System.Func<TModel, TView> view,
            double width, double height, System.Action<TModel> goTo = null, double minWidth = 0)
            where TView : global::Avalonia.Controls.Control where TModel : class
        {
            if (Refused(typeof(TView).Name)) return;
            if (BringForward<TView, TModel>(vm => goTo?.Invoke(vm))) return;
            if (VsIntroTables.WhyNot() is string why) { _ = DialogHelper.ShowInfo(why, title); return; }

            TModel vm = make();
            try
            {
                await RunBusyAsync("Opening " + title + "…", busyHint, () => load(vm));
                ready(vm);
            }
            catch (System.Exception ex) when (ex is System.IO.IOException || ex is System.IO.InvalidDataException
                                              || ex is System.InvalidOperationException || ex is System.UnauthorizedAccessException)
            {
                _ = DialogHelper.ShowError(title + " could not be opened:\n" + ex.Message, title);
                return;
            }
            // The list and the preview keep their widths, so a narrower window would squeeze the settings away.
            new EditorHostWindow(title, view(vm), width, height) { MinWidth = minWidth }.ShowManaged();
        }

        public static void OpenCameraEditor()
        {
            if (Refused("CameraEditorView")) return;
            new EditorHostWindow("Camera editor", new CameraEditorView(new CameraEditorViewModel(true))).ShowManaged();
        }

        public static void OpenTrainerEditor(int initialIndex = 0) => _ = OpenTrainerEditorAsync(initialIndex);

        public static async System.Threading.Tasks.Task OpenTrainerEditorAsync(int initialIndex = 0)
        {
            if (Refused("TrainerEditorView")) return;

            try
            {
                await RunBusyAsync("Opening Trainer Editor" + (initialIndex > 0 ? At(GetSimpleTrainerNames, initialIndex) : "") + "…", "Reading every trainer, party and trainer sprite.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties, DirNames.trainerParty, DirNames.trainerGraphics }));
                new TrainerEditorView(new TrainerEditorViewModel(true) { InitialIndex = initialIndex }).ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("Couldn't open the Trainer Editor: " + ex.Message, "Trainer Editor");
            }
        }

        public static void OpenTrainerSpriteEditor(int initialClassIndex = 0, System.Action closed = null) => _ = OpenTrainerSpriteEditorAsync(initialClassIndex, closed);

        public static async System.Threading.Tasks.Task OpenTrainerSpriteEditorAsync(int initialClassIndex = 0, System.Action closed = null)
        {
            if (Refused("TrainerSpriteEditorView")) return;

            try
            {
                await RunBusyAsync("Opening Trainer Sprite Editor…", "Reading the trainer class sprites.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerGraphics }));
                TrainerSpriteEditorView window = new TrainerSpriteEditorView(new TrainerSpriteEditorViewModel(initialClassIndex));
                if (closed != null) window.Closed += (_, _) => closed();
                window.ShowManaged();
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
            // It edits the trainer files directly; hg-engine rebuilds those from Trainers.c on every build.
            if (Refused("TrainerFlagBulkEditorView")) return;

            TrainerFlagBulkEditorViewModel vm = null;
            await RunBusyAsync("Opening Trainer Flag Bulk Editor…",
                "Reading every trainer's AI flags.",
                () =>
                {
                    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties });
                    vm = new TrainerFlagBulkEditorViewModel();
                });
            if (vm == null) return;

            new EditorHostWindow("Trainer flag bulk editor",
                new TrainerFlagBulkEditorView(vm), 1050, 700).ShowManaged();
        }

        public static void OpenVsSeekerRematchEditor(int initialRowIndex = -1) => _ = OpenVsSeekerRematchEditorAsync(initialRowIndex);

        public static async System.Threading.Tasks.Task OpenVsSeekerRematchEditorAsync(int initialRowIndex = -1)
        {
            if (Refused("VsSeekerRematchView")) return;

            VsSeekerRematchViewModel vm = null;
            await RunBusyAsync("Opening Vs. Seeker Rematch Editor…",
                "Reading the rematch table and trainer names.",
                () => vm = new VsSeekerRematchViewModel(initialRowIndex));
            if (vm == null) return;

            new EditorHostWindow("Vs. Seeker rematch editor",
                new VsSeekerRematchView(vm), 900, 600).ShowManaged();
        }

        public static void OpenHgEngineSettings()
        {
            if (Refused("HgEngineSettingsView")) return;
            new Views.Tools.HgEngineSettingsView(new ViewModels.Tools.HgEngineSettingsViewModel(load: true)).ShowManaged();
        }

        public static void OpenBattleTests()
        {
            if (!DSPRE.HgEngine.HgEngineDev.Enabled || Refused("BattleTestsView")) return;
            new Views.Tools.BattleTestsView(new ViewModels.Tools.BattleTestsViewModel(load: true)).ShowManaged();
        }

        public static void OpenHgEnginePatches()
        {
            if (Refused("HgEnginePatchesView")) return;

            new EditorHostWindow("hg-engine patches",
                new Views.Shell.HgEnginePatchesView(new HgEnginePatchesViewModel()), 1150, 700).ShowManaged();
        }

        public static void OpenPokegearRematchEditor(int initialRowIndex = -1) => _ = OpenPokegearRematchEditorAsync(initialRowIndex);

        public static async System.Threading.Tasks.Task OpenPokegearRematchEditorAsync(int initialRowIndex = -1)
        {
            if (Refused("PokegearRematchView")) return;

            PokegearRematchViewModel vm = null;
            await RunBusyAsync("Opening Pokégear Rematch Editor…",
                "Reading the rematch table, the phone book and trainer names.",
                () => vm = new PokegearRematchViewModel(initialRowIndex));
            if (vm == null) return;

            new EditorHostWindow("Pokégear rematch editor",
                new PokegearRematchView(vm), 950, 640).ShowManaged();
        }

        public static void OpenPokegearPhoneBook(int initialEntry = -1) => _ = OpenPokegearPhoneBookAsync(initialEntry);

        public static async System.Threading.Tasks.Task OpenPokegearPhoneBookAsync(int initialEntry = -1)
        {
            if (Refused("PokegearPhoneBookView")) return;

            try
            {
                PokegearPhoneBookViewModel vm = null;
                await RunBusyAsync("Opening Pokégear Phone Book…",
                    "Reading the phone book, contact names, trainers, maps and items.",
                    () =>
                    {
                        DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties });
                        vm = new PokegearPhoneBookViewModel(initialEntry);
                    });
                if (vm == null) return;

                new EditorHostWindow("Pokégear phone book", new PokegearPhoneBookView(vm), 1320, 700).ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("The Pokégear Phone Book could not be opened:" + System.Environment.NewLine + ex.Message, "Pokégear Phone Book");
            }
        }

        public static void OpenStarterEditor() => _ = OpenStarterEditorAsync();

        public static async System.Threading.Tasks.Task OpenStarterEditorAsync()
        {
            if (Refused("StarterEditorView")) return;

            try
            {
                await RunBusyAsync("Opening Starter Editor…", "Finding the starter script.",
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
            if (Refused("FlyEditorView")) return;
            if (RomInfo.FlyTableUnverified)
            {
                _ = DialogHelper.ShowError("The Fly / Warp Editor isn't checked against Japanese Pearl yet, so it stays closed rather than risk writing the wrong place.", "Fly / Warp Editor");
                return;
            }
            new FlyEditorView(HeaderLists.GetHeaderListBoxNames()).ShowManaged();
        }

        public static void OpenDungeonCutinEditor()
        {
            if (Refused("DungeonCutinEditorView")) return;
            new DungeonCutinEditorView(HeaderLists.GetHeaderListBoxNames()).ShowManaged();
        }

        public static void OpenTitleScreenEditor()
        {
            if (Refused("TitleScreenEditorView")) return;
            new TitleScreenEditorView().ShowManaged();
        }

        /// <summary>Lists every cell animation in the game so one can be picked and opened.</summary>
        public static void OpenCellAnimationPicker() => _ = OpenCellAnimationPickerAsync();

        public static async System.Threading.Tasks.Task OpenCellAnimationPickerAsync()
        {
            if (Refused("CellAnimationEditorView")) return;
            try
            {
                // Reading every archive to find the animations takes a moment, so the looking happens
                // behind the busy overlay. The window itself is built here, on the thread that owns it.
                CellAnimationPickerViewModel vm = new ViewModels.Graphics.CellAnimationPickerViewModel();
                await RunBusyAsync("Looking for animations…",
                                   "Reading every archive in the ROM.", vm.Gather);
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
            if (Refused("CellAnimationEditorView")) return;
            if (animation < 0)
            {
                _ = DialogHelper.ShowInfo("There is no animation file here to open.", "Cell Animation");
                return;
            }
            // A second window on the same file would go stale and save over the first.
            IReadOnlyList<Window> open = (global::Avalonia.Application.Current?.ApplicationLifetime
                        as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Windows;
            Views.Graphics.CellAnimationEditorView already = open?.OfType<Views.Graphics.CellAnimationEditorView>().FirstOrDefault(w =>
                w.DataContext is CellAnimationEditorViewModel v && v.Animation == animation
                && v.Source.Dir == source.Dir && v.Source.LoosePath == source.LoosePath);
            if (already != null)
            {
                if (already.WindowState == global::Avalonia.Controls.WindowState.Minimized) already.WindowState = global::Avalonia.Controls.WindowState.Normal;
                already.Activate();
                return;
            }

            try
            {
                CellAnimationEditorViewModel vm = new ViewModels.Graphics.CellAnimationEditorViewModel(
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
            if (Refused("BottomScreenEditorView")) return;

            // A second window on the same screen would leave two views of one thing, each able to edit it.
            // So an open one is brought forward and pointed at the application asked for instead.
            IReadOnlyList<Window> open = (global::Avalonia.Application.Current?.ApplicationLifetime
                        as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
                       ?.Windows;
            BottomScreenEditorView already = open == null ? null
                : System.Linq.Enumerable.FirstOrDefault(
                    System.Linq.Enumerable.OfType<BottomScreenEditorView>(open));
            if (already != null)
            {
                ShowPoketchApp(already, poketchApp);
                already.Activate();
                return;
            }

            BottomScreenEditorView view = new BottomScreenEditorView();
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

        /// <summary>The naming screen's icons, painted like trainer sprites, with the screen's top bar as a preview.</summary>
        public static void OpenNamingScreenEditor() => _ = OpenNamingScreenEditorAsync();

        public static async System.Threading.Tasks.Task OpenNamingScreenEditorAsync()
        {
            // It opens in the Trainer Sprite editor's window, so it is gated with it.
            if (Refused("NamingScreenEditor")) return;
            try
            {
                await RunBusyAsync("Opening Naming Screen Editor…", "Reading the naming screen's graphics.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.nameInputGraphics }));
                new TrainerSpriteEditorView(new TrainerSpriteEditorViewModel(48, TrainerSpriteSet.NamingIcons)).ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("Couldn't open the Naming Screen Editor: " + ex.Message, "Naming Screen Editor");
            }
        }

        public static void OpenTrainerCardEditor()
        {
            if (Refused("TrainerCardEditorView")) return;
            new TrainerCardEditorView().ShowManaged();
        }

        /// <summary>Opens one particle file for editing; <paramref name="changed"/> hears which entry was saved.</summary>
        public static void OpenParticleEditor(DirNames archive, int entry, string what, System.Action<int> changed,
                                              bool orthographic = false)
        {
            if (Refused("ParticleEditorView")) return;
            if (gameDirs.ContainsKey(archive)) DSUtils.TryUnpackNarcs(new List<DirNames> { archive });
            OpenParticleEditor(Data.ArchiveFiles.Mapped(archive), entry, what, changed, orthographic);
        }

        /// <summary>The same, for any archive, including ones DSPRE does not map, which are saved in place.</summary>
        public static void OpenParticleEditor(Data.ArchiveFiles source, int entry, string what, System.Action<int> changed,
                                              bool orthographic = false)
        {
            if (Refused("ParticleEditorView")) return;
            // A second window on the same file would go stale and save over the first.
            IReadOnlyList<Window> open = (global::Avalonia.Application.Current?.ApplicationLifetime
                        as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Windows;
            Views.Graphics.ParticleEditorView already = open?.OfType<Views.Graphics.ParticleEditorView>().FirstOrDefault(w =>
                w.DataContext is ParticleEditorViewModel v && v.Entry == entry
                && v.Source.Dir == source.Dir && v.Source.LoosePath == source.LoosePath);
            if (already != null)
            {
                if (already.WindowState == global::Avalonia.Controls.WindowState.Minimized) already.WindowState = global::Avalonia.Controls.WindowState.Normal;
                already.Activate();
                return;
            }
            try
            {
                ParticleEditorViewModel vm = new ViewModels.Graphics.ParticleEditorViewModel(source, entry, what, changed, orthographic);
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
            if (Refused("ParticleLibraryView")) return;
            try
            {
                ParticleLibraryViewModel vm = new ViewModels.Graphics.ParticleLibraryViewModel();
                await RunBusyAsync("Looking for particles…", "Reading every archive in the ROM.", () =>
                {
                    DSUtils.TryUnpackNarcs(new List<DirNames> {
                        DirNames.wazaParticle, DirNames.ballParticles, DirNames.wazaEffectScripts, DirNames.wazaEffectSub,
                        DirNames.encounterEffectGraphics }
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
            if (Refused("BallCapsuleEditorView")) return;
            Dictionary<int, List<string>> usedBy = null;
            try
            {
                await RunBusyAsync("Opening Ball Capsules…", "Reading the seals and their effects.", () =>
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
                BallCapsuleEditorViewModel vm = new ViewModels.Graphics.BallCapsuleEditorViewModel(names) { UsedBy = usedBy };
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
            if (Refused("BannerEditorView")) return;
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
            if (Refused("SpawnEditorView")) return;
            new SpawnEditorView(new SpawnEditorViewModel(HeaderLists.GetHeaderListBoxNames())).ShowManaged();
        }

        public static void OpenHeaderSearch()
        {
            if (!IsRomLoaded) return;
            new HeaderSearchView(new HeaderSearchViewModel(true)).ShowManaged();
        }

        public static void OpenMapEditor(int mapIndex = -1)
        {
            if (!IsRomLoaded) return;
            MapEditorViewModel vm = new MapEditorViewModel(true) { InitialMapIndex = mapIndex };
            EditorHostWindow window = new EditorHostWindow("Map editor", new MapEditorView(vm), 1200, 720);
            window.Closed += (_, _) => vm.Detach();
            window.ShowManaged();
        }

        public static void OpenBuildingEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            new BuildingEditorView(new BuildingEditorViewModel(true) { InitialIndex = initialIndex }).ShowManaged();
        }

        public static void OpenMatrixEditor(int initialIndex = 0, int focusHeader = -1)
        {
            if (!IsRomLoaded) return;
            if (BringForward<MatrixEditorView, MatrixEditorViewModel>(vm => { vm.SelectedMatrixIndex = initialIndex; vm.FocusHeader = focusHeader; })) return;
            MatrixEditorViewModel model = new MatrixEditorViewModel(true) { InitialIndex = initialIndex, FocusHeader = focusHeader };
            EditorHostWindow window = new EditorHostWindow("Matrix editor", new MatrixEditorView(model), 860, 640);
            window.Closed += (_, _) => model.Detach();
            window.ShowManaged();
        }

        public static void OpenEventEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            if (BringForward<EventEditorView, EventEditorViewModel>(vm => vm.SelectedEventIndex = initialIndex)) return;
            EventEditorViewModel model = new EventEditorViewModel(true) { InitialIndex = initialIndex };
            EditorHostWindow window = new EditorHostWindow("Event editor", new EventEditorView(model), 1200, 720);
            window.Closed += (_, _) => model.DetachSaves();
            window.ShowManaged();
        }

        public static void OpenEventEditorWithOverworld(int eventFileId, int owIndex)
        {
            if (!IsRomLoaded) return;
            if (BringForward<EventEditorView, EventEditorViewModel>(vm => vm.GoToOverworld(eventFileId, owIndex))) return;
            EventEditorViewModel model = new EventEditorViewModel(true) { InitialIndex = eventFileId, InitialOverworldIndex = owIndex };
            EditorHostWindow window = new EditorHostWindow("Event editor", new EventEditorView(model), 1200, 720);
            window.Closed += (_, _) => model.DetachSaves();
            window.ShowManaged();
        }

        public static void OpenAreaDataEditor(int initialIndex = 0)
        {
            if (!IsRomLoaded) return;
            AreaDataEditorViewModel model = new AreaDataEditorViewModel(true) { InitialIndex = initialIndex };
            EditorHostWindow window = new EditorHostWindow("Area data editor", new AreaDataEditorView(model), 520, 380);
            window.Closed += (_, _) => model.Detach();
            window.ShowManaged();
        }

        public static void OpenNsbtxEditor() => OpenNsbtxEditor(false, -1);

        /// <summary>Opens the texture editor on one map or building texture pack.</summary>
        public static void OpenNsbtxEditor(bool buildings, int pack)
        {
            if (!IsRomLoaded) return;
            NsbtxEditorViewModel model = new(true);
            if (pack >= 0) model.OpenAt(buildings, pack);
            new NsbtxEditorView(model).ShowManaged();
        }

        public static void OpenOverlayEditor()
        {
            if (Refused("OverlayEditorView")) return;
            if (BringForwardWindow<OverlayEditorView>()) return;
            new OverlayEditorView().ShowManaged();
        }

        public static void OpenOverworldEditor() => _ = OpenOverworldEditorAsync();

        public static async System.Threading.Tasks.Task OpenOverworldEditorAsync()
        {
            if (Refused("BtxEditorView")) return;

            try
            {
                await RunBusyAsync("Opening Overworld Editor…", "Reading every overworld sprite.",
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

        /// <summary>
        /// Deliberately not gated on a linked checkout: this is how someone with only the ROM sees what
        /// the game reads out of it, which is the case the gate leaves with nothing to look at.
        /// </summary>
        public static void OpenHgeRomReview() => _ = OpenHgeRomReviewAsync();

        public static async System.Threading.Tasks.Task OpenHgeRomReviewAsync()
        {
            if (Refused("HgeRomReviewView")) return;
            try
            {
                await RunBusyAsync("Opening hg-engine ROM review…", "Reading the icons and the a/0/2/8 tables.", HgeRomReviewViewModel.Unpack);
                await System.Threading.Tasks.Task.Yield();
                new HgeRomReviewView(new HgeRomReviewViewModel()).ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("The hg-engine ROM review could not be opened:" + System.Environment.NewLine + ex.Message, "hg-engine ROM review");
            }
        }

        public static void OpenDistortionWorldEditor() => _ = OpenDistortionWorldEditorAsync();

        public static async System.Threading.Tasks.Task OpenDistortionWorldEditorAsync()
        {
            if (Refused("DistortionWorldView")) return;
            try
            {
                await RunBusyAsync("Opening Distortion World…", "Reading the floors, their maps, props and events.", DistortionWorldViewModel.Unpack);
                await System.Threading.Tasks.Task.Yield();
                DistortionWorldViewModel vm = new ViewModels.World.DistortionWorldViewModel();
                if (!vm.Available)
                {
                    AppMessages.Info("This ROM has no Distortion World data to edit.", "Distortion World");
                    return;
                }
                new Views.World.DistortionWorldView(vm).ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowError("The Distortion World editor could not be opened:" + System.Environment.NewLine + ex.Message, "Distortion World");
            }
        }

        public static void OpenCharMapManager()
        {
            if (!IsRomLoaded) return;
            new CharMapManagerView().ShowManaged();
        }

        public static void OpenSettings()
        {
            // Settings do not require a loaded ROM.
            if (BringForwardWindow<SettingsWindowView>()) return;
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
            if (Refused("ProjectChecksView")) return;
            new ProjectChecksView().ShowManaged();
        }

        public static void OpenPatchToolbox()
        {
            // Writes to the ROM binary (ARM9 / overlays / NARCs). Native Avalonia UI over the shared
            // PatchToolboxDialog apply-logic, so it runs identical code to the WinForms dialog.
            if (Refused("PatchToolboxView")) return;
            if (BringForwardWindow<PatchToolboxView>()) return;
            new PatchToolboxView().ShowManaged();
        }

        public static void OpenCustomCommandManager()
        {
            // Manages the custom script-command databases. Still a WinForms tool (self-contained, file-based);
            // reused directly over the shared Win32 pump until a native port exists.
            if (!IsRomLoaded) return;
            new CustomScrcmdManagerView(new CustomScrcmdManagerViewModel(true)).ShowManaged();
        }

        public static void OpenScriptCommandDatabase()
        {
            if (!IsRomLoaded) return;
            new ScriptCommandGuideView(ScriptCommandGuideViewModel.ForEventScripts()).ShowManaged();
        }

        // ── Command palette (quick-open) ────────────────────────────────────────
        /// <summary>Opens the Ctrl+P quick-open palette over the given window.</summary>
        public static void OpenCommandPalette(global::Avalonia.Controls.Window owner)
        {
            CommandPaletteViewModel vm = new CommandPaletteViewModel(PaletteCommands(), DynamicCommands);
            CommandPaletteView view = new CommandPaletteView(vm);
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
            Match m = System.Text.RegularExpressions.Regex.Match(query, @"\d+");
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
                ($"Go to Headbutt file #{n}",  "headbutt tree",                () => OpenSpecialEncountersEditor(n)),
                ($"Go to Event file #{n}",     "event warp trigger overworld", () => OpenEventEditor(n)),
                ($"Go to Script #{n}",         "script",                       () => OpenScriptEditor(n)),
                ($"Go to Level Script #{n}",   "level script",                 () => OpenLevelScriptEditor(n)),
                ($"Go to Text archive #{n}",   "text string message archive",  () => OpenTextEditor(n)),
                ($"Go to Matrix #{n}",         "matrix world grid",            () => OpenMatrixEditor(n)),
                ($"Go to Area Data #{n}",      "area data tileset",            () => OpenAreaDataEditor(n)),
                ($"Go to Wild encounters #{n}","wild encounter grass surf",    () => OpenWildEditor(n)),
            };

            foreach ((string label, string keywords, System.Action run) in jumps)
                if (rest.Length == 0
                    || label.Contains(rest, System.StringComparison.OrdinalIgnoreCase)
                    || keywords.Contains(rest, System.StringComparison.OrdinalIgnoreCase))
                    yield return new CommandItem { Name = label, Run = run };
        }

        /// <summary>Opens the one place that lists every 2D graphic in the game.</summary>
        public static void OpenGraphicsBrowser() => _ = OpenGraphicsBrowserAsync();

        public static async System.Threading.Tasks.Task OpenGraphicsBrowserAsync()
        {
            if (Refused("GraphicsBrowserView")) return;

            try
            {
                GraphicsBrowserViewModel vm = new ViewModels.Graphics.GraphicsBrowserViewModel(loadImmediately: false);
                await RunBusyAsync("Opening Graphics…", "Finding every picture in the ROM.", vm.Scan);
                vm.Publish();
                new Views.Graphics.GraphicsBrowserView(vm).ShowManaged();
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenGraphicsBrowser failed: " + ex.Message);
                await DialogHelper.ShowInfo("The graphics list could not be opened. Open a ROM first.", "Graphics");
            }
        }

        /// <summary>
        /// Opens the Pokédex pages put together with a sample Pokémon, or, where that editor is not on, the
        /// graphics window at the Pokédex's first screen.
        /// </summary>
        public static void OpenPokedexGraphics()
        {
            if (EditorAvailability.Allows("PokedexGraphicsEditorView"))
            {
                Views.Graphics.PokedexGraphicsEditorView open = System.Linq.Enumerable.FirstOrDefault(
                    System.Linq.Enumerable.OfType<Views.Graphics.PokedexGraphicsEditorView>(
                        (global::Avalonia.Application.Current?.ApplicationLifetime
                         as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Windows
                        ?? new System.Collections.Generic.List<global::Avalonia.Controls.Window>()));
                if (open != null) { open.Activate(); return; }
                new Views.Graphics.PokedexGraphicsEditorView().ShowManaged();
                return;
            }
            int first = -1;
            foreach (ScreenGraphicsLayouts.Entry e in Data.ScreenGraphicsLayouts.For(RomInfo.DirNames.pokedexGraphics))
                if (e.Kind == "NSCR" && e.Drawing >= 0) { first = e.Index; break; }
            OpenGraphicAt(RomInfo.DirNames.pokedexGraphics, System.Math.Max(0, first), preferAssembled: true);
        }

        /// <summary>Opens the graphics window already looking at one file.</summary>
        public static void OpenGraphicAt(RomInfo.DirNames archive, int fileIndex, bool preferAssembled = false)
            => _ = OpenGraphicAtAsync(archive, fileIndex, preferAssembled);

        private static async System.Threading.Tasks.Task OpenGraphicAtAsync(RomInfo.DirNames archive, int fileIndex, bool preferAssembled)
        {
            if (Refused("GraphicsBrowserView")) return;

            try
            {
                GraphicAssets.Archive a = Data.GraphicAssets.All.FirstOrDefault(x => x.Dir == archive);
                if (a == null)
                {
                    await DialogHelper.ShowInfo("That kind of graphic is not one this window lists yet.", "Graphics");
                    return;
                }

                GraphicsBrowserViewModel vm = new ViewModels.Graphics.GraphicsBrowserViewModel(loadImmediately: false);
                await RunBusyAsync("Opening Graphics…", "Finding every picture in the ROM.", vm.Scan);
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
            if (Refused("TrainerBackSpriteEditor")) return;

            try
            {
                await RunBusyAsync("Opening Trainer Back Sprite Editor…", "Reading the player back sprites.",
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
                        PokemonIconFiles.Icon icon = DSPRE.ROMFiles.PokemonIconFiles.Describe(fileIndex);
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

                case RomInfo.DirNames.encounterEffectGraphics:
                    return ("VS Intro Editor", OpenVsIntroEditor);

                default:
                    return null;
            }
        }

        /// <summary>The first item that uses a drawing, since several can share one.</summary>
        private static int ItemUsingDrawing(int fileIndex)
        {
            try
            {
                string[] names = RomInfo.GetItemNames();
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
            if (Refused("TilesetBuilderView")) return;

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
            if (Refused("FontEditorView")) return;

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
            if (Refused("BattleScreenEditorView")) return;

            try
            {
                await RunBusyAsync("Opening Battle Screen…", "Reading battle backgrounds, gauges, text boxes and fonts.",
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
            if (Refused("BattleSceneBrowserView")) return;

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
            if (Refused("ModelBrowserView")) return;

            try
            {
                // Listing means reading every 3D archive to see what is in it, which is far too much
                // file work to do on the click.
                ModelBrowserViewModel vm = new ViewModels.Graphics.ModelBrowserViewModel();
                await RunBusyAsync("Opening Models…", "Finding every model and texture in the ROM.", vm.Scan);
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
        // Beta editors are left out, as in the menus.
        public static IEnumerable<CommandItem> PaletteCommands() => BuildCommands().Where(c => c.Beta == null || BetaEditors.Allows(c.Beta));

        public static List<CommandItem> BuildCommands() => new()
        {
            new() { Name = "All graphics",          Keywords = "sprite picture image texture palette colour color icon font paint draw", Run = OpenGraphicsBrowser },
            new() { Name = "Pokédex graphics",      Keywords = "pokedex zukan dex screen background graphics", Run = OpenPokedexGraphics },
            new() { Name = "All models and textures", Keywords = "3d model nsbmd nsbtx building overworld map mesh", Run = OpenModelBrowser },
            new() { Beta = "BattleScreenEditorView", Name = "Battle screen",         Keywords = "battle screen gauge hp bar backdrop platform message box touch command", Run = OpenBattleScreenEditor },
            new() { Beta = "BattleSceneBrowserView", Name = "Battle scenes",         Keywords = "battle scene backdrop terrain platform ground", Run = OpenBattleSceneBrowser },
            new() { Beta = "TilesetBuilderView", Name = "Picture to background", Keywords = "png tiles tilemap palette background", Run = OpenTilesetBuilder },
            new() { Beta = "TitleScreenEditorView", Name = "Title screen editor",   Keywords = "logo copyright intro hgss", Run = OpenTitleScreenEditor },
            new() { Beta = "BottomScreenEditorView", Name = "Bottom screen",         Keywords = "touch menu poketch pokétch bottom screen field panel icons poke ball", Run = () => OpenBottomScreenEditor() },
            new() { Beta = "CellAnimationEditorView", Name = "Cell animations",       Keywords = "nanr animation frames cell sprite sequence playback timing", Run = OpenCellAnimationPicker },
            new() { Beta = "DungeonCutinEditorView", Name = "Dungeon cutin editor",  Keywords = "dungeon location splash hgss", Run = OpenDungeonCutinEditor },
            new() { Beta = "TrainerSpriteEditorView", Name = "Naming screen", Keywords = "name entry player rival box icon keyboard", Run = OpenNamingScreenEditor },
            new() { Beta = "TrainerCardEditorView", Name = "Trainer card editor",   Keywords = "rank front back graphics", Run = OpenTrainerCardEditor },
            new() { Beta = "ParticleLibraryView", Name = "Particles",             Keywords = "spa particle emitter effect move animation seal burst sparkle", Run = OpenParticleLibrary },
            new() { Beta = "BallCapsuleEditorView", Name = "Ball Capsules",         Keywords = "seal sticker capsule poke ball send out particles effect", Run = OpenBallCapsuleEditor },
            new() { Beta = "AudioEditorView", Name = "Audio editor",          Keywords = "sound cry cries music bgm fanfare sfx song", Run = () => { _ = OpenAudioEditorAsync(); } },
            new() { Name = "Pokémon editor",        Keywords = "species personal learnset evolution sprite", Run = () => { _ = OpenPokemonEditorAsync(); } },
            new() { Beta = "WildIntroEditorView", Name = "Wild Pokémon intro editor", Keywords = "legendary wild battle intro music transition", Run = OpenWildIntroEditor },
            new() { Beta = "HgEngineFormEditorView", Name = "Form editor (hg-engine)", Keywords = "mega regional alolan galarian gmax gigantamax primal reversion form", Run = OpenHgEngineFormEditor },
            new() { Beta = "MoveBackgroundEditorView", Name = "Move backgrounds", Keywords = "move animation background surf psychic haikei scroll effect battle bg", Run = OpenMoveBackgroundEditor },
            new() { Beta = "AbilityFlagsEditorView", Name = "Ability flags (hg-engine)", Keywords = "ability mold breaker neutralizing gas trace skill swap role play entrainment receiver", Run = OpenAbilityFlagsEditor },
            new() { Name = "Move data editor",      Keywords = "attack",   Run = () => OpenMoveDataEditor() },
            new() { Name = "TM / HM editor",        Keywords = "machine",  Run = () => OpenTMEditor() },
            new() { Name = "TM/HM bulk editor",     Keywords = "machine compatibility bulk family sync copy", Run = OpenTmHmBulkEditor },
            new() { Beta = "GrowthCurveEditorView", Name = "Growth curves",         Keywords = "exp experience level growth rate curve", Run = OpenGrowthCurves },
            new() { Beta = "FriendshipChangesView", Name = "Friendship changes",    Keywords = "friendship happiness walking level up faint soothe bell luxury", Run = OpenFriendshipChanges },
            new() { Beta = "EncounterSlotOddsView", Name = "Encounter slot odds",   Keywords = "encounter slot odds chance percent wild rate fishing surf headbutt rock smash", Run = OpenEncounterSlotOdds },
            new() { Beta = "BreedingItemsView", Name = "Breeding items",        Keywords = "incense baby egg hatch breeding wynaut azurill munchlax", Run = OpenBreedingItems },
            new() { Beta = "TypeChartEditorView", Name = "Type chart",            Keywords = "type effectiveness matchup super effective resist immune weakness", Run = OpenTypeChart },
            new() { Beta = "MoveTutorEditorView", Name = "Move tutors",           Keywords = "tutor tutors shards bp teach move compatibility", Run = OpenMoveTutors },
            new() { Name = "Egg move editor",       Keywords = "breeding", Run = OpenEggMoveEditor },
            new() { Beta = "BattleScriptEditorView", Name = "Move animations & battle scripts", Keywords = "battle script editor move sequence waza be_seq sub_seq effect animation", Run = () => OpenBattleScriptEditor() },
            new() { Name = "Item editor",           Run = () => OpenItemEditor() },
            new() { Beta = "BerryDataEditorView", Name = "Berry data",            Keywords = "berry berries firmness flavour flavor growth yield poffin", Run = OpenBerryData },
            new() { Beta = "BpShopEditorView", Name = "Battle Point shop",     Keywords = "battle point bp shop exchange frontier tower tm prize", Run = OpenBpShop },
            new() { Beta = "UndergroundMiningView", Name = "Underground mining",    Keywords = "underground mining dig treasure sphere fossil plate wall", Run = OpenUndergroundMining },
            new() { Beta = "MartEditorView", Name = "Mart editor",           Keywords = "shop store inventory stock poke mart", Run = OpenMartEditor },
            new() { Name = "Item tables (pickup, hidden, Rock Smash)", Keywords = "pickup hidden ground rock smash item table hgss", Run = OpenItemTableEditor },
            new() { Name = "Trade editor",          Keywords = "in-game",  Run = () => OpenTradeEditor() },
            new() { Name = "Starter Pokémon editor", Keywords = "turtwig chimchar piplup chikorita cyndaquil totodile rival professor", Run = OpenStarterEditor },
            new() { Name = "Trainer editor",        Keywords = "battle party", Run = () => OpenTrainerEditor() },
            new() { Beta = "VsIntroEditorView", Name = "VS intro editor",       Keywords = "vs mugshot cut-in gym leader elite four battle intro music transition class", Run = OpenVsIntroEditor },
            new() { Beta = "TrainerSpriteEditorView", Name = "Trainer sprite editor", Keywords = "class pixel paint", Run = () => OpenTrainerSpriteEditor() },
            new() { Beta = "TrainerSpriteEditorView", Name = "Trainer back sprite editor", Keywords = "player back sprite throw palette animation", Run = () => OpenTrainerBackSpriteEditor() },
            new() { Name = "Vs. Seeker rematch editor", Keywords = "rematch trainer encounter chain", Run = () => OpenVsSeekerRematchEditor() },
            new() { Name = "Pokégear rematch editor", Keywords = "rematch trainer phone pokegear call hgss", Run = () => OpenPokegearRematchEditor() },
            new() { Beta = "PokegearPhoneBookView", Name = "Pokégear phone book", Keywords = "phone contact number call gift greeting pokegear hgss", Run = () => OpenPokegearPhoneBook() },
            new() { Name = "hg-engine patches", Keywords = "hook bytereplacement repoint arm9 overlay patch asm", Run = OpenHgEnginePatches },
            new() { Name = "hg-engine settings", Keywords = "config.h config.s define toggle option fairy hidden abilities mega", Run = OpenHgEngineSettings },
            new() { Name = "Trainer flag bulk editor", Keywords = "ai double battle bulk", Run = OpenTrainerFlagBulkEditor },
            new() { Name = "Text editor",           Keywords = "string archive message", Run = () => OpenTextEditor() },
            new() { Name = "Script editor",         Run = () => OpenScriptEditor() },
            new() { Name = "Level script editor",   Run = () => OpenLevelScriptEditor() },
            new() { Name = "Music & battle tables", Keywords = "table conditional music battle effects combo vs poster", Run = OpenTableEditor },
            new() { Name = "Header editor",         Keywords = "map header", Run = () => OpenHeaderEditor() },
            new() { Name = "Camera editor",         Keywords = "angle map header", Run = OpenCameraEditor },
            new() { Name = "Map editor",            Keywords = "3d model buildings", Run = () => OpenMapEditor() },
            new() { Name = "Building editor",       Run = () => OpenBuildingEditor() },
            new() { Name = "Matrix editor",         Keywords = "world grid", Run = () => OpenMatrixEditor() },
            new() { Name = "Event editor",          Keywords = "overworld warp trigger spawn", Run = () => OpenEventEditor() },
            new() { Name = "Fly / warp editor",     Run = OpenFlyWarpEditor },
            new() { Name = "Spawn point editor",    Keywords = "start position new game", Run = OpenSpawnEditor },
            new() { Name = "Advanced header search", Keywords = "find filter query field", Run = OpenHeaderSearch },
            new() { Name = "Overlay editor",        Run = OpenOverlayEditor },
            new() { Name = "Overworld editor",      Keywords = "overworld sprites btx npc", Run = OpenOverworldEditor },
            new() { Name = "Area data editor",      Keywords = "tileset", Run = () => OpenAreaDataEditor() },
            new() { Name = "Map & building textures", Keywords = "texture nsbtx tileset", Run = OpenNsbtxEditor },
            new() { Name = "Wild Pokémon editor",   Keywords = "encounter grass surf", Run = () => OpenWildEditor() },
            new() { Beta = "WildHeldItemOddsView", Name = "Wild held items",       Keywords = "held item chance odds compound eyes wild", Run = OpenWildHeldItems },
            new() { Name = "Special encounters editor", Keywords = "headbutt tree bug contest opponents great marsh honey safari trophy garden daily swarm", Run = () => OpenSpecialEncountersEditor() },
            new() { Name = "Battle Tower editor",   Keywords = "tower trainer set party rental", Run = OpenBattleTowerEditor },
            new() { Name = "Address helper",        Run = OpenAddressHelper },
            new() { Name = "Research helper",       Run = OpenResearchHelper },
            new() { Beta = "HgeRomReviewView", Name = "hg-engine ROM review",  Keywords = "hge binary icons sprites palettes archive", Run = OpenHgeRomReview },
            new() { Beta = "DistortionWorldView", Name = "Distortion World",      Keywords = "giratina platinum gravity platforms torn world", Run = OpenDistortionWorldEditor },
            new() { Name = "Char map manager",      Keywords = "text encoding", Run = OpenCharMapManager },
            new() { Name = "Custom script command manager", Keywords = "scrcmd script commands database", Run = OpenCustomCommandManager },
            new() { Beta = "FontEditorView", Name = "Font editor",           Keywords = "font letter glyph character typeface text", Run = OpenFontEditor },
            new() { Beta = "BannerEditorView", Name = "Game icon & banner",    Keywords = "rom icon ds menu title", Run = () => { _ = OpenBannerEditorAsync(); } },
            new() { Name = "Edit dropdown labels",  Keywords = "enum custom", Run = OpenLabelEditor },
            new() { Beta = "ProjectChecksView", Name = "Validation & Where-Used", Keywords = "check broken references project health", Run = OpenProjectChecks },
            new() { Name = "Settings",              Run = OpenSettings },
            // ── Actions (not editors) ──
            new() { Name = "Toggle theme (dark / light)", Keywords = "dark light appearance", Run = ThemeManager.Toggle },
        };
    }
}
