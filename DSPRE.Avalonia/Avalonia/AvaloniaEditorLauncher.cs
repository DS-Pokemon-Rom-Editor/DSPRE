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
    /// <summary>Opens every editor. Each launcher does nothing when no ROM is loaded, so menu items needn't check.</summary>
    public static class AvaloniaEditorLauncher
    {
        /// <summary>True once a ROM has been opened and <see cref="RomInfo"/> populated.</summary>
        public static bool IsRomLoaded => GameFamily != GameFamilies.NULL;

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

        /// <summary>" at NAME" for a busy title when an editor opens on a particular record, or nothing.</summary>
        private static string At(System.Func<string[]> names, int index)
        {
            try
            {
                string[] all = names();
                return index >= 0 && index < all.Length && !string.IsNullOrWhiteSpace(all[index]) ? " at " + all[index].Trim() : "";
            }
            catch (System.Exception ex) { AppLogger.WarnOnce("AvaloniaEditorLauncher.At", ex); return ""; }
        }

        /// <summary>Runs plain file work off the UI thread behind the busy card; no UI or bitmap work.</summary>
        private static System.Threading.Tasks.Task RunBusyAsync(string busyText, string busyHint, System.Action work)
        => BusyOverlay.RunAsync(busyText, busyHint, work);

        /// <summary>
        /// Runs <paramref name="work"/> behind the busy card, then builds the window while the card is still up, so
        /// the editor opens ready instead of freezing after the card goes. A null window means nothing to show.
        /// </summary>
        private static async System.Threading.Tasks.Task OpenBehindCardAsync(string busyText, string busyHint, System.Action work,
                                                                               System.Func<global::Avalonia.Controls.Window> build)
        {
            global::Avalonia.Controls.Window window = null;
            await BusyOverlay.RunAsync(busyText, busyHint, work, () => window = build());
            window?.ShowManaged();
        }

        /// <summary>Every sound in the ROM, named from the sound archive.</summary>
        /// <param name="showCryFor">A species to open on, for the Pokémon editor's Edit cry button; 0 for none.</param>
        public static async System.Threading.Tasks.Task OpenAudioEditorAsync(int showCryFor = 0)
        {
            if (Refused("AudioEditorView")) return;

            try
            {
                // The species list is counted from the personal data, which a fresh project has not unpacked yet.
                await OpenBehindCardAsync("Opening Audio editor…", "Reading the cries, music and sound effects.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.PersonalPokeData }),
                    () =>
                    {
                        string[] names;
                        try { names = GetPokemonNamesWithForms(GetPersonalFilesCount()); }
                        catch (System.Exception ex) { AppLogger.WarnOnce("AvaloniaEditorLauncher.OpenAudioEditorAsync", ex); names = System.Array.Empty<string>(); }
                        AudioEditorViewModel vm = new ViewModels.Audio.AudioEditorViewModel(names);
                        if (showCryFor > 0) vm.ShowCryFor(showCryFor);
                        return new Views.Audio.AudioEditorView(vm);
                    });
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("Couldn't open the Audio editor: " + ex.Message, "Audio editor");
            }
        }

        // ── Pokémon-related editors ────────────────────────────────────────────
        public static void OpenPokemonEditor(int species) => _ = OpenPokemonEditorAsync(species);

        public static async System.Threading.Tasks.Task OpenPokemonEditorAsync(int initialMon = 1)
        {
            if (Refused("PokemonEditorView")) return;

            try
            {
                PokemonEditorView view = null;
                // Every tab reads its archives up front, and the window is built while the card is still up, so the
                // editor is smooth once it shows.
                await BusyOverlay.RunAsync("Opening Pokémon editor" + (initialMon != 1 ? At(GetPokemonNames, initialMon) : "") + "…",
                    "Reading species data, learnsets, evolutions, the Pokédex and sprites.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> {
                        DirNames.PersonalPokeData, DirNames.Learnsets, DirNames.Evolutions, DirNames.MonIcons,
                        DirNames.PokedexAreas, DirNames.PokedexData, DirNames.PokedexDataAltered, DirNames.FootprintGraphics,
                        DirNames.PokedexSearchSteps, DirNames.RegionalDexSpecies, DirNames.RegionalDexNumbers,
                        DirNames.PokemonBattleSprites, DirNames.OtherPokemonBattleSprites, DirNames.PokeHeight, DirNames.PokeHeightForms,
                        DirNames.PokemonSpriteOffsets, DirNames.PokeYofs, DirNames.PokeShadowOfx, DirNames.PokeShadow,
                        DirNames.PokeAnim, DirNames.PokeAnimDefs, DirNames.BattleBg, DirNames.BattleObj, DirNames.WindowFrames }),
                    () =>
                    {
                        SetMonIconsPalTableAddress();

                        // Full Pokémon name list (base + alt forms this ROM actually has data for + extras)
                        string[] fullList = GetPokemonNamesWithForms(GetPersonalFilesCount());
                        string[] moveNames = GetAttackNames();

                        int mon = System.Math.Clamp(initialMon, 0, System.Math.Max(0, fullList.Length - 1));
                        PokemonEditorViewModel vm = new PokemonEditorViewModel(fullList, moveNames, initialMon: mon);
                        view = new PokemonEditorView(vm);
                    });
                view?.ShowManaged();
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("Couldn't open the Pokémon editor: " + ex.Message, "Pokémon editor");
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
                await OpenBehindCardAsync("Opening Move data editor" + (initialIndex > 0 ? At(GetAttackNames, initialIndex) : "") + "…", "Reading every move.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.MoveData }),
                    () =>
                    {
                        MoveDataEditorView view = new MoveDataEditorView();
                        if (initialIndex > 0 && view.DataContext is MoveDataEditorViewModel vm)
                            vm.SelectedMoveIndex = initialIndex;   // setter clamps + loads
                        return view;
                    });
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("Couldn't open the Move data editor: " + ex.Message, "Move data editor");
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

        /// <summary>Opens on move animations by default. The Move editor passes archive 0 (script) or 3 (animation) and the move number.</summary>
        public static void OpenBattleScriptEditor(int archive = 3, int entryIndex = 1)
        {
            // Refused too while hg-engine builds the particle archive, which has no source view yet.
            if (Refused("BattleScriptEditorView")) return;
            BattleScriptEditorViewModel vm = new BattleScriptEditorViewModel();
            BattleScriptEditorView view = new BattleScriptEditorView { DataContext = vm };
            if (vm.IsAvailable)
            {
                vm.ArchiveIndex = System.Math.Clamp(archive, 0, 4);
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
                await OpenBehindCardAsync("Opening Item editor" + (initialIndex > 1 ? At(GetItemNames, initialIndex) : "") + "…", "Reading every item and its icon.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.ItemData, DirNames.ItemIcons }),
                    () =>
                    {
                        ItemEditorViewModel vm = new ItemEditorViewModel(GetItemNames());
                        if (initialIndex > 0) vm.SelectedItemIndex = System.Math.Clamp(initialIndex, 0, vm.MaxItemIndex);
                        return new ItemEditorView(vm);
                    });
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("Couldn't open the Item editor: " + ex.Message, "Item editor");
            }
        }

        public static void OpenItemTableEditor()
        {
            if (Refused("ItemTableEditorView")) return;
            new ItemTableEditorView(new ItemTableEditorViewModel(GetItemNames(), HeaderLabels.Friendly())).ShowManaged();
        }

        /// <summary>Opens the item tables on the hidden item a map event's script picks up.</summary>
        public static void OpenItemTableEditorAtHiddenItem(int script)
        {
            if (Refused("ItemTableEditorView")) return;
            ItemTableEditorView view = (global::Avalonia.Application.Current?.ApplicationLifetime
                    as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Windows
                .OfType<ItemTableEditorView>().FirstOrDefault();
            if (view == null)
            {
                view = new ItemTableEditorView(new ItemTableEditorViewModel(GetItemNames(), HeaderLabels.Friendly()));
                view.ShowManaged();
            }
            else
            {
                if (view.WindowState == global::Avalonia.Controls.WindowState.Minimized) view.WindowState = global::Avalonia.Controls.WindowState.Normal;
                view.Activate();
            }
            view.ShowHiddenItem(script);
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
                _ = DialogHelper.ShowErrorAsync("Couldn't open the Mart editor: " + ex.Message,
                    "Mart editor");
            }
        }

        public static void OpenTradeEditor(int initialIndex = 0) => _ = OpenTradeEditorAsync(initialIndex);

        public static async System.Threading.Tasks.Task OpenTradeEditorAsync(int initialIndex = 0)
        {
            if (Refused("TradeEditorView")) return;

            try
            {
                await OpenBehindCardAsync("Opening Trade editor…", "Reading the in-game trades.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.TradeData }),
                    () =>
                    {
                        TradeEditorView view = new TradeEditorView();
                        if (initialIndex > 0 && view.DataContext is TradeEditorViewModel vm)
                            _ = vm.ChangeTradeIDAsync(initialIndex);   // async load; freshly opened editor isn't dirty
                        return view;
                    });
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("Couldn't open the Trade editor: " + ex.Message, "Trade editor");
            }
        }

        public static void OpenTextEditor(int initialIndex = 0) => _ = OpenTextEditorAsync(initialIndex);

        /// <summary>Opens the Text editor on an archive with one of its lines picked and in sight.</summary>
        public static void OpenTextEditorAt(int archive, int line) => _ = OpenTextEditorAsync(archive, line);

        public static async System.Threading.Tasks.Task OpenTextEditorAsync(int initialIndex = 0, int initialLine = -1)
        {
            if (Refused("TextEditorView")) return;

            try
            {
                await OpenBehindCardAsync("Opening Text editor…", "Reading every text archive.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.TextArchives }),
                    () => new EditorHostWindow("Text editor",
                        new TextEditorView(new TextEditorViewModel(true) { InitialIndex = initialIndex, InitialLine = initialLine }), 980, 640));
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("Couldn't open the Text editor: " + ex.Message, "Text editor");
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
            if (Refused("ScriptEditorView")) return;
            new EditorHostWindow("Script editor",
                new ScriptEditorView(new ScriptEditorViewModel(true) { InitialIndex = initialIndex, InitialScriptNumber = scriptNumber }),
                980, 760).ShowManaged();
        }

        /// <summary>Opens the Script editor on a script number, in its common file or the paired one; returns a status line.</summary>
        public static string GoToScript(int scriptNumber, System.Func<int> pairedScriptFile) =>
            GoToScript(scriptNumber, pairedScriptFile, out _);

        public static string GoToScript(int scriptNumber, System.Func<int> pairedScriptFile, out bool opened)
        {
            opened = false;
            CommonScriptId.Result result = CommonScriptId.Resolve(GameFamily, scriptNumber);
            if (result.Kind == CommonScriptId.Kind.Resolved)
            {
                OpenScriptEditor(result.ScriptArchiveId, result.ManualUserId);
                opened = true;
                return result.Where + ".";
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
            if (Refused("LevelScriptEditorView")) return;
            new EditorHostWindow("Level script editor",
                new LevelScriptEditorView(new LevelScriptEditorViewModel(true) { InitialIndex = initialIndex }),
                720, 560).ShowManaged();
        }

        public static void OpenTableEditor()
        {
            // Diamond and Pearl have only the effect combos, and only on supported ROMs.
            if (Refused("TableEditorView")) return;
            if (BringForwardWindow<TableEditorView>()) return;
            new TableEditorView(new TableEditorViewModel(HeaderLabels.Friendly())).ShowManaged();
        }

        /// <param name="headbuttFile">Opens on the Headbutt tab at this file; -1 opens on the first tab.</param>
        public static void OpenSpecialEncountersEditor(int headbuttFile = -1)
        {
            if (!IsRomLoaded) return;
            // On hg-engine only Headbutt, and Safari once a checkout is linked, are edited from source here.
            bool headbuttOnly = RomInfo.IsHGE;
            if (headbuttOnly && (GameFamily != GameFamilies.HGSS || Refused("SpecialEncountersEditorView"))) return;
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
                if (whyNot() is string why) { _ = DialogHelper.ShowInfoAsync(why, title); return; }
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
                _ = DialogHelper.ShowErrorAsync("Couldn't open " + title + ": " + ex.Message, title);
            }
        }

        public static void OpenWildHeldItems() => OpenTableEditor("Wild held items", WildHeldItemOdds.WhyNot,
            () => new WildHeldItemOddsView(new WildHeldItemOddsViewModel(true)), 640, 360, 460, 340);

        public static void OpenPokedexLists() => OpenTableEditor("Pokédex lists", PokedexLists.WhyNot,
            () => new PokedexListsView(new PokedexListsViewModel(true)), 860, 640, 640, 420);

        public static void OpenGrowthCurves() => OpenTableEditor("Growth curves", GrowthTable.WhyNot,
            () => new GrowthCurveEditorView(new GrowthCurveEditorViewModel(true)), 900, 680, 700, 460);

        public static void OpenFriendshipChanges() => OpenTableEditor("Friendship changes", FriendshipTable.WhyNot,
            () => new FriendshipChangesView(new FriendshipChangesViewModel(true)), 760, 480, 640, 440);

        public static void OpenEncounterSlotOdds() => OpenTableEditor("Encounter slot odds", EncounterSlotOdds.WhyNot,
            () => new EncounterSlotOddsView(new EncounterSlotOddsViewModel(true)), 560, 660, 440, 480);

        public static void OpenBreedingItems() => OpenTableEditor("Breeding items", IncenseBreedingTable.WhyNot,
            () => new BreedingItemsView(new BreedingItemsViewModel(true)), 820, 440, 790, 360);

        public static void OpenBerryData() => OpenTableEditor("Berry data", BerryData.WhyNot,
            () => new BerryDataEditorView(new BerryDataEditorViewModel(true)), 800, 580, 760, 480);

        public static void OpenTypeChart() => OpenTableEditor("Type chart", TypeChart.WhyNot,
            () => new TypeChartEditorView(new TypeChartEditorViewModel(true)), 1080, 780);

        public static void OpenMoveTutors() => OpenTableEditor("Move tutors", MoveTutorData.WhyNot,
            () => new MoveTutorEditorView(new MoveTutorEditorViewModel(true)), 1000, 680, 850, 420);

        public static void OpenBpShop() => OpenTableEditor("Battle Point shop", BpShopData.WhyNot,
            () => new BpShopEditorView(new BpShopEditorViewModel(true)), 1000, 640, 810, 420);

        public static void OpenUndergroundMining() => OpenTableEditor("Underground mining", MiningTable.WhyNot,
            () => new UndergroundMiningView(new UndergroundMiningViewModel(true)), 1000, 700, 1000, 420);

        public static void OpenTmHmBulkEditor() => _ = OpenTmHmBulkEditorAsync();

        public static async System.Threading.Tasks.Task OpenTmHmBulkEditorAsync()
        {
            // It writes personal data, but hg-engine takes machine compatibility from data/learnsets/learnsets.json.
            if (Refused("TmHmBulkEditorView")) return;

            try
            {
                await OpenBehindCardAsync("Opening TM/HM bulk editor…", "Reading which Pokémon learn each TM and HM.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.PersonalPokeData, DirNames.Evolutions }),
                    () =>
                    {
                        TmHmBulkEditorViewModel vm = new TmHmBulkEditorViewModel(GetPokemonNames());
                        EditorHostWindow window = new EditorHostWindow("TM/HM bulk editor", new TmHmBulkEditorView(vm), 1050, 700);
                        return window;
                    });
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("Couldn't open the TM/HM bulk editor: " + ex.Message, "TM/HM bulk editor");
            }
        }

        public static void OpenBattleTowerEditor()
        {
            if (Refused("BattleTowerEditorView")) return;
            new EditorHostWindow("Battle Tower editor",
                new BattleTowerEditorView(new BattleTowerEditorViewModel()),
                1000, 700).ShowManaged();
        }

        /// <param name="initialIndex">The encounter file to open, or -1 for the first one with encounters.</param>
        public static void OpenWildEditor(int initialIndex = -1) => _ = OpenWildEditorAsync(initialIndex);

        public static async System.Threading.Tasks.Task OpenWildEditorAsync(int initialIndex = -1)
        {
            if (Refused("WildEditorView")) return;

            try
            {
                await OpenBehindCardAsync("Opening Wild Pokémon editor…", "Reading this game's encounter files.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.Encounters, DirNames.MonIcons }), BuildWild);
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("Couldn't open the Wild Pokémon editor: " + ex.Message, "Wild Pokémon editor");
            }

            global::Avalonia.Controls.Window BuildWild()
            {
                string path = GameDirs[DirNames.Encounters].unpackedDir;
                string[] names = GetPokemonNames();
                int headerCount = GetHeaderCount();
                // A standalone window unsubscribes NamesChanged when it closes; the embedded tab lives as long as the app.
                if (GameFamily == GameFamilies.DP || GameFamily == GameFamilies.Plat)
                {
                    WildEditorDPPtViewModel vm = new WildEditorDPPtViewModel(path, names, initialIndex, headerCount);
                    EditorHostWindow window = new EditorHostWindow("Wild Pokémon editor", new WildEditorDPPtView(vm), 1000, 680) { MinWidth = 960, MinHeight = 520 };
                    return window;
                }
                else
                {
                    WildEditorHGSSViewModel vm = new WildEditorHGSSViewModel(path, names, initialIndex, headerCount);
                    EditorHostWindow window = new EditorHostWindow("Wild Pokémon editor", new WildEditorHGSSView(vm), 1000, 680) { MinWidth = 960, MinHeight = 520 };
                    return window;
                }
            }
        }

        public static void OpenHeaderEditor(int initialIndex = -1)
        {
            if (Refused("HeaderEditorView")) return;
            if (BringForward<HeaderEditorView, HeaderEditorViewModel>(vm => { if (initialIndex >= 0) vm.GoToHeader(initialIndex); })) return;
            HeaderEditorViewModel model = new HeaderEditorViewModel(true) { InitialHeaderId = initialIndex };
            EditorHostWindow window = new EditorHostWindow("Header editor", new HeaderEditorView(model));
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

        // Both intro editors read the same tables, so each opens once and neither saves a stale copy.
        private static async System.Threading.Tasks.Task OpenIntroEditorAsync<TView, TModel>(string title, string busyHint,
            System.Func<TModel> make, System.Action<TModel> load, System.Action<TModel> ready, System.Func<TModel, TView> view,
            double width, double height, System.Action<TModel> goTo = null, double minWidth = 0)
            where TView : global::Avalonia.Controls.Control where TModel : class
        {
            if (Refused(typeof(TView).Name)) return;
            if (BringForward<TView, TModel>(vm => goTo?.Invoke(vm))) return;
            if (VsIntroTables.WhyNot() is string why) { _ = DialogHelper.ShowInfoAsync(why, title); return; }

            TModel vm = make();
            try
            {
                // The list and the preview keep their widths, so a narrower window would squeeze the settings away.
                await OpenBehindCardAsync("Opening " + title + "…", busyHint, () => load(vm),
                    () => { ready(vm); return new EditorHostWindow(title, view(vm), width, height) { MinWidth = minWidth }; });
            }
            catch (System.Exception ex) when (ex is System.IO.IOException || ex is System.IO.InvalidDataException
                                              || ex is System.InvalidOperationException || ex is System.UnauthorizedAccessException)
            {
                _ = DialogHelper.ShowErrorAsync("Couldn't open " + title + ": " + ex.Message, title);
            }
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
                await OpenBehindCardAsync("Opening Trainer editor" + (initialIndex > 0 ? At(GetSimpleTrainerNames, initialIndex) : "") + "…", "Reading every trainer, party and trainer sprite.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.TrainerProperties, DirNames.TrainerParty, DirNames.TrainerGraphics }),
                    () => new TrainerEditorView(new TrainerEditorViewModel(true) { InitialIndex = initialIndex }));
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("Couldn't open the Trainer editor: " + ex.Message, "Trainer editor");
            }
        }

        public static void OpenTrainerSpriteEditor(int initialClassIndex = 0, System.Action closed = null) => _ = OpenTrainerSpriteEditorAsync(initialClassIndex, closed);

        public static async System.Threading.Tasks.Task OpenTrainerSpriteEditorAsync(int initialClassIndex = 0, System.Action closed = null)
        {
            if (Refused("TrainerSpriteEditorView")) return;

            try
            {
                await OpenBehindCardAsync("Opening Trainer sprite editor…", "Reading the trainer class sprites.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.TrainerGraphics }),
                    () =>
                    {
                        TrainerSpriteEditorViewModel vm = new TrainerSpriteEditorViewModel(initialClassIndex);
                        EditorHostWindow window = new EditorHostWindow(vm.Title, new TrainerSpriteEditorView(vm), 900, 760);
                        if (closed != null) window.Closed += (_, _) => closed();
                        return window;
                    });
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("Couldn't open the Trainer sprite editor: " + ex.Message, "Trainer sprite editor");
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
            await OpenBehindCardAsync("Opening Trainer flag bulk editor…",
                "Reading every trainer's AI flags.",
                () =>
                {
                    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.TrainerProperties });
                    vm = new TrainerFlagBulkEditorViewModel();
                },
                () => vm == null ? null : new EditorHostWindow("Trainer flag bulk editor", new TrainerFlagBulkEditorView(vm), 1050, 700));
        }

        public static void OpenVsSeekerRematchEditor(int initialRowIndex = -1) => _ = OpenVsSeekerRematchEditorAsync(initialRowIndex);

        public static async System.Threading.Tasks.Task OpenVsSeekerRematchEditorAsync(int initialRowIndex = -1)
        {
            if (Refused("VsSeekerRematchView")) return;

            VsSeekerRematchViewModel vm = null;
            await OpenBehindCardAsync("Opening Vs. Seeker rematch editor…",
                "Reading the rematch table and trainer names.",
                () => vm = new VsSeekerRematchViewModel(initialRowIndex),
                () => vm == null ? null : new EditorHostWindow("Vs. Seeker rematch editor", new VsSeekerRematchView(vm), 900, 600));
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
            await OpenBehindCardAsync("Opening Pokégear rematch editor…",
                "Reading the rematch table, the phone book and trainer names.",
                () => vm = new PokegearRematchViewModel(initialRowIndex),
                () => vm == null ? null : new EditorHostWindow("Pokégear rematch editor", new PokegearRematchView(vm), 950, 640));
        }

        public static void OpenPokegearPhoneBook(int initialEntry = -1) => _ = OpenPokegearPhoneBookAsync(initialEntry);

        public static async System.Threading.Tasks.Task OpenPokegearPhoneBookAsync(int initialEntry = -1)
        {
            if (Refused("PokegearPhoneBookView")) return;

            try
            {
                PokegearPhoneBookViewModel vm = null;
                await OpenBehindCardAsync("Opening Pokégear phone book…",
                    "Reading the phone book, contact names, trainers, maps and items.",
                    () =>
                    {
                        DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.TrainerProperties });
                        vm = new PokegearPhoneBookViewModel(initialEntry);
                    },
                    () => vm == null ? null : new EditorHostWindow("Pokégear phone book", new PokegearPhoneBookView(vm), 1320, 700));
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("Couldn't open the Pokégear phone book: " + ex.Message, "Pokégear phone book");
            }
        }

        public static void OpenStarterEditor() => _ = OpenStarterEditorAsync();

        public static async System.Threading.Tasks.Task OpenStarterEditorAsync()
        {
            if (Refused("StarterEditorView")) return;

            try
            {
                await OpenBehindCardAsync("Opening Starter Pokémon editor…", "Finding the starter script.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.Scripts, DirNames.PersonalPokeData }),
                    () => new StarterEditorView());
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("Couldn't open the Starter Pokémon editor: " + ex.Message, "Starter Pokémon editor");
            }
        }

        // ── World / data editors ───────────────────────────────────────────────
        public static void OpenFlyWarpEditor()
        {
            if (Refused("FlyEditorView")) return;
            if (RomInfo.FlyTableUnverified)
            {
                _ = DialogHelper.ShowErrorAsync("The Fly / warp editor doesn't support Japanese Pearl yet, so it can't open.", "Fly / warp editor");
                return;
            }
            new FlyEditorView(HeaderLabels.Friendly().ToList()).ShowManaged();
        }

        public static void OpenDungeonCutinEditor()
        {
            if (Refused("DungeonCutinEditorView")) return;
            new DungeonCutinEditorView(HeaderLabels.Friendly().ToList()).ShowManaged();
        }

        public static void OpenTitleScreenEditor()
        {
            if (Refused("TitleScreenEditorView")) return;
            OpenScreensEditor("title", view: "Edit");
        }

        /// <summary>Lists every cell animation in the game so one can be picked and opened.</summary>
        public static void OpenCellAnimationPicker() => _ = OpenCellAnimationPickerAsync();

        public static async System.Threading.Tasks.Task OpenCellAnimationPickerAsync()
        {
            if (Refused("CellAnimationEditorView")) return;
            try
            {
                // Finding the animations reads every archive, so it runs behind the busy card; the window is built here, on the UI thread.
                CellAnimationPickerViewModel vm = new ViewModels.Graphics.CellAnimationPickerViewModel();
                await OpenBehindCardAsync("Looking for animations…", "Reading every archive in the ROM.", vm.Gather,
                    () => { vm.Ready(); return new Views.Graphics.CellAnimationPickerView(vm); });
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenCellAnimationPicker failed: " + ex.Message);
                await DialogHelper.ShowInfoAsync("Couldn't list the animations: " + ex.Message,
                                            "Cell animations");
            }
        }

        /// <summary>Opens one cell animation. The caller names its files, since the animation names cells and the cells name tiles.</summary>
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
                _ = DialogHelper.ShowInfoAsync("There is no animation file here to open.", "Cell animation");
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
                _ = DialogHelper.ShowInfoAsync("Couldn't open that animation: " + ex.Message,
                                          "Cell animation");
            }
        }

        /// <param name="poketchApp">
        /// A Pokétch app to open on, or -1 for the window's default. An animation opened from an app uses it to go back to that app.
        /// </param>
        public static void OpenBottomScreenEditor(int poketchApp = -1)
        {
            if (Refused("BottomScreenEditorView")) return;
            OpenScreensEditor("bottomscreen", view: "Edit", then: model => ShowPoketchApp(model, poketchApp));
        }

        // The list starts with the casing, so each app is one row below its Pokétch table index.
        private static void ShowPoketchApp(object model, int poketchApp)
        {
            if (poketchApp < 0 || model is not ViewModels.Graphics.BottomScreenEditorViewModel vm)
                return;
            int at = System.Array.FindIndex(Data.PoketchApps.All, a => a.Id == poketchApp);
            if (at >= 0) vm.SelectedApp = at + 1;
        }

        /// <summary>The naming screen's icons, painted like trainer sprites, with the screen's top bar as a preview.</summary>
        public static void OpenNamingScreenEditor() => _ = OpenNamingScreenEditorAsync();

        public static async System.Threading.Tasks.Task OpenNamingScreenEditorAsync()
        {
            if (Refused("NamingScreenEditor")) return;
            try { OpenScreensEditor("naming", view: "Edit"); }
            catch (System.Exception ex)
            {
                CrashReporter.LogHandled(ex);
                await DialogHelper.ShowErrorAsync("Couldn't open the naming screen: " + ex.Message, "Naming screen");
            }
        }

        public static void OpenTrainerCardEditor()
        {
            if (Refused("TrainerCardEditorView")) return;
            OpenScreensEditor("trainercard", view: "Edit");
        }

        /// <summary>Opens one particle file for editing; <paramref name="changed"/> hears which entry was saved.</summary>
        public static void OpenParticleEditor(DirNames archive, int entry, string what, System.Action<int> changed,
                                              bool orthographic = false)
        {
            if (Refused("ParticleEditorView")) return;
            if (GameDirs.ContainsKey(archive)) DSUtils.TryUnpackNarcs(new List<DirNames> { archive });
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
                _ = DialogHelper.ShowInfoAsync("Couldn't open these particles: " + ex.Message, "Particles");
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
                await OpenBehindCardAsync("Looking for particles…", "Reading every archive in the ROM.", () =>
                {
                    DSUtils.TryUnpackNarcs(new List<DirNames> {
                        DirNames.WazaParticle, DirNames.BallParticles, DirNames.WazaEffectScripts, DirNames.WazaEffectSub,
                        DirNames.EncounterEffectGraphics }
                        .Where(d => GameDirs.ContainsKey(d)).ToList());
                    vm.Gather();
                },
                () => { vm.Ready(); return new Views.Graphics.ParticleLibraryView(vm); });
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenParticleLibrary failed: " + ex.Message);
                await DialogHelper.ShowInfoAsync("Couldn't list the particles: " + ex.Message, "Particles");
            }
        }

        public static void OpenBallCapsuleEditor() => _ = OpenBallCapsuleEditorAsync();

        /// <summary>Opens Ball capsules on one trainer capsule, numbered as a party entry names it.</summary>
        public static void OpenBallCapsuleEditorAt(int trainerCapsule) => _ = OpenBallCapsuleEditorAsync(trainerCapsule);

        public static async System.Threading.Tasks.Task OpenBallCapsuleEditorAsync(int trainerCapsule = 0)
        {
            if (Refused("BallCapsuleEditorView")) return;
            Dictionary<int, List<string>> usedBy = null;
            try
            {
                await OpenBehindCardAsync("Opening Ball capsules…", "Reading the seals and their effects.", () =>
                {
                    DSUtils.TryUnpackNarcs(new List<DirNames> {
                        DirNames.PersonalPokeData, DirNames.PokemonBattleSprites, DirNames.OtherPokemonBattleSprites,
                        DirNames.MonIcons, DirNames.SealGraphics, DirNames.TrainerCapsules, DirNames.Fonts, DirNames.WindowFrames }
                        .Where(d => GameDirs.ContainsKey(d)).ToList());
                    Data.SendOutGraphics.Unpack();
                    usedBy = Data.TrainerCapsuleCatalog.UsedBy();
                },
                () =>
                {
                    SetMonIconsPalTableAddress();
                    string[] names = GetPokemonNamesWithForms(GetPersonalFilesCount());
                    BallCapsuleEditorViewModel vm = new ViewModels.Graphics.BallCapsuleEditorViewModel(names) { UsedBy = usedBy };
                    if (trainerCapsule > 0) vm.CapsuleIndex = trainerCapsule;
                    return new Views.Graphics.BallCapsuleEditorView(vm);
                });
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenBallCapsuleEditor failed: " + ex.Message);
                await DialogHelper.ShowErrorAsync("Couldn't open Ball capsules: " + ex.Message, "Ball capsules");
            }
        }

        public static async System.Threading.Tasks.Task OpenBannerEditorAsync()
        {
            if (Refused("BannerEditorView")) return;
            if (!RomInfo.IsDsRomProject)
            {
                await DialogHelper.ShowInfoAsync(
                    "The game icon and banner titles need a ds-rom project.\n" +
                    "Use File > Convert to ds-rom format, then open this editor again.",
                    "ds-rom project required");
                return;
            }
            new BannerEditorView(new ViewModels.Graphics.BannerEditorViewModel()).ShowManaged();
        }

        public static void OpenSpawnEditor()
        {
            if (Refused("SpawnEditorView")) return;
            new SpawnEditorView(new SpawnEditorViewModel(HeaderLabels.Friendly().ToList())).ShowManaged();
        }

        /// <summary>Opens the spawn editor ready to put the spawn on one cell of a header's matrix.</summary>
        public static void OpenSpawnEditorAt(ushort header, int column, int row)
        {
            if (Refused("SpawnEditorView")) return;
            new SpawnEditorView(null, HeaderLabels.Friendly().ToList(), header, column, row).ShowManaged();
        }

        public static void OpenBulkLearnsetEditor()
        {
            if (Refused("BulkLearnsetEditorView")) return;
            new Views.Pokemon.BulkLearnsetEditorView(new ViewModels.Pokemon.BulkLearnsetEditorViewModel(true)).ShowManaged();
        }

        public static void OpenBattleMessageEditor(int trainerId)
        {
            // hg-engine builds the message table from Trainers.c, which only a linked checkout can edit.
            if (Refused("BattleMessageEditorView")) return;
            new Views.Battle.BattleMessageEditorView(new ViewModels.Battle.BattleMessageEditorViewModel(trainerId)).ShowManaged();
        }

        /// <summary>Opens the paint window on one drawing; <paramref name="closed"/> runs when it closes, so the caller can redraw.</summary>
        public static void OpenPainter(Data.GraphicAssets.Archive archive, int member, System.Action closed = null, int palette = -1, int paletteBank = -1)
        {
            Views.Graphics.GraphicPainterView painter = new Views.Graphics.GraphicPainterView(new ViewModels.Graphics.GraphicPainterViewModel(archive, member, palette, paletteBank));
            if (closed != null) painter.Closed += (_, _) => closed();
            painter.ShowManaged();
        }

        public static void OpenHeaderSearch() => OpenHeaderSearchFor(null);

        /// <summary>Opens the advanced header search with its results going to <paramref name="goTo"/>.</summary>
        public static void OpenHeaderSearchFor(System.Action<int> goTo)
        {
            if (Refused("HeaderSearchView")) return;
            new HeaderSearchView(new HeaderSearchViewModel(true)) { GoTo = goTo }.ShowManaged();
        }

        public static void OpenMapEditor(int mapIndex = -1)
        {
            if (Refused("MapEditorView")) return;
            if (BringForward<MapEditorView, MapEditorViewModel>(vm => { if (mapIndex >= 0) vm.SelectedMapIndex = mapIndex; })) return;
            MapEditorViewModel vm = new MapEditorViewModel(true) { InitialMapIndex = mapIndex };
            EditorHostWindow window = new EditorHostWindow("Map editor", new MapEditorView(vm), 1200, 720);
            window.ShowManaged();
        }

        public static void OpenBuildingEditor(int initialIndex = 0)
        {
            if (Refused("BuildingEditorView")) return;
            new BuildingEditorView(new BuildingEditorViewModel(true) { InitialIndex = initialIndex }).ShowManaged();
        }

        public static void OpenMatrixEditor(int initialIndex = 0, int focusHeader = -1)
        {
            if (Refused("MatrixEditorView")) return;
            if (BringForward<MatrixEditorView, MatrixEditorViewModel>(vm => { vm.SelectedMatrixIndex = initialIndex; vm.FocusHeader = focusHeader; })) return;
            MatrixEditorViewModel model = new MatrixEditorViewModel(true) { InitialIndex = initialIndex, FocusHeader = focusHeader };
            EditorHostWindow window = new EditorHostWindow("Matrix editor", new MatrixEditorView(model), 1180, 640);
            window.ShowManaged();
        }

        public static void OpenEventEditor(int initialIndex = 0)
        {
            if (Refused("EventEditorView")) return;
            if (BringForward<EventEditorView, EventEditorViewModel>(vm => vm.SelectedEventIndex = initialIndex)) return;
            EventEditorViewModel model = new EventEditorViewModel(true) { InitialIndex = initialIndex };
            EditorHostWindow window = new EditorHostWindow("Event editor", new EventEditorView(model), 1200, 720);
            window.ShowManaged();
        }

        public static void OpenEventEditorWithOverworld(int eventFileId, int owIndex)
        {
            if (Refused("EventEditorView")) return;
            if (BringForward<EventEditorView, EventEditorViewModel>(vm => vm.GoToOverworld(eventFileId, owIndex))) return;
            EventEditorViewModel model = new EventEditorViewModel(true) { InitialIndex = eventFileId, InitialOverworldIndex = owIndex };
            EditorHostWindow window = new EditorHostWindow("Event editor", new EventEditorView(model), 1200, 720);
            window.ShowManaged();
        }

        public static void OpenEventEditorWithSpawnable(int eventFileId, int spawnIndex)
        {
            if (Refused("EventEditorView")) return;
            if (BringForward<EventEditorView, EventEditorViewModel>(vm => vm.GoToSpawnable(eventFileId, spawnIndex))) return;
            EventEditorViewModel model = new EventEditorViewModel(true) { InitialIndex = eventFileId, InitialSpawnableIndex = spawnIndex };
            EditorHostWindow window = new EditorHostWindow("Event editor", new EventEditorView(model), 1200, 720);
            window.ShowManaged();
        }

        public static void OpenAreaDataEditor(int initialIndex = 0)
        {
            if (Refused("AreaDataEditorView")) return;
            if (BringForward<AreaDataEditorView, AreaDataEditorViewModel>(vm => vm.SelectedIndex = initialIndex)) return;
            AreaDataEditorViewModel model = new AreaDataEditorViewModel(true) { InitialIndex = initialIndex };
            EditorHostWindow window = new EditorHostWindow("Area data editor", new AreaDataEditorView(model), 610, 380);
            window.ShowManaged();
        }

        public static void OpenNsbtxEditor() => OpenNsbtxEditor(false, -1);

        /// <summary>Opens the texture editor on one map or building texture pack.</summary>
        public static void OpenNsbtxEditor(bool buildings, int pack)
        {
            if (Refused("NsbtxEditorView")) return;
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

        /// <summary>Opens the Overworld editor on one overworld table entry.</summary>
        public static void OpenOverworldEditor(uint entry) => _ = OpenOverworldEditorAsync(entry);

        public static async System.Threading.Tasks.Task OpenOverworldEditorAsync(uint? entry = null)
        {
            if (Refused("BtxEditorView")) return;

            try
            {
                await OpenBehindCardAsync("Opening Overworld editor…", "Reading every overworld sprite.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.OWSprites }),
                    () =>
                    {
                        SetOWtable();
                        Set3DOverworldsDict();
                        ReadOWTable();
                        BtxEditorViewModel model = new BtxEditorViewModel(true);
                        if (entry is uint at) model.OpenAt(at);
                        return new BtxEditorView(model);
                    });
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("Couldn't open the Overworld editor: " + ex.Message, "Overworld editor");
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

        /// <summary>Not gated on a linked checkout, so someone with only the ROM can see what the game reads.</summary>
        public static void OpenHgeRomReview() => _ = OpenHgeRomReviewAsync();

        public static async System.Threading.Tasks.Task OpenHgeRomReviewAsync()
        {
            if (Refused("HgeRomReviewView")) return;
            try
            {
                await OpenBehindCardAsync("Opening hg-engine ROM review…", "Reading the icons and the a/0/2/8 tables.", HgeRomReviewViewModel.Unpack,
                    () => new HgeRomReviewView(new HgeRomReviewViewModel()));
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("Couldn't open the hg-engine ROM review: " + ex.Message, "hg-engine ROM review");
            }
        }

        public static void OpenDistortionWorldEditor() => _ = OpenDistortionWorldEditorAsync();

        public static async System.Threading.Tasks.Task OpenDistortionWorldEditorAsync()
        {
            if (Refused("DistortionWorldView")) return;
            try
            {
                await OpenBehindCardAsync("Opening Distortion World…", "Reading the floors, their maps, props and events.", DistortionWorldViewModel.Unpack,
                    () =>
                    {
                        DistortionWorldViewModel vm = new ViewModels.World.DistortionWorldViewModel();
                        if (vm.Available) return new Views.World.DistortionWorldView(vm);
                        AppMessages.Info("This ROM has no Distortion World data to edit.", "Distortion World");
                        return null;
                    });
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("Couldn't open the Distortion World editor: " + ex.Message, "Distortion World");
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
            // Writes to the ARM9, overlays and archives.
            if (Refused("PatchToolboxView")) return;
            if (BringForwardWindow<PatchToolboxView>()) return;
            new PatchToolboxView().ShowManaged();
        }

        /// <summary>Opens the toolbox showing one patch, for an editor that needs it.</summary>
        public static void OpenPatchToolboxAt(string key)
        {
            if (Refused("PatchToolboxView")) return;
            IReadOnlyList<Window> open = (global::Avalonia.Application.Current?.ApplicationLifetime
                        as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Windows;
            PatchToolboxView view = open?.OfType<PatchToolboxView>().FirstOrDefault();
            if (view == null) { view = new PatchToolboxView(); view.ShowManaged(); }
            else
            {
                if (view.WindowState == global::Avalonia.Controls.WindowState.Minimized) view.WindowState = global::Avalonia.Controls.WindowState.Normal;
                view.Activate();
            }
            (view.DataContext as PatchToolboxViewModel)?.ShowOnly(key);
        }

        public static void OpenCustomCommandManager()
        {
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
            if (!IsRomLoaded) return;   // every entry it lists needs a ROM
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
                ($"Go to move #{n}",           "move attack",                  () => OpenMoveDataEditor(n)),
                ($"Go to move animation #{n}", "move animation effect particles", () => OpenBattleScriptEditor(3, n)),
                ($"Go to TM / HM #{n}",        "tm hm machine",                () => OpenTMEditor(n)),
                ($"Go to item #{n}",           "item",                         () => OpenItemEditor(n)),
                ($"Go to trainer #{n}",        "trainer battle party",         () => OpenTrainerEditor(n)),
                ($"Go to trade #{n}",          "trade in-game",                () => OpenTradeEditor(n)),
                ($"Go to header #{n}",         "header map",                   () => OpenHeaderEditor(n)),
                ($"Go to building #{n}",       "building model",               () => OpenBuildingEditor(n)),
                ($"Go to Headbutt file #{n}",  "headbutt tree",                () => OpenSpecialEncountersEditor(n)),
                ($"Go to event file #{n}",     "event warp trigger overworld", () => OpenEventEditor(n)),
                ($"Go to script #{n}",         "script",                       () => OpenScriptEditor(n)),
                ($"Go to level script #{n}",   "level script",                 () => OpenLevelScriptEditor(n)),
                ($"Go to text archive #{n}",   "text string message archive",  () => OpenTextEditor(n)),
                ($"Go to matrix #{n}",         "matrix world grid",            () => OpenMatrixEditor(n)),
                ($"Go to area data #{n}",      "area data tileset",            () => OpenAreaDataEditor(n)),
                ($"Go to wild encounters #{n}","wild encounter grass surf",    () => OpenWildEditor(n)),
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
                await OpenBehindCardAsync("Opening All graphics…", "Finding every picture in the ROM.", vm.Scan,
                    () => { vm.Publish(); return new Views.Graphics.GraphicsBrowserView(vm); });
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenGraphicsBrowser failed: " + ex.Message);
                await DialogHelper.ShowInfoAsync("Couldn't open All graphics. Open a ROM first.", "All graphics");
            }
        }

        /// <summary>Opens the Screens editor, at a screen and a line when given.</summary>
        public static void OpenScreensEditor(string key = null, int bank = -1, int line = -1, string view = null,
                                             System.Action<object> then = null, bool fresh = false)
        {
            if (Refused("ScreensEditorView")) return;
            void GoTo(ViewModels.Graphics.ScreensEditorViewModel vm) { if (key != null) vm.GoTo(key, bank, line, view, then, fresh); }
            if (BringForward<Views.Graphics.ScreensEditorView, ViewModels.Graphics.ScreensEditorViewModel>(GoTo)) return;
            ViewModels.Graphics.ScreensEditorViewModel model = new ViewModels.Graphics.ScreensEditorViewModel(true);
            GoTo(model);
            new EditorHostWindow("Screens", new Views.Graphics.ScreensEditorView(model), 1400, 860).ShowManaged();
        }

        /// <summary>Opens the Pokédex pages with a sample Pokémon, or All graphics at the Pokédex's first screen when that editor is off.</summary>
        public static void OpenPokedexGraphics() => OpenPokedexGraphics(0, null);

        /// <summary>Opens the Pokédex graphics editor showing <paramref name="species"/> on page <paramref name="pageId"/>.</summary>
        public static void OpenPokedexGraphics(int species, string pageId)
        {
            if (EditorAvailability.Allows("PokedexGraphicsEditorView") && EditorAvailability.Allows("ScreensEditorView"))
            {
                // An editor with unsaved work keeps its sample; otherwise it's remade with this one.
                ViewModels.Graphics.ScreenCatalog.PokedexStart = (species, pageId);
                OpenScreensEditor("pokedex", view: "Edit", fresh: true,
                    then: model => (model as ViewModels.Graphics.ScreenPagesEditorViewModel)?.GoToPage(pageId));
                return;
            }
            int first = -1;
            foreach (ScreenGraphicsLayouts.Entry e in Data.ScreenGraphicsLayouts.For(RomInfo.DirNames.PokedexGraphics))
                if (e.Kind == "NSCR" && e.Drawing >= 0) { first = e.Index; break; }
            OpenGraphicAt(RomInfo.DirNames.PokedexGraphics, System.Math.Max(0, first), preferAssembled: true);
        }

        /// <summary>Opens the Pokémon summary screen's pages, put together with a sample Pokémon.</summary>
        public static void OpenSummaryScreen() => _ = OpenSummaryScreenAsync();

        /// <param name="species">The Pokémon to show; 0 keeps the usual sample.</param>
        /// <param name="pageId">The page to open, or null for the first.</param>
        public static async System.Threading.Tasks.Task OpenSummaryScreenAsync(int species = 0, string pageId = null)
        {
            if (Refused("SummaryScreenEditorView")) return;
            ViewModels.Graphics.ScreenCatalog.SummaryStart = (species, pageId);
            OpenScreensEditor("summary", view: "Edit", fresh: true,
                then: model => (model as ViewModels.Graphics.ScreenPagesEditorViewModel)?.GoToPage(pageId));
            await System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>Opens All graphics already looking at one file.</summary>
        /// <param name="palette">The colours to show it in, when the caller knows the game's pairing.</param>
        public static void OpenGraphicAt(RomInfo.DirNames archive, int fileIndex, bool preferAssembled = false, int palette = -1)
            => _ = OpenGraphicAtAsync(archive, fileIndex, preferAssembled, palette);

        private static async System.Threading.Tasks.Task OpenGraphicAtAsync(RomInfo.DirNames archive, int fileIndex, bool preferAssembled, int palette)
        {
            if (Refused("GraphicsBrowserView")) return;

            try
            {
                GraphicAssets.Archive a = Data.GraphicAssets.All.FirstOrDefault(x => x.Dir == archive);
                if (a == null)
                {
                    await DialogHelper.ShowInfoAsync("All graphics doesn't list that kind of graphic yet.", "All graphics");
                    return;
                }

                // All graphics, when open, already holds the list, so finding every picture again is wasted time.
                IReadOnlyList<Window> open = (global::Avalonia.Application.Current?.ApplicationLifetime
                            as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Windows;
                Views.Graphics.GraphicsBrowserView already = open?.OfType<Views.Graphics.GraphicsBrowserView>().FirstOrDefault();
                GraphicsBrowserViewModel vm = already?.DataContext as GraphicsBrowserViewModel;
                if (vm == null)
                {
                    vm = new ViewModels.Graphics.GraphicsBrowserViewModel(loadImmediately: false);
                    await RunBusyAsync("Opening All graphics…", "Finding every picture in the ROM.", vm.Scan);
                    vm.Publish();
                }
                bool found = vm.JumpTo(a, fileIndex, preferAssembled, palette);
                if (already != null && ReferenceEquals(already.DataContext, vm))
                {
                    if (already.WindowState == global::Avalonia.Controls.WindowState.Minimized) already.WindowState = global::Avalonia.Controls.WindowState.Normal;
                    already.Activate();
                }
                else new Views.Graphics.GraphicsBrowserView(vm).ShowManaged();
                if (!found)
                    vm.StatusText = "Graphic not found.";
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenGraphicAt failed: " + ex.Message);
                await DialogHelper.ShowInfoAsync("Couldn't open All graphics. Open a ROM first.", "All graphics");
            }
        }

        public static void OpenTrainerBackSpriteEditor(int initialSprite = 0) => _ = OpenTrainerBackSpriteEditorAsync(initialSprite);

        public static async System.Threading.Tasks.Task OpenTrainerBackSpriteEditorAsync(int initialSprite = 0)
        {
            if (Refused("TrainerBackSpriteEditor")) return;

            try
            {
                await OpenBehindCardAsync("Opening Trainer back sprite editor…", "Reading the player back sprites.",
                    () => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.TrainerBackGraphics }),
                    () =>
                    {
                        TrainerSpriteEditorViewModel vm = new TrainerSpriteEditorViewModel(initialSprite, TrainerSpriteSet.Backs);
                        return new EditorHostWindow(vm.Title, new TrainerSpriteEditorView(vm), 900, 760);
                    });
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("Couldn't open the Trainer back sprite editor: " + ex.Message, "Trainer back sprite editor");
            }
        }

        /// <summary>The editor that owns a graphic, and what to call it.</summary>
        public static (string Name, System.Action Open)? EditorForGraphic(RomInfo.DirNames archive, int fileIndex)
        {
            switch (archive)
            {
                case RomInfo.DirNames.PokemonBattleSprites:
                    return ("Pokémon editor", () => { _ = OpenPokemonEditorAsync(fileIndex / 6); });

                case RomInfo.DirNames.MonIcons:
                {
                        PokemonIconFiles.Icon icon = DSPRE.ROMFiles.PokemonIconFiles.Describe(fileIndex);
                    if (icon == null) return null;
                    int entry = icon.EditorId;
                    return ("Pokémon editor", () => { _ = OpenPokemonEditorAsync(entry); });
                }

                case RomInfo.DirNames.TrainerGraphics:
                    return ("Trainer sprite editor", () => OpenTrainerSpriteEditor(TrainerGraphicsLayout.ClassOf(fileIndex)));

                case RomInfo.DirNames.TrainerBackGraphics:
                    return ("Trainer back sprite editor", () => OpenTrainerBackSpriteEditor(TrainerGraphicsLayout.ClassOf(fileIndex)));

                case RomInfo.DirNames.ItemIcons:
                {
                    int item = ItemUsingDrawing(fileIndex);
                    if (item < 0) return null;
                    return ("Item editor", () => OpenItemEditor(item));
                }

                case RomInfo.DirNames.BattleBg:
                    return ("Battle scenes", OpenBattleSceneBrowser);

                case RomInfo.DirNames.DungeonCutinGraphics:
                    return ("Dungeon cut-in editor", OpenDungeonCutinEditor);

                case RomInfo.DirNames.TrainerCardGraphics:
                    return ("Trainer card editor", OpenTrainerCardEditor);

                case RomInfo.DirNames.SealGraphics:
                    return ("Ball capsules", OpenBallCapsuleEditor);

                case RomInfo.DirNames.EncounterEffectGraphics:
                    return ("VS intro editor", OpenVsIntroEditor);

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
            catch (System.Exception ex) { AppLogger.Warn("Item for drawing lookup: " + ex.Message); }
            return -1;
        }

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
                _ = DialogHelper.ShowInfoAsync("Couldn't open Picture to background.", "Picture to background");
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
                _ = DialogHelper.ShowInfoAsync("Couldn't open the fonts. Open a ROM first.",
                                          "Font editor");
            }
        }

        public static void OpenHpBarLayoutEditor() => _ = OpenHpBarLayoutEditorAsync();

        public static async System.Threading.Tasks.Task OpenHpBarLayoutEditorAsync()
        {
            if (Refused("HpBarLayoutEditorView")) return;
            OpenScreensEditor("hpbar", view: "Layout");
            await System.Threading.Tasks.Task.CompletedTask;
        }

        public static void OpenBattleScreenEditor() => _ = OpenBattleScreenEditorAsync();

        public static async System.Threading.Tasks.Task OpenBattleScreenEditorAsync()
        {
            if (Refused("BattleScreenEditorView")) return;
            OpenScreensEditor("battle", view: "Edit");
            await System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>Opens the battle scenes list, with the headers that use each scene.</summary>
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
                _ = DialogHelper.ShowInfoAsync("Couldn't open the battle scenes. Open a ROM first.",
                                          "Battle scenes");
            }
        }

        /// <summary>Opens the models and textures list, which is separate from All graphics.</summary>
        public static void OpenModelBrowser() => _ = OpenModelBrowserAsync();

        public static async System.Threading.Tasks.Task OpenModelBrowserAsync()
        {
            if (Refused("ModelBrowserView")) return;

            try
            {
                // Listing reads every 3D archive, too much to do on the click.
                ModelBrowserViewModel vm = new ViewModels.Graphics.ModelBrowserViewModel();
                await OpenBehindCardAsync("Opening All models and textures…", "Finding every model and texture in the ROM.", vm.Scan,
                    () => { vm.Publish(); return new Views.Graphics.ModelBrowserView(vm); });
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("OpenModelBrowser failed: " + ex.Message);
                await DialogHelper.ShowInfoAsync("Couldn't open All models and textures. Open a ROM first.", "All models and textures");
            }
        }

        /// <summary>Opens the editor with this menu name.</summary>
        public static void OpenEditorNamed(string name)
            => PaletteCommands().FirstOrDefault(c => c.Name == name)?.Run?.Invoke();

        /// <summary>The editor list shown in the command palette (mirrors the main menu).</summary>
        // Beta editors are left out, as in the menus.
        public static IEnumerable<CommandItem> PaletteCommands() => BuildCommands()
            .Where(c => (c.Beta == null || BetaEditors.Allows(c.Beta)) && (c.Shown == null || c.Shown()));

        private static bool ScreenShown(string key) =>
            IsRomLoaded && Data.GameTextScreens.Screens.FirstOrDefault(s => s.Key == key)?.Available == true;

        public static List<CommandItem> BuildCommands() => new()
        {
            new() { Name = "All graphics",          Keywords = "sprite picture image texture palette colour color icon font paint draw", Run = OpenGraphicsBrowser },
            new() { Name = "Pokédex graphics",      Keywords = "pokedex zukan dex screen background graphics", Run = OpenPokedexGraphics },
            new() { Name = "All models and textures", Keywords = "3d model nsbmd nsbtx building overworld map mesh", Run = OpenModelBrowser },
            new() { Beta = "BattleScreenEditorView", Name = "Battle screen",         Keywords = "battle screen gauge hp bar backdrop platform message box touch command", Run = OpenBattleScreenEditor },
            new() { Beta = "BattleSceneBrowserView", Name = "Battle scenes",         Keywords = "battle scene backdrop terrain platform ground", Run = OpenBattleSceneBrowser },
            new() { Beta = "ScreensEditorView", Name = "Screens", Keywords = "screen menu window text layout", Run = () => OpenScreensEditor() },
            new() { Beta = "ScreensEditorView", Name = "Options screen", Keywords = "options config text speed frame", Shown = () => ScreenShown("options"), Run = () => OpenScreensEditor("options") },
            new() { Beta = "ScreensEditorView", Name = "Main menu", Keywords = "continue new game title menu", Shown = () => ScreenShown("mainmenu"), Run = () => OpenScreensEditor("mainmenu") },
            new() { Beta = "ScreensEditorView", Name = "Start menu", Keywords = "field menu pause x menu", Shown = () => ScreenShown("startmenu"), Run = () => OpenScreensEditor("startmenu") },
            new() { Beta = "ScreensEditorView", Name = "Place-name banner", Keywords = "location map name popup sign", Shown = () => ScreenShown("banner"), Run = () => OpenScreensEditor("banner") },
            new() { Beta = "ScreensEditorView", Name = "PC boxes", Keywords = "pc box storage wallpaper", Shown = () => ScreenShown("box"), Run = () => OpenScreensEditor("box") },
            new() { Beta = "ScreensEditorView", Name = "Party", Keywords = "party pokemon list menu", Shown = () => ScreenShown("party"), Run = () => OpenScreensEditor("party") },
            new() { Beta = "ScreensEditorView", Name = "Bag", Keywords = "bag items pocket list", Shown = () => ScreenShown("bag"), Run = () => OpenScreensEditor("bag") },
            new() { Beta = "ScreensEditorView", Name = "Pokégear", Keywords = "pokegear phone radio map call", Shown = () => ScreenShown("pokegear"), Run = () => OpenScreensEditor("pokegear") },
            new() { Beta = "SummaryScreenEditorView", Name = "Summary screen",      Keywords = "summary status pokemon info skills moves ribbons hp exp bar text box layout pst", Run = OpenSummaryScreen },
            new() { Beta = "HpBarLayoutEditorView", Name = "HP bars",               Keywords = "hp bar gauge health name level status experience layout move position", Run = OpenHpBarLayoutEditor },
            new() { Beta = "TilesetBuilderView", Name = "Picture to background", Keywords = "png tiles tilemap palette background", Run = OpenTilesetBuilder },
            new() { Beta = "TitleScreenEditorView", Name = "Title screen",          Keywords = "logo copyright intro hgss", Run = OpenTitleScreenEditor },
            new() { Beta = "BottomScreenEditorView", Name = "Bottom screen",         Keywords = "touch menu poketch pokétch bottom screen field panel icons poke ball", Run = () => OpenBottomScreenEditor() },
            new() { Beta = "CellAnimationEditorView", Name = "Cell animations",       Keywords = "nanr animation frames cell sprite sequence playback timing", Run = OpenCellAnimationPicker },
            new() { Beta = "DungeonCutinEditorView", Name = "Dungeon cut-in editor",  Keywords = "dungeon location splash hgss", Run = OpenDungeonCutinEditor },
            new() { Beta = "TrainerSpriteEditorView", Name = "Naming screen", Keywords = "name entry player rival box icon keyboard", Run = OpenNamingScreenEditor },
            new() { Beta = "TrainerCardEditorView", Name = "Trainer card",          Keywords = "rank front back graphics", Run = OpenTrainerCardEditor },
            new() { Beta = "ParticleLibraryView", Name = "Particles",             Keywords = "spa particle emitter effect move animation seal burst sparkle", Run = OpenParticleLibrary },
            new() { Beta = "BallCapsuleEditorView", Name = "Ball capsules",         Keywords = "seal sticker capsule poke ball send out particles effect", Run = OpenBallCapsuleEditor },
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
            new() { Name = "Vs. Seeker rematch editor", Keywords = "rematch trainer encounter chain", Shown = () => RomInfo.GameFamily == RomInfo.GameFamilies.DP || RomInfo.GameFamily == RomInfo.GameFamilies.Plat, Run = () => OpenVsSeekerRematchEditor() },
            new() { Name = "Pokégear rematch editor", Keywords = "rematch trainer phone pokegear call hgss", Shown = () => RomInfo.GameFamily == RomInfo.GameFamilies.HGSS, Run = () => OpenPokegearRematchEditor() },
            new() { Beta = "PokegearPhoneBookView", Name = "Pokégear phone book", Keywords = "phone contact number call gift greeting pokegear hgss", Run = () => OpenPokegearPhoneBook() },
            new() { Name = "hg-engine patches", Keywords = "hook bytereplacement repoint arm9 overlay patch asm", Run = OpenHgEnginePatches },
            new() { Shown = () => DSPRE.HgEngine.HgEngineDev.Enabled, Name = "Battle tests", Keywords = "hg-engine test scenario auto_test battle_tests", Run = OpenBattleTests },
            new() { Name = "hg-engine settings", Keywords = "config.h config.s define toggle option fairy hidden abilities mega", Run = OpenHgEngineSettings },
            new() { Name = "Trainer flag bulk editor", Keywords = "ai double battle bulk", Run = OpenTrainerFlagBulkEditor },
            new() { Name = "Text editor",           Keywords = "string archive message", Run = () => OpenTextEditor() },
            new() { Name = "Script editor",         Run = () => OpenScriptEditor() },
            new() { Name = "Level script editor",   Run = () => OpenLevelScriptEditor() },
            new() { Shown = () => !ViewModels.Tools.TableEditorViewModel.OnlyIntroLinks, Name = "Music & battle tables", Keywords = "table conditional music battle effects combo vs poster", Run = OpenTableEditor },
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
            new() { Name = "Pokédex lists",         Keywords = "pokedex dex regional order sort alphabetical type search lists", Run = OpenPokedexLists },
            new() { Beta = "WildHeldItemOddsView", Name = "Wild held items",       Keywords = "held item chance odds compound eyes wild", Run = OpenWildHeldItems },
            new() { Name = "Special encounters editor", Keywords = "headbutt tree bug contest opponents great marsh honey safari trophy garden daily swarm", Run = () => OpenSpecialEncountersEditor() },
            new() { Name = "Battle Tower editor",   Keywords = "tower trainer set party rental", Run = OpenBattleTowerEditor },
            new() { Name = "Address helper",        Run = OpenAddressHelper },
            new() { Name = "Research helper",       Run = OpenResearchHelper },
            new() { Beta = "HgeRomReviewView", Name = "hg-engine ROM review",  Keywords = "hge binary icons sprites palettes archive", Run = OpenHgeRomReview },
            new() { Beta = "DistortionWorldView", Name = "Distortion World",      Keywords = "giratina platinum gravity platforms torn world", Run = OpenDistortionWorldEditor },
            new() { Name = "Character map manager",      Keywords = "text encoding", Run = OpenCharMapManager },
            new() { Name = "Custom script command manager", Keywords = "scrcmd script commands database", Run = OpenCustomCommandManager },
            new() { Beta = "FontEditorView", Name = "Font editor",           Keywords = "font letter glyph character typeface text", Run = OpenFontEditor },
            new() { Beta = "BannerEditorView", Name = "Game icon & banner",    Keywords = "rom icon ds menu title", Run = () => { _ = OpenBannerEditorAsync(); } },
            new() { Name = "Edit dropdown labels",  Keywords = "enum custom", Run = OpenLabelEditor },
            new() { Beta = "ProjectChecksView", Name = "Validation and where used", Keywords = "check broken references project health", Run = OpenProjectChecks },
            new() { Name = "Settings",              Run = OpenSettings },
            // ── Actions (not editors) ──
            new() { Name = "Toggle theme (light / dark)", Keywords = "dark light appearance", Run = ThemeManager.Toggle },
        };
    }
}
