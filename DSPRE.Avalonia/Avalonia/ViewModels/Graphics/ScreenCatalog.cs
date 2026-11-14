using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    internal static class ScreenCatalog
    {
        private static GameTextScreens.Screen Text(string key) => GameTextScreens.Screens.FirstOrDefault(s => s.Key == key);

        /// <summary>The Pokémon and page the next Pokédex and summary editors open on.</summary>
        public static (int Species, string Page) PokedexStart, SummaryStart;

        private static ScreenEditorView Editor(string gate, string name, Func<Control> make, Action openApart = null,
                                               Action prepare = null, string busy = null, Action forgotten = null) =>
            EditorAvailability.Allows(gate) ? new ScreenEditorView(name, make, openApart, prepare, busy, forgotten) : null;

        private static ScreenEditorView LayoutOf(string screen, string title, ScreenButtonsLink link = null, Action forgotten = null) =>
            ScreenWindowLayout.Read(screen) == null ? null
                : new ScreenEditorView("Layout", () => new Views.Graphics.ScreenWindowLayoutView(new ScreenWindowLayoutViewModel(screen, title, link)),
                                       forgotten: forgotten);

        private static readonly DirNames[] SummaryArchives =
        {
            DirNames.SummaryGraphics, DirNames.SummarySprites, DirNames.PokemonBattleSprites, DirNames.MonIcons,
            DirNames.BattleObj, DirNames.PersonalPokeData, DirNames.MoveData, DirNames.Fonts,
        };

        private static Action Unpack(params DirNames[] dirs) => () => DSUtils.TryUnpackNarcs(dirs.ToList());

        // HGSS: Info shows the tabs and party, Stats and moves the move list, Ribbons the ribbons.
        private static int HgSummaryPage(int page) => page switch { 0 => 0, 1 => 1, 2 => 4, _ => -1 };

        private static ScreenEntry SummaryEntry(ScreensEditorViewModel o)
        {
            ScreenButtonsLink link = null;
            Action forgetButtons = null;
            ScreenPagesEditorViewModel shared = null;
            ScreenPagesEditorViewModel Model() =>
                shared ??= new ScreenPagesEditorViewModel(new SummaryBook(SummaryStart.Species), SummaryStart.Page, link);
            void Forget()
            {
                shared = null;
                forgetButtons?.Invoke();
            }
            ScreenEditorView[] buttons = RomLocator.PtSummaryTouchSites != null
                ? ButtonTabs(PlatinumTouchLists.Summary, "Summary", (summary, page) =>
                {
                    List<byte[]> entries = Enumerable.Range(0, summary.ButtonsOf(page).Length).Select(i => summary.SpotEntry(page, 0, i)).ToList();
                    return () => GameTextScreens.SummaryBottomPt(entries, page == 1);
                }, "Buttons", cursor: false, share: (model, drop) => (link, forgetButtons) = (new ScreenButtonsLink(model, _ => 0), drop))
                : RomLocator.SummaryTouchSites == null ? Array.Empty<ScreenEditorView>()
                : ButtonTabs(SummaryTouchButtons.Read, "Summary", (summary, page) =>
                {
                    IReadOnlyList<(int X, int Y)> icons = summary.IconSpots;
                    return () => HgssSummaryPictures.Draw(page, icons);
                }, "Buttons", cursor: false, SummaryArchives,
                share: (model, drop) => (link, forgetButtons) = (new ScreenButtonsLink(model, HgSummaryPage), drop));
            return new ScreenEntry(o, "summary", "Summary", null, new[]
            {
                Editor("SummaryScreenEditorView", "Edit", () => new Views.Graphics.SummaryScreenEditorView(Model(), "Pieces"), null,
                       Unpack(SummaryArchives), "Opening Summary screen…", Forget),
                Editor("SummaryScreenEditorView", "Layout", () => new Views.Graphics.ScreenPagesLayoutView(Model()), null,
                       Unpack(SummaryArchives), "Opening Summary screen…", Forget),
            }.Concat(buttons).ToArray());
        }

        // Layout and Cursor share one model so the Cursor tab shows unsaved panel moves.
        private static ScreenEntry PartyEntry(ScreensEditorViewModel o)
        {
            ScreenEditorView edit = BackgroundTab(BottomPalettesPatch.Party, MenuBackgroundBook.Party, "Party");
            if (ScreenWindowLayout.Read("party") == null) return new ScreenEntry(o, "party", "Party", Text("party"), edit);
            ScreenWindowLayoutViewModel shared = null;
            ScreenWindowLayoutViewModel Model() => shared ??= new ScreenWindowLayoutViewModel("party", "Party");
            void Forget() => shared = null;
            return new ScreenEntry(o, "party", "Party", Text("party"), edit,
                new ScreenEditorView("Layout", () => new Views.Graphics.ScreenWindowLayoutView(Model()), forgotten: Forget),
                PartyCursorFlow.Read() == null ? null
                    : new ScreenEditorView("Cursor", () => new Views.Graphics.ScreenCursorView(Model()), forgotten: Forget));
        }

        private static ScreenEditorView BackgroundTab(BottomPalettesPatch.Screen screen, Func<MenuBackgroundBook> book, string title, string tab = "Edit") =>
            screen == null ? null
                : new ScreenEditorView(tab,() => new Views.Graphics.ScreenPagesView { DataContext = new ScreenPagesEditorViewModel(book(), null) },
                                       null, Unpack(screen.Archive), $"Opening {title}…");

        private static ScreenEntry BattleMenuEntry(ScreensEditorViewModel o, BattleMenuButtons.Menu menu, string key, string title) =>
            RomLocator.BattleMenuSites == null ? null : new ScreenEntry(o, key, title, null,
                // Battle Pokémon's header row lives on its own Layout tab.
                new[] { menu == BattleMenuButtons.Menu.Pokemon ? LayoutOf(key, title) : null }.Where(v => v != null).Concat(
                ButtonTabs(() => BattleMenuButtons.Read(menu), title, (buttons, page) =>
                {
                    List<BattleMenuPictures.Button> placed = BattleMenuPictures.Placed(buttons, page);
                    IReadOnlyList<(int X, int Y)>[] spots = BattleMenuPictures.Spots(buttons, page);
                    return () => BattleMenuPictures.Draw(menu, page, placed, spots);
                }, menu == BattleMenuButtons.Menu.Pokemon && ScreenWindowLayout.Read(key) != null ? "Buttons" : "Layout",
                top: _ => TouchTabPictures.BattleTop)).ToArray());

        private static readonly DirNames[] BattleArchives = { DirNames.BattleObj, DirNames.BattleBg, DirNames.WindowFrames, DirNames.Fonts };

        private static ScreenEntry BattleEntry(ScreensEditorViewModel o)
        {
            ScreenEditorView edit = Editor("BattleScreenEditorView", "Edit", () => new Views.Battle.BattleScreenEditorView(), null,
                                           Unpack(BattleArchives), "Opening Battle screen…");
            if (RomLocator.BattleCommandSites == null) return new ScreenEntry(o, "battle", "Battle screen", null, edit);
            // The first battle and Pal Park draw the Command page's labels, so every page's text spots load.
            ScreenEditorView[] tabs = ButtonTabs(BattleCommandButtons.Read, "Battle screen", (buttons, page) =>
            {
                IReadOnlyList<(int X, int Y)>[][] text = Enumerable.Range(0, buttons.PageNames.Count)
                    .Select(p => Enumerable.Range(0, buttons.ButtonsOf(p).Length).Select(b => buttons.TextOf(p, b)).ToArray()).ToArray();
                FightIconView icon = buttons.IconView;
                return () => TouchTabPictures.Battle(page, (p, b) => text[p][b], icon: icon);
            }, archives: BattleArchives, top: page => () => TouchTabPictures.BattleTop(page));
            ScreenEditorView[] press = BattlePressFrames.SupportsCurrentRom && !RomInfo.IsHGE
                ? new[] { new ScreenEditorView("Press", () => new Views.Graphics.BattlePressView(new BattlePressViewModel(BattlePressFrames.Panel)), null, Unpack(BattleArchives), "Opening Battle screen…") }
                : Array.Empty<ScreenEditorView>();
            return new ScreenEntry(o, "battle", "Battle screen", null, new[] { edit }.Concat(tabs).Concat(press).ToArray());
        }

        private static ScreenEditorView[] ButtonTabs(Func<IScreenButtons> read, string title, Func<int, byte[]> draw, string touchTab = "Layout") =>
            ButtonTabs(read, title, (_, page) => () => draw(page), touchTab);

        // draw reads the tables on the UI thread and returns a function that draws elsewhere.
        private static ScreenEditorView[] ButtonTabs<T>(Func<T> read, string title, Func<T, int, Func<byte[]>> draw, string touchTab = "Layout",
                                                         bool cursor = true, DirNames[] archives = null, Func<int, Func<byte[]>> top = null,
                                                         Action<Func<ScreenButtonsViewModel>, Action> share = null,
                                                         IReadOnlyDictionary<string, (int Page, int Button)[]> carried = null, Action afterSave = null) where T : class, IScreenButtons
        {
            ScreenButtonsViewModel shared = null;
            ScreenButtonsViewModel Model()
            {
                if (shared != null) return shared;
                T buttons = read();
                return shared = new ScreenButtonsViewModel(buttons, title, page => draw(buttons, page)) { TopDraw = top, Carried = carried, AfterSave = afterSave };
            }
            void Forget()
            {
                shared?.Detach();
                shared = null;
            }
            share?.Invoke(Model, Forget);
            Action prepare = archives == null ? null : Unpack(archives);
            string busy = archives == null ? null : $"Opening {title}…";
            ScreenEditorView touch = new ScreenEditorView(touchTab, () => new Views.Graphics.ScreenButtonsView(Model()), null, prepare, busy, Forget);
            return cursor ? new[] { touch, new ScreenEditorView("Cursor", () => new Views.Graphics.ScreenCursorView(Model()), null, prepare, busy, Forget) }
                          : new[] { touch };
        }

        // HGSS shows the field touch menu here; Platinum shows the Pokétch.
        private static ScreenEntry BottomScreenEntry(ScreensEditorViewModel o)
        {
            ScreenEditorView edit = Editor("BottomScreenEditorView", "Edit", () => new Views.Graphics.BottomScreenEditorView());
            if (RomLocator.PoketchTouchSites != null)
                return new ScreenEntry(o, "bottomscreen", "Bottom screen", null, new[] { edit }.Concat(ButtonTabs(PlatinumTouchLists.Poketch, "Bottom screen",
                    (lists, page) =>
                    {
                        string app = page == 0 ? null : lists.PageNames[page];
                        return () => TouchTabPictures.Poketch(app);
                    }, cursor: false)).ToArray());
            if (RomLocator.TouchMenuSites == null) return new ScreenEntry(o, "bottomscreen", "Bottom screen", null, edit);
            return new ScreenEntry(o, "bottomscreen", "Bottom screen", null, new[] { edit }.Concat(ButtonTabs(FieldTouchMenuButtons.Read, "Bottom screen",
                (menu, page) =>
                {
                    const int FirstModePage = 10;
                    if (page >= 2 && page < FirstModePage)
                    {
                        string[] labels = menu.ButtonsOf(page);
                        IReadOnlyList<(int X, int Y, int Width, int Height)> windows = menu.OptionWindows(page);
                        return () => HgssTouchPictures.Choices(labels, page == 2, windows);
                    }
                    IReadOnlyList<(int X, int Y)> sprites = menu.SpriteSpots, names = menu.NameSpots;
                    FieldTouchMenuButtons.MenuExtras extras = menu.Extras;
                    int mode = page >= FirstModePage ? page - FirstModePage + HgssTouchScreen.SafariMode : 0;
                    return () => HgssTouchPictures.Menu(sprites, names, mode, extras);
                })).ToArray());
        }

        // Platinum's corner tabs carry their touch areas on the corner and compare pages.
        private static readonly Dictionary<string, (int Page, int Button)[]> PtPcTabs = new Dictionary<string, (int Page, int Button)[]>
        {
            ["Box list tab"] = new[] { (0, 0), (1, 0) },
            ["Markings tab"] = new[] { (0, 1), (1, 1) },
        };

        // HGSS: the box in Move mode, with or without a Pokémon picked, the marking page and the box menu.
        private static int HgPcPage(int state) => state switch { 0 or 1 => 3, 3 => 8, 6 => 9, _ => -1 };

        private static ScreenEntry PcEntry(ScreensEditorViewModel o)
        {
            ScreenButtonsLink link = null;
            Action forget = null;
            ScreenEditorView[] buttons = RomLocator.PtPcTouchSites != null
                ? ButtonTabs(PlatinumTouchLists.Pc, "PC boxes", (_, page) => () => GameTextScreens.PcBottomPt(page), "Buttons", cursor: false,
                             share: (model, drop) => (link, forget) = (new ScreenButtonsLink(model, _ => -1, PtPcTabs), drop), carried: PtPcTabs)
                : RomLocator.PcBoxSites != null ? ButtonTabs(HgssMenuButtons.Pc, "PC boxes", (_, page) => () => GameTextScreens.BoxLayoutHg(page), "Buttons",
                                                             share: (model, drop) => (link, forget) = (new ScreenButtonsLink(model, HgPcPage), drop))
                : Array.Empty<ScreenEditorView>();
            ScreenEditorView layout = LayoutOf("box", "PC boxes", link, forget);
            return new ScreenEntry(o, "box", "PC boxes", Text("box"),
                new[] { BackgroundTab(BottomPalettesPatch.Pc, MenuBackgroundBook.Pc, "PC boxes"), layout }.Where(v => v != null).Concat(buttons).ToArray());
        }

        private static ScreenEntry PokedexEntry(ScreensEditorViewModel o)
        {
            ScreenEditorView edit = Editor("PokedexGraphicsEditorView", "Edit", () => new Views.Graphics.PokedexGraphicsEditorView(
                new ScreenPagesEditorViewModel(new PokedexBook(PokedexStart.Species), PokedexStart.Page)));
            if (RomLocator.PokedexSearchSites == null) return new ScreenEntry(o, "pokedex", "Pokédex", null, edit);
            return new ScreenEntry(o, "pokedex", "Pokédex", null, new[] { edit }.Concat(ButtonTabs(HgssMenuButtons.Pokedex, "Pokédex", (dex, page) =>
            {
                List<IReadOnlyList<(int X, int Y)>> spots = new List<IReadOnlyList<(int X, int Y)>>();
                for (int t = 0; dex.SpotsOf(page, t) is { } table; t++) spots.Add(table);
                HgssPokedexPictures.Look look = new HgssPokedexPictures.Look(dex.ScreenOf(page), spots, dex.SheetOf(page));
                return () => HgssPokedexPictures.Draw(page, look);
            }, top: page => () => HgssPokedexPictures.DrawTop(page))).ToArray());
        }

        // Platinum's bag has no Cursor tab because the code places its cursor.
        private static ScreenEntry BagEntry(ScreensEditorViewModel o)
        {
            ScreenButtonsLink link = null;
            Action forget = null;
            void Share(Func<ScreenButtonsViewModel> model, Action drop, Func<int, int> pageOf) => (link, forget) = (new ScreenButtonsLink(model, pageOf), drop);
            ScreenEditorView[] buttons = RomLocator.PtBagTouchSites != null
                ? ButtonTabs(PlatinumTouchLists.Bag, "Bag", (bag, page) =>
                {
                    IReadOnlyList<(int X, int Y)> spots = page <= 3 ? bag.SpotsOf(page) : null;
                    return () => GameTextScreens.BagBottomPt(spots);
                }, "Buttons", cursor: false, share: (model, drop) => Share(model, drop, _ => 0))
                : RomLocator.BagButtonSites != null
                    ? HgBagButtonTabs((model, drop, carries) => (link, forget) = (new ScreenButtonsLink(model, GameTextScreens.BagPageOf, carries), drop))
                    : Array.Empty<ScreenEditorView>();
            ScreenEditorView[] tabs = new[] { BackgroundTab(BottomPalettesPatch.Bag, MenuBackgroundBook.Bag, "Bag"), LayoutOf("bag", "Bag", link, forget) }
                .Where(v => v != null).ToArray();
            return new ScreenEntry(o, "bag", "Bag", Text("bag"), tabs.Concat(buttons).ToArray());
        }

        // With the bag item buttons patch the Layout tab moves the item buttons, so Buttons locks them.
        private static ScreenEditorView[] HgBagButtonTabs(Action<Func<ScreenButtonsViewModel>, Action, IReadOnlyDictionary<string, (int Page, int Button)[]>> share)
        {
            bool whole = BagButtonsPatch.IsApplied;
            Dictionary<string, (int Page, int Button)[]> carries = whole
                ? Enumerable.Range(0, BagButtonsPatch.Buttons).ToDictionary(i => $"Item {i + 1}", i => new[] { (0, i + 8) })
                : null;
            Dictionary<string, (int Page, int Button)[]> carried = whole
                ? Enumerable.Range(0, BagButtonsPatch.Buttons).ToDictionary(i => $"Item {i + 1}", i => new[] { (0, i + 8), (2, i) })
                : null;
            return ButtonTabs(FieldBagButtons.Read, "Bag", (bag, page) =>
                              {
                                  GameTextScreens.BagArt art = GameTextScreens.BagArtOf(bag);
                                  return () => GameTextScreens.BagBottomHg(page, art);
                              }, "Buttons",
                              share: (model, drop) => share(model, drop, carries), carried: carried);
        }

        private static ScreenEntry PokegearEntry(ScreensEditorViewModel o)
        {
            if (RomLocator.PokegearSites == null) return new ScreenEntry(o, "pokegear", "Pokégear", Text("pokegear"));
            ScreenButtonsLink link = null;
            Action forget = null;
            ScreenEditorView[] buttons = ButtonTabs(PokegearButtons.Read, "Pokégear", (gear, page) =>
            {
                IReadOnlyList<(int X, int Y)> spots = gear?.TileSpots(gear.RadioButtonsPage);
                byte[] dial = gear?.ScreenOf(gear.RadioButtonsPage);
                return () => PokegearPictures.Draw(page, spots, dial);
            }, "Buttons", share: (model, drop) => (link, forget) = (new ScreenButtonsLink(model, state => state switch { 0 => 3, 1 => 7, 2 => 2, 4 => 1, _ => -1 }), drop));
            return new ScreenEntry(o, "pokegear", "Pokégear", Text("pokegear"),
                new[] { LayoutOf("pokegear", "Pokégear", link, forget) }.Where(v => v != null).Concat(buttons).ToArray());
        }

        private static ScreenEntry OptionsEntry(ScreensEditorViewModel o) =>
            new ScreenEntry(o, "options", "Options", Text("options"), OptionsTabs().Concat(OptionsMenuPatch.Supports
                ? new[] { new ScreenEditorView("Extra", () => new Views.Graphics.OptionsExtraView(new OptionsExtraViewModel())) }
                : Array.Empty<ScreenEditorView>()).ToArray());

        private static ScreenEditorView[] MainMenuTabs()
        {
            if (RomLocator.HgssMainMenuSites == null) return new[] { LayoutOf("mainmenu", "Main menu") }.Where(v => v != null).ToArray();
            ScreenButtonsLink link = null;
            Action forget = null;
            Dictionary<string, (int Page, int Button)[]> arrows = new() { ["Up arrow"] = new[] { (0, 0) }, ["Down arrow"] = new[] { (0, 1) } };
            ScreenEditorView[] buttons = ButtonTabs(HgssTouchLists.MainMenu, "Main menu", (menu, page) =>
            {
                IReadOnlyList<(int X, int Y)> spots = menu.SpotsOf(1);
                return () => GameTextScreens.MainMenuBottomHg(page, spots);
            }, "Buttons", cursor: false, share: (model, drop) => (link, forget) = (new ScreenButtonsLink(model, _ => 0, arrows), drop), carried: arrows);
            ScreenEditorView layout = LayoutOf("mainmenu", "Main menu", link, forget);
            return (layout == null ? buttons : new[] { layout }.Concat(buttons)).ToArray();
        }

        private static ScreenEditorView[] OptionsTabs()
        {
            ScreenButtonsLink link = null;
            Action forget = null;
            ScreenEditorView[] buttons = OptionsButtons((model, drop) => (link, forget) = (new ScreenButtonsLink(model, _ => 0), drop));
            ScreenEditorView layout = LayoutOf("options", "Options", link, forget);
            return (layout == null ? buttons : new[] { layout }.Concat(buttons)).ToArray();
        }

        private static ScreenEditorView[] OptionsButtons(Action<Func<ScreenButtonsViewModel>, Action> share) =>
            RomLocator.HgssOptionsSites == null ? Array.Empty<ScreenEditorView>()
                : ButtonTabs(HgssTouchLists.Options, "Options", (options, _) =>
                {
                    (byte[], int[][], (int, int), (int, int))? look = null;
                    if (options != null)
                    {
                        (int[][] labels, (int, int) quit, (int, int) confirm) = HgssTouchLists.OptionsText(options);
                        look = (options.ScreenOf(0), labels, quit, confirm);
                    }
                    return () => GameTextScreens.OptionsBottomHg(look);
                }, "Buttons", cursor: false, top: _ => GameTextScreens.OptionsTopHg, share: share, afterSave: OptionsMenuPatch.Refresh);

        public static IEnumerable<(string, IEnumerable<ScreenEntry>)> Build(ScreensEditorViewModel o)
        {
            yield return ("Battle", new[]
            {
                BattleEntry(o),
                new ScreenEntry(o, "hpbar", "HP bars", null,
                    Editor("HpBarLayoutEditorView", "Layout", () => new Views.Graphics.HpBarLayoutEditorView(), null,
                           Unpack(DirNames.BattleObj, DirNames.BattleBg, DirNames.WindowFrames, DirNames.Fonts), "Opening HP bars…")),
                BattleMenuEntry(o, BattleMenuButtons.Menu.Pokemon, "battlepokemon", "Battle Pokémon"),
                BattleMenuEntry(o, BattleMenuButtons.Menu.Bag, "battlebag", "Battle bag"),
            }.Where(e => e != null));
            yield return ("Menus", new[]
            {
                PartyEntry(o),
                BagEntry(o),
                PcEntry(o),
                OptionsEntry(o),
                new ScreenEntry(o, "mainmenu", "Main menu", Text("mainmenu"), MainMenuTabs()),
                new ScreenEntry(o, "startmenu", "Start menu", Text("startmenu"), new[] { LayoutOf("startmenu", "Start menu") }.Where(v => v != null).ToArray()),
                new ScreenEntry(o, "trainercard", "Trainer card", Text("trainercard"),
                    new[] { Editor("TrainerCardEditorView", "Edit", () => new Views.Graphics.TrainerCardEditorView()), LayoutOf("trainercard", "Trainer card") }
                        .Where(v => v != null).Concat(
                        RomLocator.PtTrainerCaseSites != null
                            ? ButtonTabs(PlatinumTouchLists.TrainerCase, "Trainer card", (badgeCase, page) =>
                            {
                                IReadOnlyList<(int X, int Y)> badges = badgeCase.SpotsOf(0);
                                return () => GameTextScreens.TrainerCaseBottomPt(badges, page == 1);
                            }, "Buttons", cursor: false, top: _ => GameTextScreens.TrainerCardTopPt)
                            : RomLocator.HgssTrainerCardSites == null ? Array.Empty<ScreenEditorView>()
                            : ButtonTabs(HgssTouchLists.TrainerCard, "Trainer card", (card, _) =>
                            {
                                (int X, int Y) cancel = card.SpotsOf(0, 1)[0], hint = card.SpotsOf(0, 2)[0];
                                return () => GameTextScreens.TrainerCardBottomHg(cancel, hint);
                            }, "Buttons", cursor: false, top: page => () => GameTextScreens.TrainerCardTopHg(page == 1))).ToArray()),
                new ScreenEntry(o, "naming", "Naming screen", Text("naming"), new[]
                {
                    Editor("NamingScreenEditor", "Edit", () => new Views.Trainers.TrainerSpriteEditorView(
                            new ViewModels.Trainers.TrainerSpriteEditorViewModel(48, TrainerSpriteSet.NamingIcons)), null,
                           Unpack(DirNames.NameInputGraphics), "Opening Naming screen…"),
                    BackgroundTab(BottomPalettesPatch.Naming, MenuBackgroundBook.Naming, "Naming screen", "Background"),
                    LayoutOf("naming", "Naming screen"),
                }.Where(v => v != null).ToArray()),
            });
            yield return ("Pokédex and summary", new[]
            {
                PokedexEntry(o),
                SummaryEntry(o),
            });
            yield return ("Field", new[]
            {
                new ScreenEntry(o, "banner", "Place-name banner", Text("banner"), PlaceNameBanner.SupportsCurrentRom
                    ? new ScreenEditorView("Layout", () => new Views.Graphics.BannerLayoutView(new BannerLayoutViewModel())) : null),
                BottomScreenEntry(o),
                RomLocator.NumberPadSites == null ? null
                    : new ScreenEntry(o, "numberpad", "Number pad", null, ButtonTabs(HgssTouchLists.NumberPad, "Number pad", (pad, _) =>
                    {
                        IReadOnlyList<(int X, int Y)> keys = pad.SpotsOf(0, 0), labels = pad.SpotsOf(0, 1);
                        return () => GameTextScreens.NumberPadHg(keys, labels);
                    })),
                PokegearEntry(o),
            }.Where(e => e != null));
            yield return ("Title", new[]
            {
                new ScreenEntry(o, "title", "Title screen", Text("title"),
                    Editor("TitleScreenEditorView", "Edit", () => new Views.Graphics.TitleScreenEditorView())),
            });
        }
    }
}
