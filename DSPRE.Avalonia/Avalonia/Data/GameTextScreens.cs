using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Draws the game screens that print text, with each line where the game puts it (pokeplatinum and pokeheartgold layouts).
    /// </summary>
    public static partial class GameTextScreens
    {
        public sealed record Shot(string Name, byte[] Rgba, Action Open = null)
        {
            /// <summary>Message boxes the line takes; with more than one, the box shown matters.</summary>
            public int Stops { get; init; } = 1;
            /// <summary>True when the game moves to the next box on its own.</summary>
            public bool MovesOnByItself { get; init; }
            public string Key { get; init; }
            /// <summary>The files the picture was drawn from, with their type.</summary>
            public IReadOnlyList<(DirNames Dir, int Member, string Kind)> Pieces { get; init; } = Array.Empty<(DirNames, int, string)>();
            /// <summary>The files each sample Pokémon was drawn with, by party slot.</summary>
            public IReadOnlyList<(DirNames Dir, int Member, int Who)> PokemonPieces { get; init; } = Array.Empty<(DirNames, int, int)>();
        }

        public sealed class Screen
        {
            public string Key { get; init; }
            public string Title { get; init; }
            /// <summary>The text archives this ROM prints on it; empty when the game doesn't have the screen.</summary>
            public Func<int[]> Banks { get; init; }
            public Func<int, int, Shot> Draw { get; init; }
            public int FirstLine { get; init; }
            /// <summary>Each state has its own windows and graphics; null if the screen has none.</summary>
            public string[] States { get; init; }
            /// <summary>The state a line is printed in, or -1 if it shows in any state.</summary>
            public Func<int, int, int> StateOf { get; init; }
            /// <summary>Whether a window (by group and name) is on screen in a state.</summary>
            public Func<int, string, string, bool> Shows { get; init; }
            /// <summary>The Layout tab's states: <see cref="States"/> plus Layout-only ones.</summary>
            public string[] LayoutStates => LayoutStatesOf?.Invoke();
            // Read on each use: the table outlives a switch to another game.
            public Func<string[]> LayoutStatesOf { get; init; }
            /// <summary>Whether this game has a Layout-only state; null when it has all of them.</summary>
            public Func<int, bool> HasLayoutState { get; init; }
            /// <summary>The state a Layout-only state is drawn as.</summary>
            public Func<int, int> DrawnAs { get; init; }
            /// <summary>Draws the screen the shots don't show, for the Layout tab; null if there is none.</summary>
            public Func<int, Shot> OtherScreen { get; init; }
            /// <summary>Both screens for the Layout tab by state, when they differ from the text preview's, or null.</summary>
            public Func<int, (byte[] Top, byte[] Bottom)?> LayoutPictures { get; init; }
            /// <summary>Rows a background is scrolled up in a state.</summary>
            public Func<int, int, int> RowsScrolled { get; init; }
            /// <summary>The frame's corner in tiles. Windows inside the frame are placed relative to this corner.</summary>
            public Func<int, ScreenWindowLayout.Window, (int X, int Y)> Inside { get; init; }
            /// <summary>The order the game puts windows down; null keeps the table order.</summary>
            public Func<ScreenWindowLayout.Window, int> PutOrder { get; init; }
            /// <summary>Blocks of map squares that move with a window, or null.</summary>
            public Func<IReadOnlyList<ScreenSwap>> SwapsOf { get; init; }
            /// <summary>Background rows drawn behind a window, which move up and down with it, or null.</summary>
            public Func<IReadOnlyList<ScreenBand>> BandsOf { get; init; }
            /// <summary>The block the game moves a window with, which the window must stay inside; null if none.</summary>
            public Func<ScreenWindowLayout.Window, (int X, int Y, int W, int H)?> Block { get; init; }
            /// <summary>The row the game's code forces a window to in a state; null if the table decides.</summary>
            public Func<int, ScreenWindowLayout.Window, int?> FixedTop { get; init; }
            /// <summary>Windows that take turns share this name; null for the rest.</summary>
            public Func<ScreenWindowLayout.Window, string> TakesTurns { get; init; }
            /// <summary>Whether the main engine draws the top screen; null when the game's usual layout holds.</summary>
            public bool? MainOnTop { get; init; }
            /// <summary>Set where the answer differs between the games; wins over <see cref="MainOnTop"/>.</summary>
            public Func<bool> MainOnTopOf { get; init; }
            /// <summary>Tiles a window's frame adds (left, top, right, bottom); null for none.</summary>
            public Func<ScreenWindowLayout.Window, (int L, int T, int R, int B)?> Frame { get; init; }
            public bool Available => Banks().Length > 0;
        }

        private static bool Hg => GameFamily == GameFamilies.HGSS;
        private static bool Pt => GameFamily == GameFamilies.Plat;
        private static int[] Us(params int[] banks) => banks.Select(TextBank).Where(b => b >= 0).ToArray();

        public static readonly Screen[] Screens =
        {
            new() { Key = "options", Title = "Options", Banks = () => Pt ? Us(220) : Hg ? Us(45) : Array.Empty<int>(),
                    Draw = (bank, line) => Pt ? OptionsPt(line) : OptionsHg(line), FirstLine = 3,
                    OtherScreen = _ => Pt ? OptionsBottomPt() : Hg && OptionsHg(40)?.Rgba is byte[] top ? new Shot("Options graphics: top", top) : null },
            new() { Key = "mainmenu", Title = "Main menu", Banks = () => Pt ? Us(550) : Hg ? Us(442) : Array.Empty<int>(),
                    Draw = (bank, line) => MainMenu(line, Hg),
                    LayoutStatesOf = () => Pt || Hg ? new[] { "Menu" }.Concat(RomLocator.MainMenuAlertNames).ToArray() : null,
                    LayoutPictures = state => state <= 0 ? null : Hg ? (Blank(Rgb(0, 0, 0)), MainMenuAlert(state - 1, true))
                                                                    : (MainMenuAlert(state - 1, false), Blank(Rgb(0, 0, 0))),
                    Shows = (state, group, name) => group == "Alerts" ? state > 0 && name == RomLocator.MainMenuAlertNames[state - 1] : state <= 0 },
            new() { Key = "startmenu", Title = "Start menu",
                    Banks = () => Pt ? Us(367) : Hg && FieldTouchMenuTextArchive >= 0 ? new[] { FieldTouchMenuTextArchive } : Array.Empty<int>(),
                    Draw = (bank, line) => Pt ? StartMenuPt(line) : StartMenuTopHg(),
                    OtherScreen = _ => Hg && TextScreenPreviews.TouchMenuPicture(null, null) is byte[] menu ? new Shot("Touch screen graphics: bottom", menu) : null,
                    LayoutStatesOf = () => Pt ? new[] { "Menu", "In the Safari Zone" } : null,
                    LayoutPictures = state => Pt && state == 1 && StartMenuPt(0, ballCount: true)?.Rgba is byte[] top ? (top, Blank(Rgb(0, 0, 0))) : null,
                    Shows = (state, group, name) => name != "Ball count" || state == 1 },
            new() { Key = "banner", Title = "Place-name banner",
                    Banks = () => (Pt || Hg) && LocationNamesTextNumber >= 0 ? new[] { LocationNamesTextNumber } : Array.Empty<int>(),
                    Draw = (bank, line) => Banner(line, Hg), FirstLine = 1 },
            new() { Key = "naming", Title = "Naming screen", Banks = () => Pt ? Us(422) : Hg ? Us(249) : Array.Empty<int>(),
                    Draw = (bank, line) => NamingPrompt(line, Hg), MainOnTop = false, OtherScreen = state => NamingKeyboard(state),
                    // The top asks for the naming type's name: player 0, rival 3, Pokémon 1, box 2 in both games.
                    LayoutPictures = state => NamingPrompt(new[] { 0, 3, 1, 2 }[Math.Clamp(state, 0, 3)], Hg)?.Rgba is byte[] top
                                              && NamingKeyboard(state)?.Rgba is byte[] keys ? (top, keys) : null,
                    LayoutStatesOf = () => NamingTypes.Select(t => t.Name).ToArray(), Shows = (state, group, name) => true },
            new() { Key = "trainercard", Title = "Trainer card", Banks = () => Pt ? Us(616) : Hg ? Us(727) : Array.Empty<int>(),
                    Draw = (bank, line) => TrainerCard(line, Hg),
                    // HGSS shows the card on the bottom screen from the sub engine, so the main engine is the top.
                    MainOnTopOf = () => Hg, LayoutStatesOf = () => Pt ? CardSides.Append("Badges polished").ToArray() : CardSides, LayoutPictures = CardLayout,
                    Shows = (state, group, name) => group == "Pictures" || name == "Message" || Array.IndexOf(RomLocator.HgssCardWindows, name) is int i && (i >= 7) == (state == 1) },
            new() { Key = "box", Title = "PC boxes", Banks = () => Pt ? Us(19, 18) : Hg ? Us(25, 24) : Array.Empty<int>(),
                    Draw = (bank, line) => Pt ? BoxPt(bank == TextBank(19), line) : BoxHg(bank == TextBank(25), line), FirstLine = 7,
                    LayoutStatesOf = () => BoxStates, OtherScreen = state => Hg ? BoxTopHg(state) : null,
                    Inside = (state, w) => !Hg ? (0, 0) : w.Name.StartsWith("Move ", StringComparison.Ordinal) ? (20, 12)
                        : w.Name is "Item name" or "Item description" ? (0, 15) : (0, 0),
                    LayoutPictures = state => Pt ? BoxLayoutPt(state) : null,
                    // HGSS: the markings' box is drawn into map 5 at 14,18 and moves with them.
                    SwapsOf = () => Hg ? new[] { new ScreenSwap("Markings", DirNames.BoxGraphics, 5, 14, 18, 8, 2, HgMarkingsAway, null, HgPanelPlainFrom) }
                        // Platinum: the six marks sit in BG2's map 0 at 4,19 and move with the Screen positions patch's row.
                        : Pt ? new[] { new ScreenSwap("Markings", DirNames.BoxGraphics, 0, 4, 19, 6, 1, PtMarkingsAway, null, PtPanelPlainFrom) } : null,
                    Shows = BoxShows },
            new() { Key = "party", Title = "Party", Banks = () => Pt ? Us(453) : Hg ? Us(300) : Array.Empty<int>(),
                    Draw = (bank, line) => Party(line, Hg), FirstLine = 29,
                    States = PartyStates, LayoutStatesOf = () => PartyStates.Append("Menu with field moves").Append("Multi battle").ToArray(),
                    // Retail Platinum only uses the single layout, so the multi tables never show.
                    HasLayoutState = state => state != PartyMulti || Hg,
                    DrawnAs = state => state == PartyFieldMoves ? PartyMenu : state == PartyMulti ? PartyNormal : state,
                    OtherScreen = state => Hg ? PartyTop(-1, state) : PartyBalls(),
                    RowsScrolled = (state, layer) => Hg && layer >= 4 && PartyPanelUp(state) ? 5 : 0,
                    PutOrder = PartyPutOrder, Block = PartyBlock, FixedTop = PartyFixedTop,
                    TakesTurns = w => w.Name.EndsWith(" message", StringComparison.Ordinal) ? "message" : null, Frame = PartyFrame, StateOf = (bank, line) => PartyStateOf(line, Hg), Shows = (state, group, name) => PartyShows(state, group, name, Hg) },
            new() { Key = "bag", Title = "Bag", Banks = () => Pt ? Us(392, 7, 395) : Hg ? Us(222, 10) : Array.Empty<int>(),
                    Draw = (bank, line) => Pt ? BagPt(bank, line) : BagHg(bank, line), FirstLine = 1,
                    MainOnTop = true, LayoutStatesOf = () => BagStates, OtherScreen = BagOtherScreen, Shows = BagShows,
                    // HGSS draws the description's dark band into both top backdrops (54, and 9 once a TM's menu is open): rows 17 to 28,
                    // from one row above the window.
                    BandsOf = () => Hg ? new[] { new ScreenBand("Description", DirNames.BagGraphics, new[] { 9, 54 }, 1, 12) } : null,
                    SwapsOf = () => Pt && RomLocator.PtBagItemBox is { } box
                        ? new[] { new ScreenSwap("Item frame", DirNames.BagGraphics, box.PanelMap, 0, 18, 5, 5, () => PtItemBox(false), () => PtItemBox(true)) }
                        : null },
            new() { Key = "pokegear", Title = "Pokégear",
                    Banks = () => Hg ? Us(new[] { 640 }.Concat(Enumerable.Range(641, 76).Where(b => b != 668))
                                           .Concat(Enumerable.Range(409, 12)).Concat(new[] { 273, 271 }).ToArray()) : Array.Empty<int>(),
                    Draw = Pokegear, FirstLine = 1, LayoutStatesOf = () => PokegearStates, LayoutPictures = state => Hg ? PokegearLayout(state) : null,
                    Shows = (state, group, name) => group == PokegearApps[state == PokegearPhoneMenu ? 0 : Math.Clamp(state, 0, PokegearApps.Length - 1)] },
            // Layout only: the Battle Pokémon pages that show the header, by BattleMenuButtons page.
            new() { Key = "battlepokemon", Title = "Battle Pokémon", Banks = () => Array.Empty<int>(), Draw = (_, _) => null,
                    LayoutStatesOf = () => BattleHeaderPages.Select(p => p.Name).ToArray(), Shows = (_, _, _) => true,
                    LayoutPictures = state => BattleMenuPictures.PokemonPage(BattleHeaderPages[Math.Clamp(state, 0, BattleHeaderPages.Length - 1)].Page)
                        is byte[] bottom ? (TouchTabPictures.BattleTop(), bottom) : null },
            new() { Key = "title", Title = "Title screen", Banks = () => Hg ? Us(719) : Pt ? Us(609) : Array.Empty<int>(),
                    Draw = (bank, line) => line == 0 ? TitleStartLine() : null },
        };

        /// <summary>"Top screen" or "Bottom screen", for the screen a shot shows.</summary>
        public static string Caption(string shotName) =>
            shotName != null && shotName.EndsWith("top", StringComparison.Ordinal) ? "Top screen" : "Bottom screen";

        private enum Align { Left, Right, Centre }

        private static readonly object Gate = new object();
        // The edit for the previewed line, otherwise the archive's.
        private static Func<int, int, string> _line;
        private static int _stop;
        private static int _state;
        private static int _stops;
        private static bool _byItself;
        private static List<(DirNames Dir, int Member, string Kind)> _pieces = new();
        private static int _drawing = -1;
        private static List<(DirNames Dir, int Member, int Who)> _pokemonPieces = new();
        // Left out of a redraw to find the pixels a file draws.
        private static (DirNames Dir, int Member) _without = (default, -1);
        private static bool Without(DirNames dir, int member) => _without.Member == member && _without.Dir == dir;

        private static void Credit(DirNames dir, int member)
        {
            if (_drawing >= 0) _pokemonPieces.Add((dir, member, _drawing));
        }

        // The tiles of each window that later windows leave uncovered.
        private static Dictionary<string, bool[]> _shownTiles;

        /// <summary>A window's position in tiles in a state: its own, or the row the game's code forces.</summary>
        public static (int X, int Y, int W, int H) Placed(Screen screen, ScreenWindowLayout.Window w, int state)
        {
            (int ix, int iy) = screen.Inside?.Invoke(state, w) ?? (0, 0);
            return (w.X + ix, (screen.FixedTop?.Invoke(state, w) ?? w.Y) + iy, w.Width, w.Height);
        }

        /// <summary>Whether two windows can be on screen together. Windows that take turns never are.</summary>
        public static bool Together(Screen screen, ScreenWindowLayout.Window a, ScreenWindowLayout.Window b)
        {
            string turn = screen?.TakesTurns?.Invoke(a);
            return turn == null || turn != screen.TakesTurns(b);
        }

        /// <summary>
        /// Tiles of the 32 by 32 map each window keeps: a later window replaces every tile it covers, even see-through ones.
        /// </summary>
        public static Dictionary<string, bool[]> ShownTiles(Screen screen, ScreenWindowLayout layout, int state)
        {
            Dictionary<string, bool[]> shown = new Dictionary<string, bool[]>();
            if (screen == null || layout == null) return shown;
            List<ScreenWindowLayout.Window> order = layout.Windows
                .Where(w => screen.Shows == null || screen.Shows(state, w.Group, w.Name))
                .Select((w, i) => (w, i)).OrderBy(p => screen.PutOrder?.Invoke(p.w) ?? 0).ThenBy(p => p.i).Select(p => p.w).ToList();
            for (int i = 0; i < order.Count; i++)
            {
                bool[] tiles = new bool[32 * 32];
                (int x, int y, int width, int height) = Placed(screen, order[i], state);
                for (int ty = y; ty < y + height && ty < 32; ty++)
                    for (int tx = x; tx < x + width && tx < 32; tx++) tiles[ty * 32 + tx] = true;
                for (int j = i + 1; j < order.Count; j++)
                {
                    if (order[j].Layer != order[i].Layer || !Together(screen, order[i], order[j])) continue;
                    (int ox, int oy, int ow, int oh) = Placed(screen, order[j], state);
                    for (int ty = oy; ty < oy + oh && ty < 32; ty++)
                        for (int tx = ox; tx < ox + ow && tx < 32; tx++) tiles[ty * 32 + tx] = false;
                }
                shown[order[i].Name] = tiles;
            }
            return shown;
        }

        // Draws a window's contents only on tiles that no later window replaced.
        private static void InWindow(byte[] rgba, string name, Action<byte[]> draw)
        {
            if (_shownTiles == null || !_shownTiles.TryGetValue(name, out bool[] tiles)) { draw(rgba); return; }
            byte[] own = new byte[rgba.Length];
            draw(own);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int at = (y * W + x) * 4;
                    if (own[at + 3] != 0 && tiles[(y / 8) * 32 + x / 8]) Array.Copy(own, at, rgba, at, 4);
                }
        }

        /// <summary>The screens that print <paramref name="line"/> of <paramref name="bank"/>, drawn with <paramref name="text"/>.</summary>
        public static List<Shot> For(int bank, int line, string text, Func<int, int, string> archiveLine, int stop = 0)
        {
            List<Shot> shots = new List<Shot>();
            foreach (Screen screen in Screens)
            {
                if (!screen.Banks().Contains(bank)) continue;
                Shot shot = Draw(screen, bank, line, text, archiveLine, stop);
                if (shot != null) shots.Add(shot);
            }
            return shots;
        }

        /// <summary>One screen showing that line, or null if it doesn't print it.</summary>
        /// <param name="state">The state to show it in; -1 for the line's own state, or the first one.</param>
        public static Shot Draw(Screen screen, int bank, int line, string text, Func<int, int, string> archiveLine, int stop = 0, int state = -1)
        {
            if (GameLanguage == GameLanguages.Japanese || screen == null) return null;
            lock (Gate)
            {
                _state = state >= 0 ? state : Math.Max(0, screen.StateOf?.Invoke(bank, line) ?? 0);
                _forLayout = state >= 0;
                _stop = Math.Max(0, stop);
                _line = (b, l) => b == bank && l == line ? text : archiveLine(b, l);
                _stops = 1;
                _byItself = false;
                _pieces = new List<(DirNames Dir, int Member, string Kind)>();
                _pokemonPieces = new List<(DirNames Dir, int Member, int Who)>();
                _drawing = -1;
                _shownTiles = null;
                try
                {
                    Shot s = screen.Draw(bank, line);
                    if (s?.Rgba == null) return null;
                    return s with
                    {
                        Stops = _stops, MovesOnByItself = _stops > 1 && _byItself, Key = screen.Key, Pieces = _pieces.Distinct().ToList(),
                        PokemonPieces = _pokemonPieces.Distinct().ToList(),
                    };
                }
                catch (Exception ex)
                {
                    if (_without.Member < 0) AppLogger.WarnOnce("GameTextScreens." + screen.Key, ex);
                    return null;
                }
                finally { _drawing = -1; }
            }
        }

        /// <summary>
        /// The pixels each piece draws, found by redrawing without it; a piece the screen can't be drawn without gets none.
        /// </summary>
        public static bool[][] Areas(Screen screen, int bank, int line, string text, Func<int, int, string> archiveLine, int stop, int state,
                                     Shot shot, Func<bool> cancelled)
        {
            bool[][] areas = new bool[shot.Pieces.Count][];
            for (int i = 0; i < shot.Pieces.Count; i++)
            {
                if (cancelled()) return null;
                Shot without;
                lock (Gate)
                {
                    _without = (shot.Pieces[i].Dir, shot.Pieces[i].Member);
                    try { without = Draw(screen, bank, line, text, archiveLine, stop, state); }
                    finally { _without = (default, -1); }
                }
                bool[] area = new bool[W * H];
                if (without?.Rgba != null && without.Rgba.Length == shot.Rgba.Length)
                    for (int p = 0; p < area.Length; p++)
                        area[p] = shot.Rgba[p * 4] != without.Rgba[p * 4] || shot.Rgba[p * 4 + 1] != without.Rgba[p * 4 + 1]
                                  || shot.Rgba[p * 4 + 2] != without.Rgba[p * 4 + 2];
                areas[i] = area;
            }
            return areas;
        }

        // Notes which files a screen reads, so the editor can list them.
        private static Func<int, byte[]> Members(DirNames dir)
        {
            Func<int, byte[]> m = MoveTextScreens.Members(dir);
            if (m == null) return null;
            List<(DirNames, int, string)> seen = _pieces;
            return i =>
            {
                if (Without(dir, i)) return null;
                byte[] data = ScriptNarc.Staged(dir, i) is byte[] staged ? NitroBgCodec.Inflate(staged) : m(i);
                seen.Add((dir, i, KindOf(data)));
                Credit(dir, i);
                return data;
            };
        }

        private static string KindOf(byte[] data)
        {
            if (data == null || data.Length < 4) return "File";
            return System.Text.Encoding.ASCII.GetString(data, 0, 4) switch
            {
                "RGCN" => "Drawing", "RCSN" => "Arrangement", "RLCN" => "Colours", "RECN" => "Cells", "RNAN" => "Animation",
                _ => "File",
            };
        }

        // ── shared pieces ────────────────────────────────────────────────────────────

        private const int W = DsBgScreen.Width, H = DsBgScreen.Height;

        private static readonly Dictionary<int, FieldFont> Fonts = new();
        private static string _rom;

        /// <summary>Reads the fonts again on the next draw, after the font archive was saved.</summary>
        public static void ForgetFonts() => _rom = null;

        private static FieldFont Font(int entry)
        {
            if (_rom != WorkDir) { _rom = WorkDir; Fonts.Clear(); }
            if (Fonts.TryGetValue(entry, out FieldFont f)) return f;
            try { f = FieldFont.LoadFromArchive(entry); } catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.Font", ex); f = null; }
            Fonts[entry] = f;
            return f;
        }

        private static readonly Dictionary<int, List<string>> Archives = new();

        // <summary>Reads saved lines for pictures drawn outside a text preview.</summary>
        private static string ArchiveLine(int bank, int line)
        {
            if (!Archives.TryGetValue(bank, out List<string> lines))
            {
                try { lines = new TextArchive(bank).Messages; } catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.ArchiveLine", ex); lines = new List<string>(); }
                Archives[bank] = lines;
            }
            return line >= 0 && line < lines.Count ? lines[line] : "";
        }

        // Rereads the lines for each picture because the Text editor may have saved in between.
        private static T WithArchive<T>(Func<T> draw)
        {
            lock (Gate)
            {
                Archives.Clear();
                _line = ArchiveLine;
                _pieces = new List<(DirNames Dir, int Member, string Kind)>();
                try { return draw(); }
                catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.WithArchive", ex); return default; }
            }
        }

        /// <summary>A font from the font archive, for pictures drawn outside a text preview.</summary>
        internal static FieldFont FontFor(int entry)
        {
            lock (Gate) return Font(entry);
        }

        private static FieldFont SystemFont => Font(0);
        private static FieldFont TalkFont => Font(1);

        // Pt 6 and HGSS 7 are the system font's palette rows, Pt 7 and HGSS 8 the message font's.
        private static ushort[] FontColours(int member) =>
            DsBgScreen.Row(NitroBgCodec.ReadColours(MoveTextScreens.Members(DirNames.Fonts)?.Invoke(member)), 0);

        internal static ushort[] SystemColours => FontColours(GameFamily == GameFamilies.HGSS ? 7 : 6);
        internal static ushort[] TalkColours => FontColours(GameFamily == GameFamilies.HGSS ? 8 : 7);

        private static ushort Rgb(int r, int g, int b) => (ushort)(r | g << 5 | b << 10);

        private static uint Argb(ushort colour)
        {
            (byte r, byte g, byte b) = NitroBgCodec.Colour(colour);
            return 0xFF000000u | (uint)r << 16 | (uint)g << 8 | b;
        }

        /// <summary>Fills placeholders with sample words and removes markup, keeping line breaks.</summary>
        private static string Text(int usBank, int id, Func<int, int, int, string> fill = null) => Line(TextBank(usBank), id, fill);

        /// <summary>Placeholders that <paramref name="fill"/> leaves null get sample words.</summary>
        private static string Line(int bank, int id, Func<int, int, int, string> fill = null)
        {
            string raw = _line(bank, id) ?? "";
            if (fill != null) raw = FieldStringVars.Expand(raw, fill);
            string expanded = FieldStringVars.Expand(raw, SampleWord);
            expanded = expanded.Replace("\\n", "\n").Replace("\\r", "\n").Replace("\\f", "\n");
            return global::System.Text.RegularExpressions.Regex.Replace(expanded, @"\{[^}]*\}", "");
        }

        // A line read without stripping its colour codes, for PrintColoured.
        private static string Coloured(int usBank, int id)
        {
            string expanded = FieldStringVars.Expand(_line(TextBank(usBank), id) ?? "", SampleWord);
            expanded = expanded.Replace("\\n", "\n").Replace("\\r", "\n").Replace("\\f", "\n");
            return global::System.Text.RegularExpressions.Regex.Replace(expanded, @"\{(?!COLOR,)[^}]*\}", "");
        }


        private static void PrintColoured(byte[] rgba, FieldFont font, string text, int x, int y, ushort[] row)
        {
            string[] lines = (text ?? "").Split('\n');
            TextColours.State colour = TextColours.State.Start;
            for (int i = 0; i < lines.Length; i++)
            {
                int pen = x;
                foreach ((string run, int pair) in TextColours.Runs(lines[i], ref colour))
                {
                    (int ink, int shadow) = TextColours.Entries(pair);
                    DsBgScreen.DrawText(rgba, font, run, pen, y + i * 16, row[ink], row[shadow]);
                    pen += DsBgScreen.MeasureText(font, run);
                }
            }
        }

        private static string Box(int bank, int id, FieldFont font)
        {
            string expanded = FieldStringVars.Expand(_line(bank, id) ?? "", SampleWord);
            List<FieldMessageFrame> frames = FieldMessageScript.Frames(expanded, t => DsBgScreen.MeasureText(font, t));
            _stops = Math.Max(_stops, frames.Count);
            string first = frames.Count > 0 ? string.Join("\n", frames[_stop % frames.Count].Lines) : "";
            return global::System.Text.RegularExpressions.Regex.Replace(first, @"\{[^}]*\}", "");
        }

        private static string BoxText(int usBank, int id, FieldFont font) => Box(TextBank(usBank), id, font);

        // The item named in a screen's messages, such as the item picked in the bag, or 0 for the generic word.
        [ThreadStatic] private static int _sampleItem;

        public static string SampleWord(int family, int kind, int buffer)
        {
            if (kind == 8 && _sampleItem > 0) return Text(392, _sampleItem);
            string word = FieldStringVars.SuggestFor(kind, buffer, null);
            if (!word.StartsWith("strvar", StringComparison.Ordinal)) return word;
            // Month names are family 34 in HGSS; any other unknown placeholder shows as a dash.
            return kind == 74 || family == 34 ? "Jan." : "-";
        }

        private static void Print(byte[] rgba, FieldFont font, string text, int x, int y, ushort ink, ushort shadow,
                                  Align align = Align.Left)
        {
            string[] lines = (text ?? "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int w = DsBgScreen.MeasureText(font, lines[i]);
                int left = align == Align.Right ? x - w : align == Align.Centre ? x - w / 2 : x;
                DsBgScreen.DrawText(rgba, font, lines[i], left, y + i * 16, ink, shadow);
            }
        }

        private static void Fill(byte[] rgba, ushort colour, int x = 0, int y = 0, int w = W, int h = H)
        {
            (byte r, byte g, byte b) = NitroBgCodec.Colour(colour);
            for (int yy = Math.Max(0, y); yy < Math.Min(H, y + h); yy++)
                for (int xx = Math.Max(0, x); xx < Math.Min(W, x + w); xx++)
                {
                    int at = (yy * W + xx) * 4;
                    rgba[at] = r; rgba[at + 1] = g; rgba[at + 2] = b; rgba[at + 3] = 255;
                }
        }

        private static byte[] Blank(ushort colour)
        {
            byte[] rgba = new byte[W * H * 4];
            Fill(rgba, colour);
            return rgba;
        }

        // The preview can't know the field behind a field window.
        private static ushort FieldBackdrop => Rgb(6, 7, 8);

        private static void Blit(byte[] rgba, byte[] piece, int pw, int ph, int left, int top)
        {
            if (piece == null) return;
            for (int y = 0; y < ph; y++)
                for (int x = 0; x < pw; x++)
                {
                    int sx = left + x, sy = top + y;
                    if (sx < 0 || sy < 0 || sx >= W || sy >= H) continue;
                    int from = (y * pw + x) * 4;
                    if (piece[from + 3] == 0) continue;
                    Array.Copy(piece, from, rgba, (sy * W + sx) * 4, 4);
                }
        }

        private static void StandardWindow(byte[] rgba, int x, int y, int w, int h, ushort paper) => StandardWindow(rgba, x, y, w, h, paper, null);

        // pokeplatinum start_menu.c: the field menus use frame type 1 (STANDARD_WINDOW_FIELD).
        private static FieldWindowFrame FieldFrame() => WindowFrames.LoadStandard(1);

        private static void StandardWindow(byte[] rgba, int x, int y, int w, int h, ushort paper, Func<FieldWindowFrame> pick)
        {
            (byte r, byte g, byte b) = NitroBgCodec.Colour(paper);
            FieldWindowFrame frame = pick != null ? pick() : WindowFrames.LoadStandard();
            if (frame == null) { Fill(rgba, paper, x * 8, y * 8, w * 8, h * 8); return; }
            byte[] piece = frame.ComposeStandard(w, h, out int pw, out int ph, 0xFF000000u | (uint)r << 16 | (uint)g << 8 | b);
            Blit(rgba, piece, pw, ph, (x - 1) * 8, (y - 1) * 8);
        }

        /// <summary>Two tiles left, three right, one above and below, in message frame type <paramref name="type"/>.</summary>
        internal static void MessageWindow(byte[] rgba, int x, int y, int w, int h, ushort paper, int type = 0)
        {
            FieldWindowFrame frame = WindowFrames.Load(type);
            if (frame == null)
            {
                Fill(rgba, paper, x * 8 - 1, y * 8 - 1, w * 8 + 2, h * 8 + 2);
                return;
            }
            (byte r, byte g, byte b) = NitroBgCodec.Colour(paper);
            byte[] piece = frame.Compose(w, h, out int pw, out int ph, 0xFF000000u | (uint)r << 16 | (uint)g << 8 | b);
            Blit(rgba, piece, pw, ph, (x - 2) * 8, (y - 1) * 8);
        }

        private static (DsBgScreen Screen, Func<int, byte[]> Member) Bg(DirNames dir)
        {
            Func<int, byte[]> m = Members(dir);
            return m == null ? (null, null) : (new DsBgScreen(), m);
        }

        private static void Layer(DsBgScreen s, Func<int, byte[]> m, int bg, int priority, int charBase, int ncgr, int nscr, int rowsDown = 0)
        {
            s.InitLayer(bg, priority, charBase);
            if (ncgr >= 0) s.LoadTiles(charBase, m(ncgr));
            if (nscr < 0) return;
            (int width, ushort[] entries) = NitroBgCodec.ReadMap(m(nscr));
            if (rowsDown == 0) { s.LoadMap(bg, entries, DsBgScreen.MapSide); return; }
            int wide = Math.Max(1, Math.Min(width, DsBgScreen.MapSide));
            s.Copy(bg, 0, rowsDown, wide, DsBgScreen.MapSide - rowsDown, entries, 0, 0, DsBgScreen.MapSide);
        }

        private static void Palettes(DsBgScreen s, ushort[] colours, int firstSlot = 0, int rows = 16) => s.SetPalettes(colours, firstSlot, rows);

        // With the bottom screen palettes patch, the layer's drawing has 256-colour tiles that read the patch's palettes.
        // gameRows: rows the game copies over the first 16 colours of extended palettes 0 and up while the screen is open.
        internal static void ExtendedColours(DsBgScreen s, Func<int, byte[]> m, int bg, BottomPalettesPatch.Screen screen, int load = 0,
                                             IReadOnlyList<ushort[]> gameRows = null)
        {
            int member = screen == null ? -1 : BottomPalettesPatch.PaletteMember(screen, load);
            if (member < 0) return;
            ushort[] palettes = NitroBgCodec.ReadColours(m(member));
            for (int r = 0; gameRows != null && r < gameRows.Count; r++)
                Array.Copy(gameRows[r], 0, palettes, r * BottomPalettesPatch.PaletteSize, Math.Min(16, Math.Min(gameRows[r].Length, palettes.Length - r * BottomPalettesPatch.PaletteSize)));
            s.UseExtendedPalettes(bg, palettes);
        }

        // ── options ──────────────────────────────────────────────────────────────────

        // pokeplatinum src/applications/options_menu.c: rows of label, choices and description, by message id.
        private static readonly (int Label, int[] Choices, int About)[] PtOptionRows =
        {
            (3, new[] { 10, 11, 12 }, 43), (6, new[] { 17, 18 }, 46), (4, new[] { 13, 14 }, 44),
            (5, new[] { 15, 16 }, 45), (7, new[] { 19, 20, 21 }, 47), (8, new[] { 22 }, 48), (42, Array.Empty<int>(), 52),
        };

        private static Shot OptionsPt(int line)
        {
            const int bank = 220;
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.OptionsGraphics);
            if (s == null) return null;

            int row = Array.FindIndex(PtOptionRows, r => r.Label == line || r.About == line || r.Choices.Contains(line));
            if (line >= 22 && line <= 41) row = 5;
            if (row < 0) row = 0;
            // With the Options patch, rows sit at their slots, locked rows show one choice and text speed lists the patch's names.
            OptionsMenuPatch.ScreenLayout patch = OptionsLayout();
            int Slot(int r) => patch == null ? r : patch.Slots[r];
            if (Slot(row) < 0) row = 0;

            Palettes(s, NitroBgCodec.ReadColours(m(0)), 0, 1);
            s.InitLayer(2, 3, 0);
            s.LoadTiles(0, m(1));
            s.Fill(2, 1, 0, 0, 32, 32, 0);
            // The cursor bar is the arrangement's top two rows, scrolled to the cursor's row inside the entries.
            (int entriesX, int entriesY, int _, int _) = ScreenWindowLayouts.Window("options", "Text", "Entries", (1, 3, 30, 14));
            s.InitLayer(0, 1, 0);
            s.Copy(0, 0, entriesY + Slot(row) * 2, 32, 2, NitroBgCodec.ReadMap(m(2)).Entries, 0, 0, 32);
            byte[] rgba = s.Render();

            ushort[] sys = SystemColours, talk = TalkColours;
            // The title, the description box and the save yes/no move in the Layout tab (RomLocator.OptionsCodeSitesIn).
            (int titleX, int titleY, int _, int _) = ScreenWindowLayouts.Window("options", "Text", "Title", (1, 0, 12, 2));
            (int aboutX, int aboutY, int _, int _) = ScreenWindowLayouts.Window("options", "Text", "Description", (2, 19, 27, 4));
            (int yesX, int yesY, int _, int _) = ScreenWindowLayouts.Window("options", "Save yes/no", (25, 13, 6, 4));
            Print(rgba, SystemFont, Text(bank, 0), titleX * 8 + 2, titleY * 8 + 2, sys[1], sys[2]);
            StandardWindow(rgba, entriesX, entriesY, 30, 14, sys[15]);
            for (int r = 0; r < PtOptionRows.Length; r++)
            {
                if (Slot(r) < 0) continue;
                (int label, int[] choices, int _) = PtOptionRows[r];
                int y = entriesY * 8 + 16 * Slot(r);
                Print(rgba, SystemFont, Text(bank, label), entriesX * 8 + 4, y, sys[1], sys[2]);
                int locked = patch == null || r > 5 ? -1 : patch.Locks[r];
                string[] words = r == 5 ? new[] { Text(bank, line >= 22 && line <= 41 ? line : 22) }
                    : r == 0 && patch != null ? (patch.Arrows ? new[] { patch.SpeedNames[0] } : patch.SpeedNames.ToArray())
                    : locked >= 0 ? new[] { Text(bank, choices[locked]) }
                    : choices.Select(c => Text(bank, c)).ToArray();
                int x = 100;
                for (int i = 0; i < words.Length; i++)
                {
                    bool picked = r == 5 || locked >= 0 || (r == 0 && patch?.Arrows == true)
                                  || (i < choices.Length && choices[i] == line) || (!choices.Contains(line) && i == 0);
                    int at = r == 5 || (r == 0 && patch?.Arrows == true) ? 148 : r == 4 ? x
                           : 100 + 48 * (locked >= 0 ? locked : i);
                    Print(rgba, SystemFont, words[i], entriesX * 8 + at, y, picked ? sys[3] : sys[1], picked ? sys[4] : sys[2]);
                    x += DsBgScreen.MeasureText(SystemFont, words[i]) + 12;
                }
            }
            // options_menu.c OptionsMenuVBlank redraws the box in the frame type picked.
            MessageWindow(rgba, aboutX, aboutY, 27, 4, talk[15], line >= 22 && line <= 41 ? line - 22 : 0);
            int about = line == 49 ? 49 : PtOptionRows[row].About;
            Print(rgba, TalkFont, BoxText(bank, about, TalkFont), aboutX * 8 + 4, aboutY * 8, talk[1], talk[2]);
            // Leaving with changes asks to save them: sConfirmationWindowTemplate, 25,13, 6 by 4, with the common YES and NO.
            if (line == 49 && RomInfo.FieldMenuEntriesArchive >= 0 && RomInfo.FieldYesEntry >= 0)
            {
                StandardWindow(rgba, yesX, yesY, 6, 4, sys[15]);
                Print(rgba, SystemFont, Line(RomInfo.FieldMenuEntriesArchive, RomInfo.FieldYesEntry), yesX * 8 + 8, yesY * 8, sys[1], sys[2]);
                Print(rgba, SystemFont, Line(RomInfo.FieldMenuEntriesArchive, RomInfo.FieldNoEntry), yesX * 8 + 8, yesY * 8 + 16, sys[1], sys[2]);
            }
            return new Shot("Options graphics: top", rgba);
        }

        // options_menu.c fills the bottom screen with square 1 of the options drawing, as it does the top's backdrop.
        private static Shot OptionsBottomPt()
        {
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.OptionsGraphics);
            if (s == null) return null;
            Palettes(s, NitroBgCodec.ReadColours(m(0)), 0, 1);
            s.InitLayer(2, 3, 0);
            s.LoadTiles(0, m(1));
            s.Fill(2, 1, 0, 0, 32, 32, 0);
            return new Shot("Options graphics: bottom", s.Render());
        }

        // pokeheartgold src/options_app.c: the label, the choices and the x each is centred on.
        private static readonly (int Label, int[] Choices, int[] Xs)[] HgOptionRows =
        {
            (1, new[] { 9, 10, 11 }, new[] { 124, 172, 220 }), (2, new[] { 12, 13 }, new[] { 124, 172 }),
            (3, new[] { 14, 15 }, new[] { 132, 212 }), (4, new[] { 16, 17 }, new[] { 132, 212 }),
            (5, new[] { 18, 19 }, new[] { 132, 212 }), (6, new[] { 20 }, new[] { 172 }),
        };

        /// <param name="screen">Edited background screen 17, or null for the saved one.</param>
        /// <param name="labelXs">Choice label x positions, one array per row.</param>
        /// <param name="quit">Quit window position in tiles.</param>
        /// <param name="confirm">Confirm window position in tiles.</param>
        private static Shot OptionsHg(int line, byte[] screen = null, int[][] labelXs = null, (int X, int Y)? quit = null, (int X, int Y)? confirm = null)
        {
            const int bank = 45;
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.OptionsGraphics);
            if (s == null) return null;

            // Top: the frame type box, drawn over the list.
            DsBgScreen top = new DsBgScreen();
            Palettes(top, NitroBgCodec.ReadColours(m(3)), 0, 2);
            Layer(top, m, 0, 1, 0, 8, 19);
            byte[] upper = top.Render();
            ushort[] talk = TalkColours;
            // The test window shows the frame type picked, and its text names it.
            int type = line >= 20 && line <= 39 ? line - 20 : line >= 40 && line <= 59 ? line - 40 : 0;
            (int testX, int testY, int _, int _) = ScreenWindowLayouts.Window("options", "Text", "Text speed test", (2, 19, 27, 4));
            MessageWindow(upper, testX, testY, 27, 4, talk[15], type);
            int frameLine = 40 + type;
            Print(upper, TalkFont, BoxText(bank, frameLine, TalkFont), testX * 8 + 4, testY * 8, talk[1], talk[2]);

            Palettes(s, NitroBgCodec.ReadColours(m(2)), 0, 2);
            s.SetPalette(13, SystemColours);
            // The Preview shows the patched screen. The Buttons tab passes its own screen, drawn as it is.
            OptionsMenuPatch.ScreenLayout patch = screen == null ? OptionsLayout() : null;
            byte[] shownMap = screen ?? (patch != null ? _optionsShown?.Map : null);
            Layer(s, m, 2, 2, 0, 7, shownMap != null ? -1 : patch != null ? OptionsMenuPatch.MapMember() : 17);
            if (shownMap != null) s.LoadMap(2, NitroBgCodec.ReadMap(shownMap).Entries, DsBgScreen.MapSide);
            s.InitLayer(0, 0, 0);
            s.Copy(0, 0, 1, 32, 7, NitroBgCodec.ReadMap(m(18)).Entries, 0, 0, 32);
            byte[] lower = s.Render();
            ushort[] sys = SystemColours;
            (int cx, int cy) = confirm ?? (15, 21);
            (int qx, int qy) = quit ?? (24, 21);
            // With the Options patch, rows sit at their slots, locked rows show one choice and text speed lists the patch's names.
            OptionsSpritesHg(lower, m, line, cx, cy, qx, qy, patch);
            (int _, int titleY, int _, int _) = ScreenWindowLayouts.Window("options", "Text", "Title", (1, 0, 12, 3));
            Print(lower, SystemFont, Text(bank, 0), 8 + 2, titleY * 8 + 5, sys[15], sys[2]);
            for (int r = 0; r < HgOptionRows.Length; r++)
            {
                int slot = patch == null ? r : patch.Slots[r];
                if (slot < 0) continue;
                (int label, int[] choices, int[] xs) = HgOptionRows[r];
                if (labelXs != null && r < labelXs.Length) xs = labelXs[r];
                int y = 24 + 24 * slot + 5;
                Print(lower, SystemFont, Text(bank, label), 8 + 4, y, sys[15], sys[2]);
                int locked = patch == null ? -1 : patch.Locks[r];
                bool arrows = r == 0 && patch?.Arrows == true;
                string[] words = r == 5 ? new[] { Text(bank, line >= 20 && line <= 39 ? line : 20) }
                    : r == 0 && patch != null ? (arrows ? new[] { patch.SpeedNames[0] } : patch.SpeedNames.Take(3).ToArray())
                    : locked >= 0 ? new[] { Text(bank, choices[locked]) }
                    : choices.Select(c => Text(bank, c)).ToArray();
                for (int i = 0; i < words.Length; i++)
                {
                    bool picked = r == 5 || arrows || locked >= 0 || (i < choices.Length && choices[i] == line) || (!choices.Contains(line) && i == 0);
                    int x = arrows ? HgOptionRows[5].Xs[0] : xs[locked >= 0 ? locked : i];
                    Print(lower, SystemFont, words[i], 8 + x, y, picked ? sys[1] : sys[15], sys[2], Align.Centre);
                }
            }
            // OptionsApp_SetupWindows puts Confirm's 7-tile window at 15,21 and Quit's at 24,21.
            Print(lower, SystemFont, Text(bank, 7), cx * 8 + 28, cy * 8 + 6, sys[15], sys[2], Align.Centre);
            Print(lower, SystemFont, Text(bank, 8), qx * 8 + 28, qy * 8 + 6, sys[15], sys[2], Align.Centre);

            bool onTop = line >= 40 && line <= 59;
            return new Shot($"Options graphics: {(onTop ? "top" : "bottom")}", onTop ? upper : lower);
        }

        /// <summary>The Extra tab's unsaved Options settings: the layout, and on HGSS the screen built from it.</summary>
        public sealed record OptionsShown(OptionsMenuPatch.ScreenLayout Layout, byte[] Map);

        [ThreadStatic] private static OptionsShown _optionsShown;

        /// <summary>Options previews drawn on this thread show these until the result is disposed.</summary>
        public static IDisposable UseOptions(OptionsShown shown)
        {
            OptionsShown before = _optionsShown;
            _optionsShown = shown;
            return new OnDispose(() => _optionsShown = before);
        }

        private static OptionsMenuPatch.ScreenLayout OptionsLayout() => _optionsShown?.Layout ?? OptionsMenuPatch.Layout();

        // options_app.c sActiveButtonXCoords: the highlight's x for each value of the first five rows.
        private static readonly int[][] HgOptionHighlightXs =
        {
            new[] { 112, 160, 208 }, new[] { 112, 160, 208 }, new[] { 112, 192 }, new[] { 112, 192 }, new[] { 112, 192 },
        };

        // options_app.c ov54_021E6EAC with resource list 77: the highlights (set 0: drawing 4, colours 0, cells 9, animations 13)
        // at y 24 + 24 per row, the frame arrows (sets 1 and 2) at 115,144 and 213,144, and Confirm and Quit (set 3: drawing 6,
        // colours 1, cells 12, animations 16) at 116,170 and 188,170, which go with their windows.
        private static void OptionsSpritesHg(byte[] rgba, Func<int, byte[]> m, int line, int cx, int cy, int qx, int qy,
                                             OptionsMenuPatch.ScreenLayout patch = null)
        {
            ushort[] colours = NitroBgCodec.ReadColours(m(0)), buttons = NitroBgCodec.ReadColours(m(1));
            if (colours == null) return;
            int Slot(int r) => patch == null ? r : patch.Slots[r];
            for (int r = 0; r < HgOptionHighlightXs.Length; r++)
            {
                if (Slot(r) < 0 || (r == 0 && patch?.Arrows == true)) continue;
                int value = patch != null && patch.Locks[r] >= 0 ? patch.Locks[r] : Math.Max(0, Array.IndexOf(HgOptionRows[r].Choices, line));
                // The two-choice rows use the wider highlight, animation 1.
                Sprite(rgba, m, 13, 9, 4, HgOptionHighlightXs[r][Math.Min(value, HgOptionHighlightXs[r].Length - 1)], 24 + 24 * Slot(r), r >= 2 ? 1 : 0,
                       row => DsBgScreen.Row(colours, row));
            }
            if (Slot(5) >= 0)
            {
                Sprite(rgba, m, 14, 10, 5, 115, 24 + 24 * Slot(5), 0, row => DsBgScreen.Row(colours, row));
                Sprite(rgba, m, 15, 11, 5, 213, 24 + 24 * Slot(5), 0, row => DsBgScreen.Row(colours, row));
            }
            // The text speed row's own arrows when it lists more than three speeds.
            if (patch?.Arrows == true)
            {
                Sprite(rgba, m, 14, 10, 5, 115, 24, 0, row => DsBgScreen.Row(colours, row));
                Sprite(rgba, m, 15, 11, 5, 213, 24, 0, row => DsBgScreen.Row(colours, row));
            }
            if (buttons == null) return;
            Sprite(rgba, m, 16, 12, 6, 116 + (cx - 15) * 8, 170 + (cy - 21) * 8, 0, row => DsBgScreen.Row(buttons, row));
            Sprite(rgba, m, 16, 12, 6, 188 + (qx - 24) * 8, 170 + (qy - 21) * 8, 0, row => DsBgScreen.Row(buttons, row));
        }

        // ── main menu ────────────────────────────────────────────────────────────────

        // main_menu.c sMainMenuAlerts (Pt, bank 14) and ov74_0223BC30 (HGSS, bank 17) on a cleared screen: a standard frame and the
        // message in the system font from 0,0. HGSS centres its warning 4 down, with Begin adventure and Return under it.
        private static readonly int[] AlertLines = { 1, 3, 0, 2, 5 };

        private static byte[] MainMenuAlert(int alert, bool hg)
        {
            byte[] rgba = Blank(Rgb(12, 12, 31));
            ushort[] sys = SystemColours;
            (int x, int y, int w, int h) = ScreenWindowLayouts.Window("mainmenu", "Alerts", RomLocator.MainMenuAlertNames[alert], (4, 2, 24, 20));
            string text = Coloured(hg ? 17 : 14, AlertLines[alert]);
            // The HGSS warning has no frame, only its white fill.
            bool warning = hg && alert == 4;
            if (warning) Fill(rgba, sys[15], x * 8, y * 8, w * 8, h * 8);
            else StandardWindow(rgba, x, y, w, h, sys[15]);
            if (warning) PrintColoured(rgba, SystemFont, text, x * 8 + (w * 8 - TextColours.WithoutCodes(text).Split('\n').Max(l => DsBgScreen.MeasureText(SystemFont, l))) / 2, y * 8 + 4, sys);
            else PrintColoured(rgba, SystemFont, text, x * 8, y * 8, sys);
            if (warning)
                for (int i = 0; i < 2; i++)
                {
                    // main_menu.c ov74_02228548: the chosen button (Begin adventure) in frame 1 with the menu's green cycle on white,
                    // the other in frame 0 on the unselected grey.
                    (int bx, int by) = (4, 14 + 5 * i);
                    if (i == 0) StandardWindow(rgba, bx, by, 24, 3, sys[15], () => WindowFrames.LoadStandard(1)?.WithColour(6, Argb(Rgb(1, 28, 20))));
                    else StandardWindow(rgba, bx, by, 24, 3, Rgb(26, 26, 26));
                    Print(rgba, SystemFont, Text(17, 6 + i), bx * 8, by * 8 + 4, sys[1], sys[2]);
                }
            return rgba;
        }

        private static Shot MainMenu(int line, bool hg)
        {
            int bank = hg ? 442 : 550;
            // pokeplatinum src/main_menu/main_menu.c, pokeheartgold src/main_menu.c.
            int width = hg ? 23 : 26, others = hg ? 4 : 2;
            int[] labels = hg ? new[] { 13, 14, 16, 15 } : new[] { 12, 13, 15, 14 };
            int[] values = hg ? new[] { 17, 18, 20, 19 } : new[] { 16, 17, 19, 18 };
            int valueRight = (hg ? 184 : 208) - 32;

            byte[] rgba = Blank(Rgb(12, 12, 31));
            ushort[] sys = SystemColours;
            ushort grey = Rgb(26, 26, 26);

            // CONTINUE is selected, so its window is white and the others grey. Both games frame the selected button with frame type 1
            // and cycle its colour 6 from RGB(1, 28, 20): pokeplatinum main_menu.c DoColorCycleStep, HGSS ov74_0222841C.
            Func<FieldWindowFrame> chosen = () => WindowFrames.LoadStandard(1)?.WithColour(6, Argb(Rgb(1, 28, 20)));
            StandardWindow(rgba, 3, 1, width, 10, sys[15], chosen);
            Print(rgba, SystemFont, Text(bank, 0), 24, 8, sys[1], sys[2]);
            for (int i = 0; i < 4; i++)
            {
                int y = 8 + 16 * (i + 1);
                Print(rgba, SystemFont, Text(bank, labels[i]), 24 + 32, y, sys[7], sys[8]);
                // The time's minutes are padded with zeros (BufferIntegerAsString, PRINTING_MODE_LEADING_ZEROS), so 0:00.
                string value = i == 1 ? FieldStringVars.Expand(_line(bank, values[i]) ?? "", (_, _, buffer) => buffer == 1 ? "00" : "0") : Text(bank, values[i]);
                Print(rgba, SystemFont, value, 24 + valueRight, y, sys[7], sys[8], Align.Right);
            }
            List<int> titles = new List<int> { 1 };
            bool inContinue = line == 0 || labels.Contains(line) || values.Contains(line);
            if (!inContinue && line != 1) titles.Add(line);
            if (hg && !titles.Contains(9)) titles.Add(9);
            // main_menu.c sMainMenuButtons: Nintendo WFC Settings always follows, below the screen.
            if (hg && !titles.Contains(12)) titles.Add(12);
            // pokeplatinum always shows Nintendo WFC Settings and Wii Message Settings too.
            if (!hg && !titles.Contains(11)) titles.Add(11);
            if (!hg && !titles.Contains(20)) titles.Add(20);
            int top = 13;
            bool below = false;
            List<(int Row, int Kind)> wireless = new List<(int, int)>();
            foreach (int title in titles)
            {
                if (top + others > 24) { below = true; break; }
                StandardWindow(rgba, 3, top, width, others, grey);
                Print(rgba, SystemFont, Text(bank, title), 24, top * 8, sys[1], sys[2]);
                int wifi = hg ? 12 : 11;
                if (title == 3 || title == wifi || title == (hg ? 11 : 10)) wireless.Add((top, title == wifi ? 2 : 1));
                top += others + 2;
            }
            if (wireless.Count > 0) rgba = WirelessIcons(rgba, wireless, hg);
            if (!hg && Members(DirNames.MainMenuGraphics) is Func<int, byte[]> mystery)
            {
                // pokeplatinum LoadScrollArrowsSprites: graphic/mystery 41 to 43 in colours 40, at 128,8 and 128,184, shown only when
                // options lie past that edge.
                Func<int, byte[]> m = i => GraphicAssets.Unsqueeze(mystery(i));
                if (below) PartySprite(rgba, m, 41, 42, 43, 128, 184, 1, -1, 40);
            }
            if (hg)
            {
                // main_menu.c: the arrows are sprites (a/1/1/3 drawing 47, colours 44, cells 46, animations 45) at 236,20 and 236,172.
                // MainMenu_UpdateArrowSprites lights one (sequence 2 or 3) when options lie past that edge.
                Func<int, byte[]> archive = Members(DirNames.MainMenuGraphics);
                if (archive != null)
                {
                    Func<int, byte[]> m = i => GraphicAssets.Unsqueeze(archive(i));
                    // The Layout tab moves each arrow (RomLocator.MainMenuCodeSitesIn).
                    (int ux, int uy, int _, int _) = ScreenWindowLayouts.Window("mainmenu", "Pictures", "Up arrow", (28, 1, 3, 3));
                    (int dx, int dy, int _, int _) = ScreenWindowLayouts.Window("mainmenu", "Pictures", "Down arrow", (28, 20, 3, 3));
                    PartySprite(rgba, m, 45, 46, 47, ux * 8 + 12, uy * 8 + 12, 0, -1, 44);
                    PartySprite(rgba, m, 45, 46, 47, dx * 8 + 12, dy * 8 + 12, below ? 3 : 1, -1, 44);
                }
            }
            return new Shot($"Main menu graphics: {(hg ? "bottom" : "top")}", rgba);
        }

        // main_menu.c DrawWirelessIcon: Ranger and Wii (kind 1) or Nintendo WFC (kind 2), 2 by 2 tiles on BG2 at the button's top row,
        // in colour row 4. HGSS: a/1/1/3 drawing 48 (colours 49), column 23, the WFC icon 4 tiles on. Platinum: graphic/mystery 44
        // (colours 45), column 26, an 8-tile-wide sheet with the WFC icon 2 tiles on.
        private static byte[] WirelessIcons(byte[] rgba, List<(int Row, int Kind)> icons, bool hg)
        {
            Func<int, byte[]> archive = Members(DirNames.MainMenuGraphics);
            if (archive == null) return rgba;
            ushort[] colours = NitroBgCodec.ReadColours(GraphicAssets.Unsqueeze(archive(hg ? 49 : 45)));
            if (colours == null) return rgba;
            DsBgScreen s = new DsBgScreen();
            s.SetPalette(4, colours);
            s.InitLayer(2, 0, 0);
            // One tile in, so the empty map's tile 0 stays blank.
            s.LoadTiles(0, GraphicAssets.Unsqueeze(archive(hg ? 48 : 44)), 1);
            int below = hg ? 2 : 8;
            foreach ((int row, int kind) in icons)
            {
                int first = 4 << 12 | 1 + (kind == 2 ? (hg ? 4 : 2) : 0);
                ushort[] tiles = { (ushort)first, (ushort)(first + 1), (ushort)(first + below), (ushort)(first + below + 1) };
                s.Copy(2, hg ? 23 : 26, row, 2, 2, tiles, 0, 0, 2);
            }
            return s.Render(rgba);
        }

        // pokeheartgold ov74_0222779C centres the warning in a 32 by 11 window and gives Begin adventure and Return 24 by 3 windows.
        private static Shot NewGameWarningHg((int X, int Y) begin, (int X, int Y) back)
        {
            byte[] rgba = Blank(Rgb(12, 12, 31));
            ushort[] sys = SystemColours;
            StandardWindow(rgba, 0, 1, 32, 11, sys[15]);
            string warning = Text(17, 5);
            int widest = warning.Split('\n').Max(l => DsBgScreen.MeasureText(SystemFont, l));
            Print(rgba, SystemFont, warning, (256 - widest) / 2, 12, sys[1], sys[2]);
            (int X, int Y)[] spots = { begin, back };
            for (int i = 0; i < 2; i++)
            {
                StandardWindow(rgba, spots[i].X, spots[i].Y, 24, 3, i == 0 ? sys[15] : Rgb(26, 26, 26));
                Print(rgba, SystemFont, Text(17, 6 + i), spots[i].X * 8, spots[i].Y * 8 + 4, sys[1], sys[2]);
            }
            return new Shot("Main menu graphics: bottom", rgba);
        }

        // ── place-name banner ────────────────────────────────────────────────────────

        private static Dictionary<int, int> _bannerStyles, _bannerUses;
        private static string _bannerRom;

        // Maps with no banner use style 0.
        private static int BannerStyle(int name, bool hg)
        {
            if (_bannerStyles == null || _bannerRom != WorkDir)
            {
                _bannerRom = WorkDir;
                _bannerStyles = new Dictionary<int, int>();
                _bannerUses = new Dictionary<int, int>();
                int count = MapHeader.GetHeaderCount();
                for (ushort i = 0; i < count; i++)
                {
                    MapHeader h;
                    try { h = MapHeader.GetMapHeader(i); } catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.Header", ex); continue; }
                    (int place, int style) = h switch
                    {
                        HeaderPt p => (p.LocationName, p.AreaIcon),
                        HeaderHGSS g => (g.LocationName, g.AreaIcon & 0xF),
                        _ => (-1, 0),
                    };
                    if (place >= 0 && style > 0 && !_bannerStyles.ContainsKey(place)) _bannerStyles[place] = style;
                    if (place >= 0 && style > 0) _bannerUses[place] = _bannerUses.GetValueOrDefault(place) + 1;
                }
            }
            return _bannerStyles.TryGetValue(name, out int found) ? found : 0;
        }

        [ThreadStatic] private static (PlaceNameBanner.Look Saved, PlaceNameBanner.Look Shown)? _usingBanner;
        [ThreadStatic] private static (int Scroll, int Level, BannerMotionPatch.Effect Effect) _bannerStep;

        /// <summary>Draws the banner with unsaved values at one step of the slide until disposed.</summary>
        public static IDisposable UseBanner(PlaceNameBanner.Look saved, PlaceNameBanner.Look shown, int scroll = 0, int level = 0,
                                            BannerMotionPatch.Effect effect = BannerMotionPatch.Effect.None)
        {
            (PlaceNameBanner.Look, PlaceNameBanner.Look)? before = _usingBanner;
            (int, int, BannerMotionPatch.Effect) step = _bannerStep;
            _usingBanner = (saved, shown);
            _bannerStep = (scroll, level, effect);
            return new OnDispose(() => { _usingBanner = before; _bannerStep = step; });
        }

        public static Shot BannerOf(int line) => Banner(line, Hg);

        /// <summary>The place name most maps show on entry. HGSS lists Sinnoh's names first, and its maps don't use them.</summary>
        public static int CommonBannerPlace()
        {
            lock (Gate)
            {
                BannerStyle(0, Hg);
                return _bannerUses.Count == 0 ? 1 : _bannerUses.OrderByDescending(u => u.Value).ThenBy(u => u.Key).First().Key;
            }
        }

        private static Shot Banner(int line, bool hg)
        {
            // So a name no map shows on entry can still be previewed.
            int style = Math.Max(1, BannerStyle(line, hg));
            Func<int, byte[]> m = Members(DirNames.AreaWindowGraphics);
            if (m == null) return null;
            // Platinum's banner uses tiles 0 to 84, 17 a row (map_name_popup.c); HGSS uses tiles 1 to 68 (draw_map_name.c).
            PlaceNameBanner.Look game = new PlaceNameBanner.Look(0, 0, 17, hg ? 4 : 5, 60, 4, 4);
            PlaceNameBanner.Look onDisk = _usingBanner == null ? PlaceNameBanner.Read()?.Current ?? game : null;
            (PlaceNameBanner.Look saved, PlaceNameBanner.Look look) = _usingBanner ?? (onDisk, onDisk);
            int first = hg ? 1 : 0, cols = look.Width, count = look.Width * look.Height;
            byte[] sheet = m(2 * (style - 1));
            if (sheet == null) return null;
            if (saved.Width != look.Width || saved.Height != look.Height)
                sheet = PlaceNameBanner.Stretch(sheet, first, saved.Width, saved.Height, look.Width, look.Height) ?? sheet;
            byte[] tiles = NitroBgCodec.ReadCharacters(sheet);
            ushort[] colours = DsBgScreen.Row(NitroBgCodec.ReadColours(m(2 * (style - 1) + 1)), 0);

            byte[] rgba = new byte[W * H * 4];
            int left = look.Across * 8, top = look.Down * 8;
            for (int i = 0; i < count; i++)
            {
                int tile = first + i, px = left + (i % cols) * 8, py = top + (i / cols) * 8;
                for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                    {
                        int at = tile * 32 + (y * 8 + x) / 2;
                        if (at >= tiles.Length || px + x >= W || py + y >= H || py + y < 0) continue;
                        int index = ((y * 8 + x) & 1) != 0 ? tiles[at] >> 4 : tiles[at] & 0xF;
                        if (index == 0) continue;
                        (byte r, byte g, byte b) = NitroBgCodec.Colour(colours[index]);
                        int to = ((py + y) * W + px + x) * 4;
                        rgba[to] = r; rgba[to + 1] = g; rgba[to + 2] = b; rgba[to + 3] = 255;
                    }
            }
            string name = Line(LocationNamesTextNumber, line);
            int w = DsBgScreen.MeasureText(SystemFont, name);
            int textX;
            if (hg) textX = (8 * cols - 4 - w) / 2 + 4;
            else
            {
                int margin = ((w + 8) / 8) * 8 - w, half = margin / 2;
                int spare = 4 + half >= 8 ? 0 : ((8 - (4 + half)) * 2 + 7) / 8;
                textX = 4 + (w <= 0 ? 0 : ((((w + 8) / 8) + spare) * 8 + 8 - w) / 2);
            }
            Print(rgba, SystemFont, name, left + textX, top + (look.NameY >= 0 ? look.NameY : hg ? 8 : 16), hg ? colours[1] : colours[3], colours[2]);
            return new Shot("Place-name banner: top", Compose(rgba, _bannerStep.Scroll, _bannerStep.Level, _bannerStep.Effect));
        }

        // The layer wraps at 256 pixels, so a negative scroll brings it up from below the screen.
        private static byte[] Compose(byte[] layer, int scroll, int level, BannerMotionPatch.Effect effect)
        {
            byte[] rgba = Blank(FieldBackdrop);
            int block = effect == BannerMotionPatch.Effect.Pixelate ? level + 1 : 1;
            int fade = effect == BannerMotionPatch.Effect.Fade ? level : 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int sy = ((y - y % block + scroll) % 256 + 256) % 256, sx = x - x % block;
                    if (sy >= H) continue;
                    int from = (sy * W + sx) * 4, to = (y * W + x) * 4;
                    if (layer[from + 3] == 0) continue;
                    for (int c = 0; c < 3; c++) rgba[to + c] = (byte)((layer[from + c] * (16 - fade) + rgba[to + c] * fade) / 16);
                }
            return rgba;
        }

        // ── naming screen ────────────────────────────────────────────────────────────

        private static Shot NamingPrompt(int line, bool hg)
        {
            int[] prompts = hg ? new[] { 0, 1, 2, 3, 5, 8 } : new[] { 0, 1, 2, 3, 4, 5, 6 };
            if (!prompts.Contains(line)) return null;
            byte[] rgba = Blank(0);
            ushort[] talk = TalkColours;
            (int x, int y, int w, int h) = ScreenWindowLayouts.Window("naming", "Prompt", (2, 19, 27, 4));
            MessageWindow(rgba, x, y, w, h, talk[15]);
            Print(rgba, TalkFont, BoxText(hg ? 249 : 422, line, TalkFont), x * 8, y * 8, talk[1], talk[2]);
            return new Shot("Naming screen graphics: top", rgba, () => AvaloniaEditorLauncher.OpenNamingScreenEditor());
        }

        // naming_screen.c NamingScreen_InitIconSprite, the same in both games: each kind of name, its length and the icon's
        // animation (-1 for the Pokémon's own icon, with its gender sign).
        private static readonly (string Name, int Letters, int Icon)[] NamingTypes =
        {
            ("Player name", 7, 48), ("Rival name", 7, 51), ("Pokémon nickname", 10, -1), ("Box name", 8, 47),
        };

        // naming_screen.c in both games: the backdrop on BG2, the key art on BG1 scrolled by -11,-80, and the keys window (2,1, 26 by 12)
        // filled with colour 4, every other key in colour 3, the first page's characters every 16 by 19 pixels in colours 14 and 15.
        private static Shot NamingKeyboard(int state = 0)
        {
            (string _, int letters, int icon) = NamingTypes[Math.Clamp(state, 0, NamingTypes.Length - 1)];
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.NameInputGraphics);
            if (s == null) return null;
            ushort[] colours = NitroBgCodec.ReadColours(m(NamingScreenMembers.Colours));
            Palettes(s, colours, 0, 3);
            // With the bottom screen palettes patch, the backdrop reads a 256-colour copy at 0x20000.
            BottomPalettesPatch.Screen wide = BottomPalettesPatch.Naming;
            int backdrop = BottomPalettesPatch.DrawingMember(wide);
            bool widened = backdrop >= 0 && backdrop != NamingScreenMembers.Tiles;
            Layer(s, m, 2, 3, widened ? 0x20000 : 0, widened ? backdrop : NamingScreenMembers.Tiles, NamingScreenMembers.Backdrop);
            if (widened) ExtendedColours(s, m, 2, wide);
            Layer(s, m, 1, 2, 0x10000, NamingScreenMembers.Tiles, NamingScreenMembers.Keys);
            const int scrollX = -11, scrollY = -80;
            s.SetScroll(1, scrollX, scrollY);
            byte[] rgba = s.Render();
            // The name and its side window are filled with colour 1, which an imported backdrop doesn't have there.
            (int nameX, int nameY, int nameW, int nameH) = ScreenWindowLayouts.Window("naming", "Name", (10, 3, 13, 2));
            // The name window is as wide as the name's letters: letters × 12 / 8 + 1 tiles.
            nameW = letters * 12 / 8 + 1;
            Fill(rgba, DsBgScreen.Row(colours, 0)[1], nameX * 8, nameY * 8, nameW * 8, nameH * 8);

            ushort[] row = DsBgScreen.Row(colours, 1);
            (int kx, int ky, int _, int _) = ScreenWindowLayouts.Window("naming", "Keys", (2, 1, 26, 12));
            int left = kx * 8 - scrollX, top = ky * 8 - scrollY;
            const int keyWidth = 16, keyHeight = 19, keyColumns = 13;
            Fill(rgba, row[4], left, top, 26 * 8, 12 * 8);
            for (int r = 0; r < 5; r++)
                for (int c = (r + 1) % 2; c < keyColumns; c += 2)
                    Fill(rgba, row[3], left + c * keyWidth, top + r * keyHeight, keyWidth, keyHeight);
            ushort[][] keys = NamingKeys(0);
            FieldFont font = SystemFont;
            for (int r = 0; keys != null && font != null && r < keys.Length; r++)
                for (int c = 0; c < keys[r].Length; c++)
                {
                    int glyph = keys[r][c] - 1;
                    if (glyph < 0) continue;
                    int width = font.WidthOf(glyph);
                    DsBgScreen.DrawGlyph(rgba, font, glyph, left + c * keyWidth + (keyWidth - width) / 2, top + r * keyHeight + 4, row[14], row[15]);
                }

            // sSpriteAnimations / sUISpritesParam: the labels sit at 22,56 with UPPER chosen and each button is placed from its x
            // (SubspritePosControllerTask). The player's icon (48) is at 24,8, and seven underscores run from 80,39 every 12.
            Func<int, byte[]> sprites = i => GraphicAssets.Unsqueeze(m(i));
            const int labelsX = 22;
            List<(int X, int Y, int Anim)> drawn = new List<(int, int, int)>
            {
                (labelsX, 56, 37), (labelsX + 4, 68, 0), (labelsX + 36, 68, 8), (labelsX + 68, 68, 13), (labelsX + 136, 68, 23),
                (labelsX + 176, 68, 25),
            };
            (int iconX, int iconY, int _, int _) = ScreenWindowLayouts.Window("naming", "Icon", (1, 0, 4, 3));
            iconX = iconX * 8 + 16;
            iconY = iconY * 8 + 8;
            if (icon >= 0) drawn.Add((iconX, iconY, icon));
            // A Pokémon shows its own icon, and its gender sign at 80 + letters × 13, 27 (the male one here).
            else
            {
                PartyIcon(rgba, PartySample.Party[0].Species, iconX, iconY);
                drawn.Add((80 + letters * 13, 27, 45));
            }
            // naming_screen.c: a box starts with its name ("BOX 1", Pt bank 18 or HGSS bank 24 line 6) and a nickname with the species,
            // each letter centred in its 12-pixel slot in colours 14 and 15. The lit underscore waits after the last letter.
            string preset = state == 3 ? Text(Pt ? 18 : 24, 6) : state == 2 ? Line(PokemonNamesTextNumbers[0], PartySample.Party[0].Species) : "";
            int typed = Math.Min(preset.Length, letters);
            ushort[] nameInk = DsBgScreen.Row(colours, 0);
            for (int i = 0; i < typed; i++)
            {
                string letter = preset[i].ToString();
                Print(rgba, SystemFont, letter, nameX * 8 + 12 * i + (12 - DsBgScreen.MeasureText(SystemFont, letter)) / 2, nameY * 8, nameInk[14], nameInk[15]);
            }
            // The underscores follow the Name window, at 10,3 in the game.
            for (int i = 0; i < letters; i++) drawn.Add((nameX * 8 + 12 * i, nameY * 8 + 15, i == typed ? 44 : 43));
            foreach ((int bx, int by, int anim) in drawn)
                PartySprite(rgba, sprites, NamingScreenMembers.Animations, NamingScreenMembers.Cells, NamingScreenMembers.Sprites, bx, by, anim, -1,
                            NamingScreenMembers.SpriteColours);
            return new Shot("Naming screen graphics: bottom", rgba, () => AvaloniaEditorLauncher.OpenNamingScreenEditor());
        }

        private static ushort[][] NamingKeys(int page)
        {
            try { return ScreenCodeData.NamingKeys(page); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { AppLogger.WarnOnce("GameTextScreens.NamingKeys", ex); return null; }
        }

        // ── trainer card ─────────────────────────────────────────────────────────────

        // The value ends at Right, or starts at At when it is placed.
        private sealed record CardRow(int TileX, int TileY, int Label, int Value, int Right, int At = -1);

        // pokeplatinum src/applications/trainer_case/card_text.c; pokeheartgold overlay_trainer_card_main.
        private static readonly CardRow[] PtFront =
        {
            new(2, 4, 0, -1, 136), new(2, 6, 1, -2, 136), new(2, 9, 2, 14, 136), new(2, 12, 3, 21, 136),
            new(2, 15, 4, -3, 136), new(2, 18, 5, 15, 224), new(2, 20, 6, 17, 224),
        };
        private static readonly CardRow[] PtBack =
        {
            new(2, 2, 7, 17, 224), new(2, 7, 8, 22, 224), new(2, 9, 9, 18, -1, 112), new(2, 11, 10, 22, 224),
        };
        private static readonly CardRow[] HgFront =
        {
            new(2, 3, 0, -1, 96), new(17, 3, 1, -2, 104), new(2, 6, 2, 19, 136), new(2, 9, 3, 26, 136),
            new(2, 13, 4, -3, 136), new(2, 16, 5, 20, 224), new(2, 18, 6, 22, 224),
        };
        private static readonly CardRow[] HgBack =
        {
            new(2, 1, 7, 22, 224), new(2, 6, 8, 27, 224), new(2, 8, 9, 23, -1, 112), new(2, 10, 10, 27, 224),
        };

        /// <param name="cancel">HGSS Cancel label position in tiles, or null for the game's.</param>
        /// <param name="hint">HGSS hint label position in tiles, or null for the game's.</param>
        private static Shot TrainerCard(int line, bool hg, (int X, int Y)? cancel = null, (int X, int Y)? hint = null)
        {
            int bank = hg ? 727 : 616;
            int[] backOnly = hg ? new[] { 7, 8, 9, 10, 23, 24, 25, 27 } : new[] { 7, 8, 9, 10, 18, 19, 20, 22 };
            bool back = backOnly.Contains(line) || (hg && line == 16);
            TrainerCardGraphics card = new TrainerCardGraphics();
            if (!card.Available) return null;
            (int ncgr, int front, int backScreen, int[] _) = TrainerCardMembers;
            _pieces.Add((DirNames.TrainerCardGraphics, ncgr, "Card drawing"));
            _pieces.Add((DirNames.TrainerCardGraphics, back ? backScreen : front, back ? "Card back" : "Card front"));
            if (!back) _pieces.Add((DirNames.TrainerCardGraphics, TrainerCardTrainerMembers.maleNscr, "Trainer"));

            // The case on BG1 shows around and under the card on BG2. HGSS colours it with the card palette, Platinum
            // (trainer_case/main.c) with the case palette.
            byte[] caseLayer = hg ? TrainerCaseHg() : Pt ? TrainerCasePt() : null;
            byte[] rgba = caseLayer ?? Blank(0);
            DsBgScreen.DrawImage(rgba, back ? card.ComposeCardBack(0, caseLayer != null) : card.ComposeCardFront(0, caseLayer != null), 0, 0);
            if (!back)
            {
                RawImage trainer = card.ComposeTrainer(true);
                if (hg) DrawImagePart(rgba, trainer, 168, 40, 80, 88);
                else DsBgScreen.DrawImage(rgba, trainer, 0, 0);
            }

            ushort[] rank = DsBgScreen.Row(NitroBgCodec.ReadColours(Members(DirNames.TrainerCardGraphics)?.Invoke(TrainerCardMembers.rankPalettes[0])), 15);
            ushort ink = rank[1], shadow = rank[2];
            CardRow[] rows = back ? (hg ? HgBack : PtBack) : (hg ? HgFront : PtFront);
            for (int n = 0; n < rows.Length; n++)
            {
                CardRow r = rows[n];
                // HGSS rows are the card's window table: front 0 to 6, back 7 to 10.
                (int wx, int wy, int _, int _) = ScreenWindowLayouts.Window("trainercard", RomLocator.HgssCardWindows[(back ? 7 : 0) + n], (r.TileX, r.TileY, 1, 1));
                int x = wx * 8, y = wy * 8;
                Print(rgba, SystemFont, Text(bank, r.Label), x, y, ink, shadow);
                string value = r.Value switch { -1 => "00000", -2 => "PLAYER", -3 => "0", _ => CardValue(bank, r.Value) };
                if (r.At >= 0)
                {
                    // Link battles place "W" and "L", with each count ending before its letter.
                    Print(rgba, SystemFont, Text(bank, r.Value), x + r.At, y, ink, shadow);
                    Print(rgba, SystemFont, Text(bank, r.Value + 1), x + r.At + 64, y, ink, shadow);
                    Print(rgba, SystemFont, "0", x + 160, y, ink, shadow, Align.Right);
                    Print(rgba, SystemFont, "0", x + 224, y, ink, shadow, Align.Right);
                    continue;
                }
                Print(rgba, SystemFont, value, x + r.Right, y, ink, shadow, Align.Right);
                if (r.Label == 7) Print(rgba, SystemFont, Text(bank, hg ? 20 : 15), x + r.Right, y + 16, ink, shadow, Align.Right);
            }
            if (hg)
            {
                // Palette row 15, colours 5 and 4.
                FieldFont touch = Font(4) ?? SystemFont;
                (int hx, int hy) = hint ?? (1, 21);
                (int cx, int cy) = cancel ?? (25, 21);
                // The Back and hint buttons are sprites a tile left of their text (ov51_021E80A0): Back animation 0 at 192,168, the hint 4 at 0,168.
                Func<int, byte[]> files = Members(DirNames.TrainerCardGraphics);
                if (files != null)
                {
                    Func<int, byte[]> m = i => GraphicAssets.Unsqueeze(files(i));
                    (int bNcgr, int bNcer, int bNanr, int bNclr) = (HgssTrainerCardCase.ButtonNcgr, HgssTrainerCardCase.ButtonNcer,
                                                                     HgssTrainerCardCase.ButtonNanr, HgssTrainerCardCase.ButtonNclr);
                    PartySprite(rgba, m, bNanr, bNcer, bNcgr, cx * 8 - 8, cy * 8, 0, -1, bNclr);
                    if (!back) PartySprite(rgba, m, bNanr, bNcer, bNcgr, hx * 8 - 8, hy * 8, 4, -1, bNclr);
                }
                if (back) Print(rgba, touch, Text(bank, 16), hx * 8 + 48, hy * 8 + 4, rank[5], rank[4], Align.Centre);
                else Print(rgba, SystemFont, Text(bank, 15), hx * 8, hy * 8 + 4, rank[5], rank[4]);
                Print(rgba, touch, Text(bank, 14), cx * 8 + 24, cy * 8 + 4, rank[5], rank[4], Align.Centre);
                if (line == 17 || line == 18)
                {
                    ushort[] talk = TalkColours;
                    (int mx, int my, int mw, int mh) = ScreenWindowLayouts.Window("trainercard", "Message", (2, 1, 27, 4));
                    MessageWindow(rgba, mx, my, mw, mh, talk[15]);
                    Print(rgba, TalkFont, BoxText(bank, line, TalkFont), mx * 8, my * 8, talk[1], talk[2]);
                }
            }
            return new Shot($"Trainer card graphics: {(hg ? "bottom" : "top")}", rgba, AvaloniaEditorLauncher.OpenTrainerCardEditor);
        }

        private static readonly string[] CardSides = { "Front", "Back" };

        // HGSS: the badge case on top and the card below. Platinum: the card on top and the badge case below.
        private static (byte[] Top, byte[] Bottom)? CardLayout(int state) => WithArchive<(byte[], byte[])?>(() => Hg
            ? (TrainerCardTopHg(), TrainerCard(state == 1 ? 7 : 0, true)?.Rgba)
            : (TrainerCard(state == 1 ? 7 : 0, false)?.Rgba, TrainerCaseBottomPt(PlatinumTouchLists.TrainerCase()?.SpotsOf(0), polished: state == 2)));

        // A new save pads the minutes and the year's last two digits with zeros (0:00, Jan. 1, 2009).
        private static string CardValue(int bank, int line)
        {
            return Text(bank, line, (_, kind, buffer) => kind != 51 ? null : buffer switch { 1 => "00", 2 => "09", 4 => "1", _ => null });
        }

        private static byte[] TrainerCasePt()
        {
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.TrainerCardGraphics);
            if (s == null) return null;
            Palettes(s, NitroBgCodec.ReadColours(m(PlatinumTrainerCardCase.CaseNclr)));
            Layer(s, m, 1, 1, 0, PlatinumTrainerCardCase.CaseNcgr, PlatinumTrainerCardCase.CaseNscr);
            return s.Render();
        }

        private static byte[] TrainerCaseHg()
        {
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.TrainerCardGraphics);
            if (s == null) return null;
            Palettes(s, NitroBgCodec.ReadColours(m(TrainerCardMembers.rankPalettes[0])));
            Layer(s, m, 1, 1, 0, HgssTrainerCardCase.CaseNcgr, HgssTrainerCardCase.CaseNscr);
            return s.Render();
        }

        private static void DrawImagePart(byte[] rgba, RawImage image, int x, int y, int w, int h)
        {
            if (image == null || image.IsEmpty) return;
            for (int yy = y; yy < Math.Min(image.Height, y + h); yy++)
                for (int xx = x; xx < Math.Min(image.Width, x + w); xx++)
                {
                    int s = (yy * image.Width + xx) * 4;
                    if (image.Bgra[s + 3] < 128 || xx >= W || yy >= H) continue;
                    int p = (yy * W + xx) * 4;
                    rgba[p] = image.Bgra[s + 2]; rgba[p + 1] = image.Bgra[s + 1]; rgba[p + 2] = image.Bgra[s]; rgba[p + 3] = 255;
                }
        }

        // ── PC boxes ─────────────────────────────────────────────────────────────────

        // pokeplatinum src/applications/pc_boxes/ov19_021D61B0.c, ov19_021D79F8.c and ov19_021DB2FC.c.
        private static Shot BoxPt(bool messages, int line)
        {
            if (!messages && (line < 6 || line > 79)) return null;
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.BoxGraphics);
            if (s == null) return null;
            ushort[] panel = NitroBgCodec.ReadColours(m(5));
            Palettes(s, panel, 0, 7);
            s.SetPalette(9, DsBgScreen.Row(NitroBgCodec.ReadColours(m(28)), 0));
            Layer(s, m, 2, 1, 0, 1, 0);
            // preview_display.c PCMonPreviewDisplay_LoadGraphics fills species, number and nickname with colour 15 and leaves the
            // held item window clear. Only its top 12x2 goes on the map.
            (string Name, (int, int, int, int) At, int Palette, int Base, int Fill, int Rows)[] windows =
            {
                ("Species", (0, 1, 10, 2), 1, 288, 15, -1), ("Dex number", (5, 3, 5, 1), 1, 308, 15, -1),
                ("Nickname", (0, 16, 10, 3), 3, 313, 15, -1), ("Held item and more", (1, 21, 12, 4), 1, 343, 0, 2),
            };
            foreach ((string wName, (int, int, int, int) at, int palette, int baseTile, int fill, int rows) in windows)
            {
                (int wx, int wy, int ww, int wh) = ScreenWindowLayouts.Window("box", wName, at);
                s.PutWindow(2, wx, wy, ww, wh, palette, baseTile, fill, rows);
            }
            s.InitLayer(3, 3, 0x10000);
            s.LoadTiles(0x10000, m(29));
            s.Fill(3, 0x18, 0, 0, 32, 32, 9);
            (int _, ushort[] wall) = NitroBgCodec.ReadMap(m(30));
            // The Box picture scrolls BG3, so the wallpaper and its name go wherever it is.
            (int boxX, int boxY, int _, int _) = ScreenWindowLayouts.Window("box", "Pictures", "Box", (11, 0, 21, 20));
            for (int y = 0; y < 20; y++)
                for (int x = 0; x < 21; x++)
                {
                    int at = y * 21 + x;
                    if (at < wall.Length && boxX + x < 32 && boxY + y < 32)
                        s.Copy(3, boxX + x, boxY + y, 1, 1, new[] { (ushort)((wall[at] & 0x0FFF) | 9 << 12) }, 0, 0, 1);
                }
            // view.c BoxView_WriteWallpaperTilemap: the four rows under the wallpaper get its first tile.
            s.Fill(3, 0, boxX, boxY + 20, Math.Min(21, 32 - boxX), Math.Max(0, Math.Min(4, 32 - boxY - 20)), 9);
            byte[] rgba = s.Render();

            ushort[] wallRow = DsBgScreen.Row(NitroBgCodec.ReadColours(m(28)), 0);
            int name = !messages && line >= 6 && line <= 23 ? line : 6;
            Print(rgba, SystemFont, Text(18, name), boxX * 8 + 84, boxY * 8 + 13, wallRow[2], wallRow[1], Align.Centre);

            ushort[] row4 = DsBgScreen.Row(panel, 4);
            if (messages)
            {
                (int mx, int my, int mw, int mh) = ScreenWindowLayouts.Window("box", "Message", (2, 21, 27, 2));
                MessageWindow(rgba, mx, my, mw, mh, row4[15]);
                Print(rgba, TalkFont, BoxText(19, line, TalkFont), mx * 8, my * 8, row4[1], row4[2]);
            }
            else if (line >= 24)
            {
                List<int> items = line == 67 ? new List<int> { 24, 67 } : new List<int> { line, 67 };
                // The menu window is eight rows tall; the frame only covers the rows in use, at the bottom.
                (int ux, int uy, int uw, int _) = ScreenWindowLayouts.Window("box", "Menu", (19, 3, 12, 16));
                int n = items.Count, top = uy + (8 - n) * 2;
                StandardWindow(rgba, ux, top, uw, 2 * n, row4[15]);
                for (int i = 0; i < n; i++)
                    Print(rgba, SystemFont, Text(18, items[i]), ux * 8 + 10, top * 8 + 16 * i, row4[11], row4[12]);
            }
            return new Shot("PC box graphics: top", rgba);
        }

        // The Layout tab's Platinum box list: the first sample Pokémon hovered, then picked (preview_display.c windows on BG2:
        // species 0,1 and number 5,3 in row 1, nickname 0,16 in row 3, held item 1,21 in row 1, and the picture centred on 44,84).
        private static byte[] BoxLayoutTopPt(int state)
        {
            byte[] rgba = BoxPt(false, 6)?.Rgba;
            Func<int, byte[]> m = Members(DirNames.BoxGraphics);
            if (rgba == null || m == null) return rgba;
            ushort[] panel = NitroBgCodec.ReadColours(m(5));
            ushort[] row1 = DsBgScreen.Row(panel, 1), row3 = DsBgScreen.Row(panel, 3), row4 = DsBgScreen.Row(panel, 4);
            (int boxX, int boxY, int _, int _) = ScreenWindowLayouts.Window("box", "Pictures", "Box", (11, 0, 21, 20));
            for (int i = 0; i < PartySample.Party.Length; i++)
                if (!PartySample.Party[i].Empty)
                    PartyIcon(rgba, PartySample.Party[i].Species, 108 + 8 * (boxX - 11) + 24 * (i % 6), 46 + 8 * boxY + 24 * (i / 6));
            // Pictures move in whole tiles from where the game has them (RomLocator.BoxCodeWindowSitesIn).
            (int X, int Y) Moved(string name, int x, int y, int tileX, int tileY, int width, int height)
            {
                (int nx, int ny, int _, int _) = ScreenWindowLayouts.Window("box", name, (tileX, tileY, width, height));
                return (x + 8 * (nx - tileX), y + 8 * (ny - tileY));
            }
            Func<int, byte[]> files = i => GraphicAssets.Unsqueeze(m(i));
            // Sprites in the order the game stacks them: frame (47), buttons (44), arrows (43).
            ushort[] objects = NitroBgCodec.ReadColours(files(26));
            // preview_display.c PCMonPreviewDisplay_CreateInfoSprites: the held item's frame (tiles 15, cells 16) under its clear window.
            (int hx, int hy, int _, int _) = ScreenWindowLayouts.Window("box", "Held item and more", (1, 21, 12, 4));
            List<DsBgScreen.Oam[]> frame = DsBgScreen.ReadCells(files(16));
            if (frame.Count > 0)
                DsBgScreen.DrawCell(rgba, frame[0], NitroBgCodec.ReadCharacters(files(15)), row => DsBgScreen.Row(objects, row), hx * 8 + 48, hy * 8 + 8);
            // buttons_graphics.c: the Party Pokémon and Close buttons are one sprite (tiles 9, cells 10, animations 11).
            (int bx, int by) = Moved("Party and Close buttons", 183, 176, 13, 20, 18, 3);
            Sprite(rgba, files, 11, 10, 9, bx, by, 0, row => DsBgScreen.Row(objects, row));
            // cursor_graphics.c: the box arrows (tiles 12, cells 13, animations 14, sequences 6 and 7).
            (int ax, int ay) = Moved("Box arrows", 108, 20, 12, 1, 19, 3);
            Sprite(rgba, files, 14, 13, 12, ax, ay, 6, row => DsBgScreen.Row(objects, row));
            Sprite(rgba, files, 14, 13, 12, ax + 128, ay, 7, row => DsBgScreen.Row(objects, row));
            PartySample.Pokemon who = PartySample.Party[0];
            string species = Line(PokemonNamesTextNumbers[0], who.Species);
            (int sx, int sy, int _, int _) = ScreenWindowLayouts.Window("box", "Species", (0, 1, 10, 2));
            Print(rgba, SystemFont, species, sx * 8 + 2, sy * 8, row1[9], row1[6]);
            (int dx, int dy, int _, int _) = ScreenWindowLayouts.Window("box", "Dex number", (5, 3, 5, 1));
            SpecialText(rgba, 2, who.Species, 3, 2, dx * 8, dy * 8, row1[9], row1[6]);
            if (new DexSample(who.Species).Picture(new ScreenMon { Kind = ScreenMonKind.Front }) is { Rgba: not null } picture)
            {
                (int px, int py) = Moved("Pokémon picture", 44, 84, 0, 5, 10, 10);
                Blit(rgba, picture.Rgba, picture.Width, picture.Height, px - picture.Width / 2, py - picture.Height / 2);
            }
            (int nx, int ny, int _, int _) = ScreenWindowLayouts.Window("box", "Nickname", (0, 16, 10, 3));
            Print(rgba, SystemFont, species, nx * 8 + 2, ny * 8, row3[1], row3[2]);
            // Male in colours 7 and 8, female in 3 and 4 (bank 19 lines 21 and 22).
            if (who.Gender is 0 or 1)
                Print(rgba, SystemFont, Text(19, 21 + who.Gender), nx * 8 + 70, ny * 8, row3[who.Gender == 0 ? 7 : 3], row3[who.Gender == 0 ? 8 : 4]);
            SpecialText(rgba, 1, who.Level, 3, 1, nx * 8, ny * 8 + 16, row3[1], row3[2]);
            if (state == BoxSelected)
            {
                // A picked Pokémon swaps the held item for the message and opens the menu (bank 18: Move, Summary, Item, Withdraw, Mark, Release, Cancel).
                (int mx, int my, int mw, int mh) = ScreenWindowLayouts.Window("box", "Message", (2, 21, 27, 2));
                MessageWindow(rgba, mx, my, mw, mh, row4[15]);
                string picked = Text(19, 0, (_, _, _) => species);
                Print(rgba, TalkFont, picked, mx * 8, my * 8, row4[1], row4[2]);
                int[] items = { 58, 61, 64, 62, 65, 66, 67 };
                (int ux, int uy, int uw, int _) = ScreenWindowLayouts.Window("box", "Menu", (19, 3, 12, 16));
                int top = uy + (8 - items.Length) * 2;
                StandardWindow(rgba, ux, top, uw, 2 * items.Length, row4[15]);
                for (int i = 0; i < items.Length; i++)
                    Print(rgba, SystemFont, Text(18, items[i]), ux * 8 + 10, top * 8 + 16 * i, row4[11], row4[12]);
            }
            else
            {
                (int ix, int iy, int _, int _) = ScreenWindowLayouts.Window("box", "Held item and more", (1, 21, 12, 4));
                PokemonPersonalData data = new PokemonPersonalData(who.Species);
                if (state == PtTypesPage)
                {
                    // PCMonPreviewDisplay_DrawRotationPage: the types at 24 and 60, 176 down, the second only when it differs.
                    int ty = 176 + 8 * (iy - 21);
                    Paste(rgba, TypeIcons.PixelsFor((int)data.Type1), 24 - 16, ty - 8);
                    if (data.Type2 != data.Type1) Paste(rgba, TypeIcons.PixelsFor((int)data.Type2), 60 - 16, ty - 8);
                }
                else
                {
                    // The sample is Hardy, nature 0, with its first ability.
                    string info = state switch
                    {
                        PtNaturePage => Text(PtNatureNames, 0),
                        PtAbilityPage => Line(AbilityNamesTextNumber, data.FirstAbility),
                        _ => who.ItemId > 0 ? Line(ItemNamesTextNumber, who.ItemId) : Text(19, 20),
                    };
                    Print(rgba, SystemFont, info, ix * 8, iy * 8, row1[9], row1[6]);
                }
            }
            return rgba;
        }

        // Platinum shows the held item on hover and the message and menu once picked. HGSS shows the item texts in Move Items mode
        // with an item picked, and each message in its dialog. The copies are a page-swap buffer and move with their windows.
        // Plain squares from the box top map's row, to fill where the markings were.
        private static ushort[] HgMarkingsAway()
        {
            Func<int, byte[]> m = Members(DirNames.BoxGraphics);
            byte[] map = m == null ? null : WindowTileArt.Unpacked(m(5));
            return map == null ? null : NitroBgCodec.PlainSquares(map, 14, 18, 8, 2, HgPanelPlainFrom);
        }

        // Left of column 14 the panel has the label boxes and its frame, so plain squares are looked for from there.
        private const int HgPanelPlainFrom = 14;

        // Plain squares from BG2's row past the frame column, to fill where Platinum's markings were.
        private static ushort[] PtMarkingsAway()
        {
            Func<int, byte[]> m = Members(DirNames.BoxGraphics);
            byte[] map = m == null ? null : WindowTileArt.Unpacked(m(0));
            return map == null ? null : NitroBgCodec.PlainSquares(map, 4, 19, 6, 1, PtPanelPlainFrom);
        }

        private const int PtPanelPlainFrom = 1;

        private static bool BoxShows(int state, string group, string name)
        {
            bool picked = state == BoxSelected;
            if (Pt) return picked ? name != "Held item and more" : name is not ("Message" or "Menu");
            if (name.StartsWith("Copy of", StringComparison.Ordinal)) return false;
            if (name is "Item name" or "Item description") return state == BoxItemMessage;
            if (name.StartsWith("Move ", StringComparison.Ordinal)) return picked;
            return name switch
            {
                "Message" => state == BoxRelease,
                "Message at the top" => state == BoxMarking,
                "Short message" => state is BoxPartyMessage or BoxMenu,
                "Second message" => state == BoxItemMessage,
                "Change box button" => state == BoxMenu,
                _ => true,
            };
        }

        /// <summary>Both screens of the Platinum box list for the Layout tab, by state.</summary>
        internal static (byte[] Top, byte[] Bottom) BoxLayoutPt(int state) => WithArchive(() => (BoxLayoutTopPt(state), BoxLayoutBottomPt()));

        // dial_graphics.c: the box list and markings tabs (tiles 138, cells 139, animations 140, colours 144) at the bottom
        // corners, x 0 and 256, 568 on the sprite surface (184 on the bottom screen). The markings tab is the same sprite flipped.
        private static byte[] BoxLayoutBottomPt()
        {
            byte[] rgba = PcBottomPt(0);
            Func<int, byte[]> m = Members(DirNames.BoxGraphics);
            if (rgba == null || m == null) return rgba;
            ushort[] tabs = NitroBgCodec.ReadColours(GraphicAssets.Unsqueeze(m(144)));
            // The tabs move in whole tiles from 0,184 and 256,184, the markings one flipped to the left of its x.
            (int lx, int ly, int _, int _) = ScreenWindowLayouts.Window("box", "Box list tab", (0, 17, 6, 6));
            (int rx, int ry, int _, int _) = ScreenWindowLayouts.Window("box", "Markings tab", (26, 17, 6, 6));
            int leftY = 184 + 8 * (ly - 17), rightX = 256 + 8 * (rx - 26), rightY = 184 + 8 * (ry - 17);
            byte[] tab = new byte[rgba.Length], flipped = new byte[rgba.Length];
            Sprite(tab, i => GraphicAssets.Unsqueeze(m(i)), 140, 139, 138, 8 * lx, leftY, 0, row => DsBgScreen.Row(tabs, row));
            Sprite(flipped, i => GraphicAssets.Unsqueeze(m(i)), 140, 139, 138, 0, rightY, 0, row => DsBgScreen.Row(tabs, row));
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int from = (y * W + x) * 4;
                    if (tab[from + 3] != 0) Array.Copy(tab, from, rgba, from, 4);
                    int to = rightX - 1 - x;
                    if (flipped[from + 3] != 0 && to >= 0 && to < W) Array.Copy(flipped, from, rgba, (y * W + to) * 4, 4);
                }
            return rgba;
        }

        // pokeheartgold overlay 14, laid out like Platinum's box screens.
        private static string[] BoxStates => Hg
            ? new[] { "Normal", "Pokémon selected", "Release question", "Marking", "Party message", "Item message", "Box menu" }
            : new[] { "Normal", "Pokémon selected", "Nature page", "Ability page", "Types page" };
        // preview_display.c's rotation after the held item: nature, ability, then the types as icons.
        private const int PtNaturePage = 2, PtAbilityPage = 3, PtTypesPage = 4, PtNatureNames = 202;
        private const int BoxNormal = 0, BoxSelected = 1, BoxRelease = 2, BoxMarking = 3, BoxPartyMessage = 4, BoxItemMessage = 5, BoxMenu = 6;
        // The box menu opened from the box name: HgssMenuButtons.Pc's Box theme layout.
        private const int BoxThemeLayout = 9;

        private static Shot BoxHg(bool messages, int line)
        {
            // A Pokémon picked in the box opens the menu on the right (HgssMenuButtons.Pc layout 3).
            if (_state == BoxSelected) return DrawBox(3) is byte[] menu ? new Shot("PC box graphics: bottom", menu) : null;
            if (_forLayout && _state == BoxNormal) return DrawBox(IdleMoveLayout) is byte[] idle ? new Shot("PC box graphics: bottom", idle) : null;
            if (_forLayout && _state == BoxMenu && DrawBox(BoxThemeLayout) is byte[] theme)
            {
                // Bank 25 line 7, "What do you want to do?", in the short message window.
                ushort[] says = TalkColours;
                (int qx, int qy, int qw, int qh) = ScreenWindowLayouts.Window("box", "Short message", (2, 21, 19, 2));
                MessageWindow(theme, qx, qy, qw, qh, says[15]);
                Print(theme, TalkFont, BoxText(25, 7, TalkFont), qx * 8, qy * 8, says[1], says[2]);
                return new Shot("PC box graphics: bottom", theme);
            }
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.BoxGraphics);
            if (s == null) return null;

            if (messages && line >= 50 && line <= 53)
            {
                DsBgScreen top = new DsBgScreen();
                Palettes(top, NitroBgCodec.ReadColours(m(7)));
                Layer(top, m, 1, 2, 0, 6, 5);
                byte[] upper = top.Render();
                ushort[] sys = SystemColours;
                Print(upper, SystemFont, Text(25, line), 8, 8, sys[1], sys[2]);
                return new Shot("PC box graphics: top", upper);
            }

            ushort[] colours = NitroBgCodec.ReadColours(m(4));
            Palettes(s, colours, 0, 4);
            Layer(s, m, 3, 3, 0, 1, 0);
            Layer(s, m, 2, 2, 0x8000, 3, 2);
            s.SetPalette(14, DsBgScreen.Row(NitroBgCodec.ReadColours(m(40)), 0));
            s.LoadTiles(0, m(16), 604);
            (int _, ushort[] wall) = NitroBgCodec.ReadMap(m(15));
            for (int y = 0; y < 20; y++)
                for (int x = 0; x < 21; x++)
                {
                    int at = y * 21 + x;
                    if (at < wall.Length)
                        s.Copy(3, x, 1 + y, 1, 1, new[] { (ushort)(((wall[at] & 0x3FF) + 604) | (wall[at] & 0x0C00) | 14 << 12) }, 0, 0, 1);
                }
            byte[] rgba = s.Render();
            ushort[] talk = TalkColours;
            if (_forLayout)
            {
                // The window and bank 25 line of each dialog: release 2 (with the touch Yes and No), marking 35, party 37, item mode 60.
                (string Window, (int, int, int, int) At, int Line) dialog = _state switch
                {
                    BoxRelease => ("Message", (2, 21, 27, 2), 2),
                    BoxMarking => ("Message at the top", (2, 1, 27, 2), 35),
                    BoxPartyMessage => ("Short message", (2, 21, 19, 2), 37),
                    BoxItemMessage => ("Second message", (2, 21, 27, 2), 60),
                    _ => (null, default, -1),
                };
                if (dialog.Window != null)
                {
                    (int mx, int my, int mw, int mh) = ScreenWindowLayouts.Window("box", dialog.Window, dialog.At);
                    MessageWindow(rgba, mx, my, mw, mh, talk[15]);
                    Print(rgba, TalkFont, BoxText(25, dialog.Line, TalkFont), mx * 8, my * 8, talk[1], talk[2]);
                }
                if (_state == BoxRelease) TouchYesNoHg(rgba, 25, 12);
            }
            else if (messages)
            {
                (int mx, int my, int mw, int mh) = ScreenWindowLayouts.Window("box", "Message", (2, 21, 27, 2));
                MessageWindow(rgba, mx, my, mw, mh, talk[15]);
                Print(rgba, TalkFont, BoxText(25, line, TalkFont), mx * 8, my * 8, talk[1], talk[2]);
            }
            else
            {
                // Bottom-aligned at column 21, three tiles per row, in the touch font.
                FieldFont touch = Font(4) ?? SystemFont;
                ushort[] sel = DsBgScreen.Row(colours, 0);
                Fill(rgba, Rgb(31, 31, 31), 21 * 8, 17 * 8, 11 * 8, 3 * 8);
                Print(rgba, touch, Text(24, line), 21 * 8 + 44, 17 * 8 + 4, Rgb(8, 8, 8), Rgb(22, 22, 22), Align.Centre);
            }
            return new Shot("PC box graphics: bottom", rgba);
        }

        // yes_no_prompt.c, the small shape: tiles 1 and colours 0 of system/touch_subwindow, Yes (map 2) and No (map 4) each 6 by 4,
        // stacked from tile x, y, each in a separate colour row.
        private static void TouchYesNoHg(byte[] rgba, int x, int y)
        {
            Func<int, byte[]> m = Members(DirNames.TouchYesNoGraphics);
            if (m == null) return;
            DsBgScreen s = new DsBgScreen();
            Palettes(s, NitroBgCodec.ReadColours(m(0)), 0, 2);
            s.InitLayer(0, 0, 0);
            s.LoadTiles(0, m(1));
            for (int i = 0; i < 2; i++)
            {
                (int width, ushort[] map) = NitroBgCodec.ReadMap(m(2 + 2 * i));
                if (map == null) continue;
                ushort[] button = map.Select(e => (ushort)(e & 0x0FFF | i << 12)).ToArray();
                s.Copy(0, x, y + 4 * i, 6, 4, button, 0, 0, Math.Max(1, width));
            }
            byte[] drawn = s.Render(rgba);
            Array.Copy(drawn, rgba, rgba.Length);
        }

        // Draws the PC's top screen (overlay 14) for the hovered Pokémon, with its moves once it's picked.
        private static Shot BoxTopHg(int state)
        {
            Func<int, byte[]> m = Members(DirNames.BoxGraphics);
            if (m == null) return null;
            DsBgScreen top = new DsBgScreen();
            Palettes(top, NitroBgCodec.ReadColours(m(7)));
            Layer(top, m, 1, 2, 0, 6, 5);
            if (state == BoxSelected)
            {
                // The moves panel is a 12 by 10 frame (member 86) on the front layer, slid in to 20,12.
                top.InitLayer(2, 0, 0);
                (int frameWidth, ushort[] frame) = NitroBgCodec.ReadMap(m(86));
                if (frame != null) top.Copy(2, 20, 12, 12, 10, frame, 0, 0, Math.Max(1, frameWidth));
            }
            else if (state == BoxItemMessage)
            {
                // Move Items mode's item panel is a 32 by 9 frame (member 85) slid in to row 15.
                top.InitLayer(2, 0, 0);
                (int frameWidth, ushort[] frame) = NitroBgCodec.ReadMap(m(85));
                if (frame != null) top.Copy(2, 0, 15, 32, 9, frame, 0, 0, Math.Max(1, frameWidth));
            }
            byte[] rgba = top.Render();
            PartySample.Pokemon who = PartySample.Party[0];
            PokemonPersonalData data = new PokemonPersonalData(who.Species);
            ushort[] sys = SystemColours;
            string species = Line(PokemonNamesTextNumbers[0], who.Species);
            // The "No." and "Lv" marks sit 5 down in their windows, with the number 16 right of the mark.
            void Put(string window, (int X, int Y, int W, int H) fallback, string text, int mark = 0, int down = 0)
            {
                (int x, int y, int w, int h) = ScreenWindowLayouts.Window("box", window, fallback);
                // The item panel covers row 15 and below.
                if (state == BoxItemMessage && y + h > 15) return;
                if (mark > 0) SpecialMark(rgba, mark, x * 8, y * 8 + 5, sys[1], sys[2]);
                Print(rgba, SystemFont, text, x * 8 + (mark > 0 ? 16 : 0), y * 8 + down, sys[1], sys[2]);
            }
            Put("Title", (1, 0, 30, 3), Text(25, 52), down: 8);
            // A new game shows the Johto number, and nothing for a species outside the Johto dex.
            int dexNumber = PokedexLists.RegionalNumber(who.Species);
            if (dexNumber > 0) Put("Dex number", (1, 5, 6, 2), $"{dexNumber:000}", mark: 2);
            Put("Species", (8, 5, 8, 2), species);
            // The sign uses the font's blue (7, 8) or red (3, 4) pair, as the Platinum box does.
            (int gx, int gy, int _, int gh) = ScreenWindowLayouts.Window("box", "Gender", (16, 5, 2, 2));
            if (who.Gender < 2 && !(state == BoxItemMessage && gy + gh > 15))
                Print(rgba, SystemFont, Text(24, 82 + who.Gender), gx * 8, gy * 8, sys[who.Gender == 0 ? 7 : 3], sys[who.Gender == 0 ? 8 : 4]);
            Put("Nickname", (1, 7, 8, 2), species);
            Put("Level", (4, 9, 6, 2), who.Level.ToString(), mark: 1);
            Put("Nature label", (1, 11, 8, 2), Text(24, 87));
            Put("Ability label", (1, 15, 11, 2), Text(24, 88));
            Put("Held item label", (1, 19, 12, 2), Text(24, 89));
            Put("Nature", (1, 13, 8, 2), Text(34, 0));
            Put("Ability", (1, 17, 11, 2), Line(AbilityNamesTextNumber, data.FirstAbility));
            Put("Held item", (1, 21, 12, 2), who.ItemId > 0 ? Line(ItemNamesTextNumber, who.ItemId) : Text(24, 92));
            // The picture is centred on its templates' spot, 44 right and down from the window's corner.
            (int picX, int picY, int _, int _) = ScreenWindowLayouts.Window("box", "Pictures", "Pokémon picture", (12, 7, 10, 10));
            if (new DexSample(who.Species).Picture(new ScreenMon { Kind = ScreenMonKind.Front }) is { Rgba: not null } picture)
                Blit(rgba, picture.Rgba, picture.Width, picture.Height, picX * 8 + 44 - picture.Width / 2, picY * 8 + 44 - picture.Height / 2);
            // The type icons' templates centre them 80 and 114 right of the species box and 8 down, so they follow it. They are sprites
            // and draw over the Pokémon.
            (int speciesX, int speciesY, int _, int _) = ScreenWindowLayouts.Window("box", "Species", (8, 5, 8, 2));
            int typeX = speciesX * 8 + 80;
            foreach (int type in new[] { (int)data.Type1, (int)data.Type2 }.Distinct())
            {
                Paste(rgba, TypeIcons.PixelsFor(type), typeX, speciesY * 8);
                typeX += 34;
            }
            if (state == BoxSelected)
            {
                int[] moves = PartySample.Moves;
                // The move windows sit inside the frame: their table places count from its corner.
                for (int i = 0; i < moves.Length; i++)
                {
                    (int mx, int my, int _, int _) = ScreenWindowLayouts.Window("box", $"Move {i + 1}", (1, 1 + 2 * i, 11, 2));
                    Print(rgba, SystemFont, Line(MoveNamesTextNumbers, moves[i]), (20 + mx) * 8, (12 + my) * 8, sys[1], sys[2]);
                }
            }
            if (state == BoxItemMessage)
            {
                // The item texts sit inside the panel. The icon is centred 10 right of the name and 32 below it.
                int item = PartySample.Party.Select(p => p.ItemId).FirstOrDefault(i => i > 0);
                if (item == 0) item = 234;
                (int nx, int ny, int nw, int nh) = ScreenWindowLayouts.Window("box", "Item name", (1, 1, 12, 2));
                (int dx, int dy, int dw, int dh) = ScreenWindowLayouts.Window("box", "Item description", (4, 3, 27, 6));
                // Both windows are cleared to colour 13 of palette 1 first.
                ushort[] colours = NitroBgCodec.ReadColours(m(7));
                if (colours != null && colours.Length > 29)
                {
                    Fill(rgba, colours[29], nx * 8, (15 + ny) * 8, nw * 8, nh * 8);
                    Fill(rgba, colours[29], dx * 8, (15 + dy) * 8, dw * 8, dh * 8);
                }
                Print(rgba, SystemFont, Line(ItemNamesTextNumber, item), nx * 8, (15 + ny) * 8, sys[1], sys[2]);
                Print(rgba, SystemFont, Line(RomInfo.ItemDescriptionsTextNumber, item), dx * 8, (15 + dy) * 8, sys[1], sys[2]);
                ItemIcon(rgba, item, nx * 8 + 10, (15 + ny) * 8 + 32);
            }
            return new Shot("PC box graphics: top", rgba);
        }

        // ── Pokégear ─────────────────────────────────────────────────────────────────

        // Each app as it opens: phone and radio from their text previews, and the town map and fly map on New Bark Town (map 60,
        // flavour text 21 in pokegear_map.c) with the windows of overlay_101_021E7FF4.c and overlay_101_021EDCE0.c.
        private static (byte[] Top, byte[] Bottom)? PokegearLayout(int state) => WithArchive<(byte[], byte[])?>(() =>
        {
            _stop = 0;
            _stops = 1;
            _forLayout = true;
            switch (state)
            {
                case 0: return (Pokegear(TextBank(640), 1)?.Rgba, PokegearPictures.Draw(0));
                case PokegearPhoneMenu:
                    // overlay_101_021F017C.c sContextMenuParam[0]: Call, Sort and Quit (bank 271 lines 0 to 2), 16 wide at 13,9.
                    // The hint (line 20) is centred in the Hint window, colours 3 and 2 of row 10, on its band from the row above.
                    (int _, int hy, int _, int _) = ScreenWindowLayouts.Window("pokegear", "Phone", "Hint", (0, 21, 32, 2));
                    byte[] list = PokegearPictures.Draw(0, hintRow: hy - 1);
                    if (list != null && Members(DirNames.PokegearPhoneGraphics) is Func<int, byte[]> phone)
                    {
                        TouchListMenu(list, Enumerable.Range(0, 3).Select(i => Text(271, i)).ToArray(), 13, 9, 16);
                        ushort[] hint = DsBgScreen.Row(NitroBgCodec.ReadColours(phone(10)), 10);
                        Print(list, SystemFont, Text(271, 20), 128, hy * 8, hint[3], hint[2], Align.Centre);
                    }
                    return (Pokegear(TextBank(640), 1)?.Rgba, list);
                // pokegear/configure clears the sub layers, leaving the frame and clock on top.
                case 4: return (PokegearFrameTop(), PokegearPictures.Draw(1));
                case 1:
                    PokegearButtons radio = ScreenWindowLayouts.ButtonsInUse<PokegearButtons>();
                    return (Pokegear(TextBank(409), 2)?.Rgba,
                            PokegearPictures.Draw(7, radio?.TileSpots(radio.RadioButtonsPage), radio?.ScreenOf(radio.RadioButtonsPage)));
            }
            bool fly = state == 3;
            string group = fly ? "Fly map" : "Map";
            Func<int, byte[]> m = Members(DirNames.PokegearMapGraphics);
            byte[] top = fly ? FlyMapTopHg(m) : Pokegear(TextBank(273), 1)?.Rgba;
            byte[] bottom = fly ? FlyMapBottomHg(m) : PokegearPictures.Draw(2);
            if (top == null || bottom == null) return (top, bottom);
            if (fly)
            {
                ushort[] sub = DsBgScreen.Row(NitroBgCodec.ReadColours(m(62)), 1);
                (int rx, int ry, int _, int _) = ScreenWindowLayouts.Window("pokegear", group, "Region", (1, 8, 5, 3));
                Print(top, SystemFont, Text(273, 1), rx * 8 + 2, ry * 8 + 4, sub[1], sub[2]);
            }
            else
            {
                // overlay_101_021E9270.c prints the place again in the bottom heading.
                ushort[] heading = DsBgScreen.Row(NitroBgCodec.ReadColours(m(20)), 10);
                (int hx, int hy, int _, int _) = ScreenWindowLayouts.Window("pokegear", group, "Map heading", (3, 0, 12, 3));
                int at = 0;
                bool headers = false;
                try { headers = HeaderLabels.DynamicHeaders; } catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.PokegearHeading", ex); }
                if (MapHeader.TryReadLocationNameIndex(60, headers, out int shown)) at = shown;
                Print(bottom, SystemFont, Line(LocationNamesTextNumber, at), hx * 8, hy * 8 + 5, heading[1], heading[2]);
            }
            ushort[] row = DsBgScreen.Row(NitroBgCodec.ReadColours(m?.Invoke(fly ? 62 : 14)), 1);
            int place = 0;
            bool dynamic = false;
            try { dynamic = HeaderLabels.DynamicHeaders; } catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.PokegearPlace", ex); }
            if (MapHeader.TryReadLocationNameIndex(60, dynamic, out int named)) place = named;
            (int px, int py, int _, int _) = ScreenWindowLayouts.Window("pokegear", group, "Place", (3, fly ? 11 : 12, 12, 2));
            Print(top, SystemFont, Line(LocationNamesTextNumber, place), px * 8, py * 8 + (fly ? 0 : 1), row[1], row[2]);
            (int dx, int dy, int _, int _) = ScreenWindowLayouts.Window("pokegear", group, "Description", (1, 14, 28, 4));
            Print(top, SystemFont, Text(273, 21), dx * 8, dy * 8, row[1], row[2]);
            // The four marking icons (sprite set 0: files 4, 5 and 6, colours 0) sit 8 left of the two marking windows, in two rows
            // 21 apart. The game shows only the markings that are set.
            (int l1x, int l1y, int _, int _) = ScreenWindowLayouts.Window("pokegear", group, "Landmark 1", (5, 18, 11, 5));
            (int l2x, int _, int _, int _) = ScreenWindowLayouts.Window("pokegear", group, "Landmark 2", (18, 18, 11, 5));
            ushort[] markerColours = NitroBgCodec.ReadColours(m?.Invoke(0));
            if (m != null && markerColours != null)
                for (int i = 0; i < 4; i++)
                    Sprite(top, m, 6, 5, 4, (i % 2 == 0 ? l1x : l2x) * 8 - 8, l1y * 8 + 11 + 21 * (i / 2), 0,
                           r => DsBgScreen.Row(markerColours, r), i);
            if (fly)
            {
                ushort[] lower = NitroBgCodec.ReadColours(m?.Invoke(63));
                ushort[] r10 = DsBgScreen.Row(lower, 10);
                (int hx, int hy, int _, int _) = ScreenWindowLayouts.Window("pokegear", group, "Fly heading", (0, 21, 25, 2));
                Print(bottom, SystemFont, Text(273, 4), hx * 8 + 8, hy * 8, r10[3], r10[2]);
                (int cx, int cy, int _, int _) = ScreenWindowLayouts.Window("pokegear", group, "Fly close", (25, 21, 6, 2));
                FieldFont touch = Font(4) ?? SystemFont;
                Print(bottom, touch, Text(273, 6), cx * 8 + 24, cy * 8, r10[3], r10[1], Align.Centre);
            }
            return (top, bottom);
        });

        // overlay_101_021EDCE0.c, the fly map: the top is BG0 (drawing 64, map 65) behind BG2 (drawing 68, rows 8 on of map 69),
        // coloured by file 62. The bottom is BG1 (drawing 66, map 67, colours 63) over the region map.
        private static byte[] FlyMapTopHg(Func<int, byte[]> m)
        {
            if (m == null) return null;
            DsBgScreen s = new DsBgScreen();
            Palettes(s, NitroBgCodec.ReadColours(m(62)));
            Layer(s, m, 0, 3, 0x8000, 64, 65);
            s.InitLayer(2, 1, 0);
            s.LoadTiles(0, m(68));
            (int width, ushort[] map) = NitroBgCodec.ReadMap(m(69));
            if (map != null) s.Copy(2, 0, 8, 32, 16, map, 0, 8, Math.Max(1, width));
            return s.Render();
        }

        private static byte[] FlyMapBottomHg(Func<int, byte[]> m)
        {
            if (m == null) return null;
            DsBgScreen s = new DsBgScreen();
            Palettes(s, NitroBgCodec.ReadColours(m(63)));
            Layer(s, m, 1, 1, 0, 66, 67);
            return s.Render(PokegearPictures.RegionMap(m, 63));
        }

        // pokeheartgold src/application/pokegear: the frame every app's top screen sits in, skin 0.
        private static void PokegearFrame(DsBgScreen s)
        {
            Func<int, byte[]> gear = Members(DirNames.PokegearGraphics);
            if (gear == null) return;
            ushort[] colours = NitroBgCodec.ReadColours(gear(24));
            for (int r = 12; r < 16; r++) s.SetPalette(r, colours, r * 16);
            s.InitLayer(0, 3, 0x8000);
            s.LoadTiles(0x8000, gear(36));
            s.LoadMap(0, NitroBgCodec.ReadMap(gear(42)).Entries, DsBgScreen.MapSide);
        }

        // overlay_100_021E5900.c PokegearApp_LoadGraphics: weekday (sequence 2), four digits (0), colon (1) and the signal (3), drawn
        // from files 13, 12 and 6 in palette 2, the signal in 3. The sample clock reads Monday 10:30.
        private static void PokegearClock(byte[] rgba)
        {
            Func<int, byte[]> gear = Members(DirNames.PokegearGraphics);
            ushort[] colours = NitroBgCodec.ReadColours(gear?.Invoke(0));
            if (colours == null) return;
            Func<int, ushort[]> clock = _ => DsBgScreen.Row(colours, 2);
            Sprite(rgba, gear, 13, 12, 6, 173, 48, 2, clock, 1);
            int[] digits = { 1, 0, 3, 0 };
            int[] across = { 70, 86, 110, 126 };
            for (int i = 0; i < 4; i++) Sprite(rgba, gear, 13, 12, 6, across[i], 46, 0, clock, digits[i]);
            Sprite(rgba, gear, 13, 12, 6, 98, 46, 1, clock);
            Sprite(rgba, gear, 13, 12, 6, 197, 48, 3, _ => DsBgScreen.Row(colours, 3));
        }

        private static byte[] PokegearFrameTop()
        {
            DsBgScreen s = new DsBgScreen();
            PokegearFrame(s);
            byte[] rgba = s.Render();
            PokegearClock(rgba);
            return rgba;
        }

        private static Shot Pokegear(int bank, int line)
        {
            int[] contacts = Enumerable.Range(641, 76).Where(b => b != 668).Select(TextBank).ToArray();
            bool phone = bank == TextBank(640) || contacts.Contains(bank) || (bank == TextBank(271) && line >= 30 && line <= 37);
            bool radio = Enumerable.Range(409, 12).Select(TextBank).Contains(bank);
            bool map = bank == TextBank(273) && (line <= 1 || (line >= 10 && line <= 69) || line == 4 || line == 5);
            if (!phone && !radio && !map) return null;

            DsBgScreen s = new DsBgScreen();
            PokegearFrame(s);
            if (phone)
            {
                Func<int, byte[]> m = Members(DirNames.PokegearPhoneGraphics);
                if (m == null) return null;
                ushort[] colours = NitroBgCodec.ReadColours(m(4));
                Palettes(s, colours, 0, 12);
                Layer(s, m, 3, 2, 0, 16, 22);
                byte[] rgba = s.Render();
                PokegearClock(rgba);
                ushort[] row = DsBgScreen.Row(colours, 1);
                bool contact = contacts.Contains(bank);
                (int cx, int cy, int _, int _) = ScreenWindowLayouts.Window("pokegear", "Phone", "Caller", (4, 16, 12, 2));
                (int tx, int ty, int _, int _) = ScreenWindowLayouts.Window("pokegear", "Phone", "Call", (2, 19, 27, 4));
                if (contact && line > 0) Print(rgba, SystemFont, Line(bank, 0), cx * 8, cy * 8, row[1], row[2]);
                Print(rgba, SystemFont, Box(bank, line, SystemFont), tx * 8, ty * 8, row[1], row[2]);
                return new Shot("Pokégear phone graphics: top", rgba, () => AvaloniaEditorLauncher.OpenPokegearPhoneBook());
            }
            if (radio)
            {
                Func<int, byte[]> m = Members(DirNames.PokegearRadioGraphics);
                if (m == null) return null;
                if (line == 0)
                {
                    DsBgScreen bottom = new DsBgScreen();
                    ushort[] lower = NitroBgCodec.ReadColours(m(10));
                    Palettes(bottom, lower, 0, 14);
                    Layer(bottom, m, 2, 2, 0, 16, 22);
                    bottom.InitLayer(3, 3, 0);
                    bottom.LoadMap(3, NitroBgCodec.ReadMap(m(28)).Entries, DsBgScreen.MapSide);
                    byte[] under = bottom.Render();
                    ushort[] r0 = DsBgScreen.Row(lower, 0);
                    (int hx, int hy, int _, int _) = ScreenWindowLayouts.Window("pokegear", "Radio", "Heading", (5, 1, 22, 2));
                    Print(under, SystemFont, Line(bank, 0), hx * 8, hy * 8, r0[1], r0[2]);
                    return new Shot("Pokégear radio graphics: bottom", under);
                }
                ushort[] colours = NitroBgCodec.ReadColours(m(4));
                Palettes(s, colours, 0, 12);
                Layer(s, m, 3, 2, 0, 34, 40);
                byte[] rgba = s.Render();
                PokegearClock(rgba);
                ushort[] row = DsBgScreen.Row(colours, 0);
                (int sx, int sy, int _, int _) = ScreenWindowLayouts.Window("pokegear", "Radio", "Station", (4, 16, 12, 2));
                (int bx, int by, int _, int _) = ScreenWindowLayouts.Window("pokegear", "Radio", "Broadcast", (2, 19, 27, 4));
                Print(rgba, SystemFont, Line(bank, 1), sx * 8, sy * 8, row[1], row[2]);
                if (line >= 2)
                {
                    // The show scrolls up a line at a time, with two lines visible.
                    string[] said = Text(bank, line).Split('\n');
                    _byItself = true;
                    _stops = Math.Max(1, said.Length - 1);
                    int from = Math.Min(_stop % _stops, Math.Max(0, said.Length - 2));
                    Print(rgba, SystemFont, string.Join("\n", said.Skip(from).Take(2)), bx * 8, by * 8, row[1], row[2]);
                }
                return new Shot("Pokégear radio graphics: top", rgba);
            }
            {
                Func<int, byte[]> m = Members(DirNames.PokegearMapGraphics);
                if (m == null) return null;
                if (line == 4 || line == 5)
                {
                    DsBgScreen bottom = new DsBgScreen();
                    ushort[] lower = NitroBgCodec.ReadColours(m(20));
                    Palettes(bottom, lower, 0, 14);
                    Layer(bottom, m, 1, 1, 0, 26, 32);
                    byte[] under = bottom.Render();
                    ushort[] r10 = DsBgScreen.Row(lower, 10);
                    Print(under, SystemFont, Line(bank, line), 24 + 8, 0, r10[3], r10[2]);
                    return new Shot("Pokégear map graphics: bottom", under);
                }
                ushort[] colours = NitroBgCodec.ReadColours(m(14));
                Palettes(s, colours, 0, 12);
                s.InitLayer(2, 1, 0);
                s.LoadTiles(0, m(50));
                s.Copy(2, 0, 7, 32, 17, NitroBgCodec.ReadMap(m(56)).Entries, 0, 7, 32);
                byte[] rgba = s.Render();
                PokegearClock(rgba);
                ushort[] row = DsBgScreen.Row(colours, 1);
                (int rx, int ry, int _, int _) = ScreenWindowLayouts.Window("pokegear", "Map", "Region", (1, 8, 5, 3));
                (int dx, int dy, int _, int _) = ScreenWindowLayouts.Window("pokegear", "Map", "Description", (1, 14, 28, 4));
                if (line <= 1) Print(rgba, SystemFont, Line(bank, line), rx * 8 + 2, ry * 8 + 4, row[1], row[2]);
                else Print(rgba, SystemFont, Line(bank, line), dx * 8, dy * 8, row[1], row[2]);
                return new Shot("Pokégear map graphics: top", rgba);
            }
        }

        // ── party ────────────────────────────────────────────────────────────────────

        // pokeheartgold party_context_menu.c, pokeplatinum windows.c.
        private static void PartyPokemon(byte[] rgba, Func<int, byte[]> m, int n, PartySample.Pokemon who, int bank, ushort[] row0,
                                        ushort[] colours, byte[] glyphs, bool hg)
        {
            (int px, int py) = PanelOf(n);
            string name = who.Egg ? Line(PokemonNamesTextNumbers[0], 494) : Line(PokemonNamesTextNumbers[0], who.Species);
            string shown = Line(bank, 8 + n, (_, _, _) => name);
            if (string.IsNullOrEmpty(shown)) shown = name;
            (int nx, int ny, int _, int _) = ScreenWindowLayouts.Window("party", ScreenWindowLayout.PokemonWindow(n + 1, "name"), (px + 6, py + 1, 9, 2));
            FieldFont nameFont = (GameFonts.EntryOf(ScreenWindowLayouts.PartyFont("name")) is int entry && entry >= 0 ? Font(entry) : null) ?? SystemFont;
            InWindow(rgba, ScreenWindowLayout.PokemonWindow(n + 1, "name"), into =>
            {
                Print(into, nameFont, shown, nx * 8, ny * 8, row0[15], row0[14]);
                if (!who.Egg && who.Gender < 2)
                    Print(into, nameFont, Text(bank, 27 + who.Gender), nx * 8 + 64, ny * 8,
                          row0[who.Gender == 0 ? 3 : 5], row0[who.Gender == 0 ? 4 : 6]);
            });

            int condition = who.Egg ? 0 : who.Hp == 0 ? 6 : who.Condition;
            if (!who.Egg)
            {
                (int lx, int ly, int _, int _) = ScreenWindowLayouts.Window("party", ScreenWindowLayout.PokemonWindow(n + 1, "level"), (px, py + 4, 6, 2));
                if (condition == 0)
                    InWindow(rgba, ScreenWindowLayout.PokemonWindow(n + 1, "level"), into =>
                    {
                        Glyph(into, glyphs, 11, 2, lx * 8 + 5, ly * 8 + 2, row0);
                        Digits(into, glyphs, who.Level.ToString(), lx * 8 + 21, ly * 8 + 2, row0);
                    });
                (int hx, int hy, int _, int _) = ScreenWindowLayouts.Window("party", ScreenWindowLayout.PokemonWindow(n + 1, "HP"), (px + 7, py + 4, 8, 2));
                // HGSS right-aligns the HP in three digits from 0; Platinum pads it with spaces from 4.
                string cur = who.Hp.ToString();
                InWindow(rgba, ScreenWindowLayout.PokemonWindow(n + 1, "HP"), into =>
                {
                    Digits(into, glyphs, cur, hx * 8 + (hg ? 0 : 4) + (3 - cur.Length) * 8, hy * 8 + 2, row0);
                    Glyph(into, glyphs, 10, 1, hx * 8 + 28, hy * 8 + 2, row0);
                    Digits(into, glyphs, who.MaxHp.ToString(), hx * 8 + 36, hy * 8 + 2, row0);
                });

                (int bx, int by, int _, int _) = ScreenWindowLayouts.Window("party", ScreenWindowLayout.PokemonWindow(n + 1, "HP bar"), (px + 8, py + 3, 6, 1));
                int full = ScreenWindowLayouts.Panels()?.HpBarLength ?? PartyPanelLayout.GameHpBarLength;
                int length = who.MaxHp <= 0 ? 0 : who.Hp * full / who.MaxHp;
                if (length == 0 && who.Hp > 0) length = 1;
                // The colour is always judged on the game's 48 pixels, whatever the bar length.
                int judged = who.MaxHp <= 0 ? 0 : who.Hp * PartyPanelLayout.GameHpBarLength / who.MaxHp;
                int barRow = who.Hp == who.MaxHp || judged * 2 > PartyPanelLayout.GameHpBarLength ? 3
                           : judged * 5 > PartyPanelLayout.GameHpBarLength ? 4 : 5;
                ushort[] bar = DsBgScreen.Row(colours, barRow);
                InWindow(rgba, ScreenWindowLayout.PokemonWindow(n + 1, "HP bar"), into =>
                {
                    Fill(into, bar[10], bx * 8, by * 8 + 2, length, 1);
                    Fill(into, bar[9], bx * 8, by * 8 + 3, length, 2);
                    Fill(into, bar[10], bx * 8, by * 8 + 5, length, 1);
                });
            }
        }

        // Sprites sit above the text layer, so they're drawn after all the text.
        private static void PartyPokemonSprites(byte[] rgba, Func<int, byte[]> m, int n, PartySample.Pokemon who, bool hg, bool opened)
        {
            (int px, int py) = PanelOf(n);
            int condition = who.Egg ? 0 : who.Hp == 0 ? 6 : who.Condition;
            // pokeheartgold _0210150C, pokeplatinum sMemberPanelTemplates; the held item sits 8,8 from the icon.
            PartyPanelLayout panels = ScreenWindowLayouts.Panels();
            (int bx, int by) = panels?.Ball(n) ?? (px * 8 + 16, py * 8 + 14);
            (int ix, int iy) = panels?.Icon(n) ?? (px * 8 + 30, py * 8 + 16);
            (int sx, int sy) = panels?.Status(n) ?? (px * 8 + 36, py * 8 + 44);
            PartySprite(rgba, m, 0, 1, 2, bx, by, opened ? 1 : 0, -1);
            PartyIcon(rgba, who.Egg ? 494 : who.Species, ix + (opened ? 2 : 0), iy + (opened ? 2 : 0));
            if (condition > 0) StatusIcon(rgba, condition, sx, sy, hg);
            if (who.Item > 0) PartySprite(rgba, m, 18, 19, 20, ix + 8, iy + 8, who.Item == 2 ? 1 : 0, -1, 21);
        }

        private static PartyPanelSizePatch.Size PanelSize => ScreenWindowLayouts.Panels()?.Size ?? PartyPanelSizePatch.Original;

        private static (int X, int Y) PanelOf(int n) => ScreenWindowLayouts.Panels()?.Panel(n) ?? PartyPanels[n];

        // Pixel 1 is colour 15 and pixel 2 colour 14; the rest stay clear.
        private static void Glyph(byte[] rgba, byte[] glyphs, int tile, int tiles, int x, int y, ushort[] row)
        {
            if (glyphs == null) return;
            for (int t = 0; t < tiles; t++)
                for (int yy = 0; yy < 8; yy++)
                    for (int xx = 0; xx < 8; xx++)
                    {
                        int at = (tile + t) * 32 + (yy * 8 + xx) / 2;
                        if (at >= glyphs.Length) return;
                        int value = (xx & 1) != 0 ? glyphs[at] >> 4 : glyphs[at] & 0xF;
                        if (value is not (1 or 2)) continue;
                        int sx = x + t * 8 + xx, sy = y + yy;
                        if (sx < 0 || sy < 0 || sx >= W || sy >= H) continue;
                        (byte r, byte g, byte b) = NitroBgCodec.Colour(row[value == 1 ? 15 : 14]);
                        int to = (sy * W + sx) * 4;
                        rgba[to] = r; rgba[to + 1] = g; rgba[to + 2] = b; rgba[to + 3] = 255;
                    }
        }

        // The digits are the first ten glyphs.
        private static void Digits(byte[] rgba, byte[] glyphs, string number, int x, int y, ushort[] row)
        {
            for (int i = 0; i < number.Length; i++)
                if (char.IsDigit(number[i])) Glyph(rgba, glyphs, number[i] - '0', 1, x + i * 8, y, row);
        }

        private static void PartySprite(byte[] rgba, Func<int, byte[]> m, int nanr, int ncer, int ncgr, int x, int y, int sequence,
                                        int forcedRow, int nclr = 8)
        {
            ushort[] obj = null;
            Sprite(rgba, m, nanr, ncer, ncgr, x, y, sequence, row =>
            {
                obj ??= NitroBgCodec.ReadColours(m(nclr));
                return DsBgScreen.Row(obj, forcedRow >= 0 ? forcedRow : row);
            });
        }

        /// <summary>A frame of <paramref name="sequence"/> centred at x, y, with each OAM palette row from <paramref name="rows"/>.</summary>
        internal static void Sprite(byte[] rgba, Func<int, byte[]> m, int nanr, int ncer, int ncgr, int x, int y, int sequence, Func<int, ushort[]> rows,
                                    int frame = 0)
        {
            try
            {
                NanrFile anim = NanrFile.Read(m(nanr));
                List<DsBgScreen.Oam[]> cells = DsBgScreen.ReadCells(m(ncer));
                byte[] tiles = NitroBgCodec.ReadCharacters(m(ncgr));
                if (anim == null || sequence >= anim.Sequences.Count || anim.Sequences[sequence].Frames.Count <= frame) return;
                int resultAt = anim.Sequences[sequence].Frames[frame].ResultAt;
                NanrFile.Result result = anim.Results.FirstOrDefault(r => r.Offset == resultAt);
                if (result == null || result.Cell >= cells.Count) return;
                DsBgScreen.DrawCell(rgba, cells[result.Cell], tiles, rows, x, y);
            }
            catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.Sprite", ex); }
        }

        // font_special_chars.c FontSpecialChars_DrawPartyScreenText: a glyph (1 "Lv", 2 "No.", two tiles from tile 11 + 2·(glyph - 1))
        // then the number in tile digits 0 to 9, padded to its digits with blanks (pad 1) or zeros (pad 2). Ink 1 and shadow 2.
        private static void SpecialText(byte[] rgba, int glyph, int value, int digits, int pad, int x, int y, ushort ink, ushort shadow)
        {
            byte[] tiles;
            try { tiles = NitroBgCodec.ReadCharacters(NitroBgCodec.Inflate(new ScriptNarc(DirNames.Fonts).Get(RomInfo.SpecialGlyphsFontEntry(Hg)))); }
            catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.SpecialText", ex); return; }
            if (tiles == null) return;
            void Tile(int tile, int left)
            {
                for (int yy = 0; yy < 8; yy++)
                    for (int xx = 0; xx < 8; xx++)
                    {
                        int at = tile * 32 + (yy * 8 + xx) / 2, px = left + xx, py = y + yy;
                        if (at >= tiles.Length || px < 0 || py < 0 || px >= W || py >= H) continue;
                        int v = (xx & 1) != 0 ? tiles[at] >> 4 : tiles[at] & 0xF;
                        if (v is 1 or 2) DsBgScreen.Put(rgba, (py * W + px) * 4, v == 1 ? ink : shadow);
                    }
            }
            Tile(11 + 2 * (glyph - 1), x);
            Tile(12 + 2 * (glyph - 1), x + 8);
            if (value < 0) return;
            string text = pad == 2 ? value.ToString().PadLeft(digits, '0') : pad == 1 ? value.ToString().PadLeft(digits) : value.ToString();
            for (int i = 0; i < text.Length; i++)
                if (text[i] is >= '0' and <= '9') Tile(text[i] - '0', x + 16 + 8 * i);
        }

        /// <summary>The "Lv" (1) or "No." (2) mark alone, where text follows it.</summary>
        private static void SpecialMark(byte[] rgba, int glyph, int x, int y, ushort ink, ushort shadow) =>
            SpecialText(rgba, glyph, -1, 0, 0, x, y, ink, shadow);

        /// <summary>The sample party's icons at <paramref name="spots"/>, one per Pokémon.</summary>
        internal static void PartyIcons(byte[] rgba, IReadOnlyList<(int X, int Y)> spots) => WithArchive(() =>
        {
            for (int i = 0; i < spots.Count && i < PartySample.Party.Length; i++)
                if (!PartySample.Party[i].Empty) PartyIcon(rgba, PartySample.Party[i].Species, spots[i].X, spots[i].Y);
            return 0;
        });

        // A scaled or turned sprite gets twice the room, so anything past 64 pixels across is cut off, as on the DS.
        private static void TurnedPartyIcon(byte[] rgba, int species, FightIconView icon)
        {
            try
            {
                if (IconPicture(species) is { } p) PasteTurned(rgba, p, icon);
            }
            catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.TurnedPartyIcon", ex); }
        }

        private static GraphicAssets.Preview IconPicture(int species)
        {
            if (Without(DirNames.MonIcons, species + 7)) return null;
            GraphicAssets.Archive icons = GraphicAssets.All.FirstOrDefault(a => a.Dir == DirNames.MonIcons);
            GraphicAssets.Preview p = icons == null ? null : GraphicAssets.Render(icons, species + 7);
            if (p?.Rgba == null || p.Width < 32) return null;
            _pieces.Add((DirNames.MonIcons, species + 7, "Drawing"));
            Credit(DirNames.MonIcons, species + 7);
            return p;
        }

        private static void PasteTurned(byte[] rgba, GraphicAssets.Preview p, FightIconView icon)
        {
            double scale = icon.Look.Percent / 100.0, angle = icon.Look.Degrees * Math.PI / 180;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            for (int yy = -32; yy < 32; yy++)
                for (int xx = -32; xx < 32; xx++)
                {
                    int sx = icon.X + xx, sy = icon.Y + yy;
                    if (sx < 0 || sy < 0 || sx >= W || sy >= H) continue;
                    // The game rotates and then flips, so this reverse lookup undoes the flip first.
                    double dx = (icon.Look.FlipAcross ? -1 : 1) * (xx + 0.5), dy = (icon.Look.FlipDown ? -1 : 1) * (yy + 0.5);
                    double u = (dx * cos + dy * sin) / scale, v = (-dx * sin + dy * cos) / scale;
                    int fromX = (int)Math.Floor(u) + 16, fromY = (int)Math.Floor(v) + 16;
                    if (fromX < 0 || fromY < 0 || fromX >= 32 || fromY >= 32 || fromY >= p.Height) continue;
                    int from = (fromY * p.Width + fromX) * 4;
                    if (p.Rgba[from + 3] == 0) continue;
                    Array.Copy(p.Rgba, from, rgba, (sy * W + sx) * 4, 4);
                }
        }

        private static void PartyIcon(byte[] rgba, int species, int x, int y)
        {
            try
            {
                GraphicAssets.Preview p = IconPicture(species);
                if (p == null) return;
                for (int yy = 0; yy < 32 && yy < p.Height; yy++)
                    for (int xx = 0; xx < 32; xx++)
                    {
                        int from = (yy * p.Width + xx) * 4;
                        if (p.Rgba[from + 3] == 0) continue;
                        int sx = x - 16 + xx, sy = y - 16 + yy;
                        if (sx < 0 || sy < 0 || sx >= W || sy >= H) continue;
                        Array.Copy(p.Rgba, from, rgba, (sy * W + sx) * 4, 4);
                    }
            }
            catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.PartyIcon", ex); }
        }

        // HGSS a/0/3/9 files 62 to 65, Platinum's summary archive 62 to 65.
        private static void StatusIcon(byte[] rgba, int condition, int x, int y, bool hg)
        {
            Func<int, byte[]> m = Members(hg ? DirNames.SummarySprites : DirNames.SummaryGraphics);
            if (m != null) PartySprite(rgba, m, 62, 63, 64, x, y, condition, -1, 65);
        }

        // pokeplatinum party_menu main.c, pokeheartgold party_menu.c.
        private static readonly (int X, int Y)[] PartyPanels = { (0, 0), (16, 1), (0, 6), (16, 7), (0, 12), (16, 13) };
        private static readonly (int X, int Y)[] MultiPanels = { (0, 0), (16, 0), (0, 6), (16, 6), (0, 12), (16, 12) };

        private static readonly string[] PokemonParts = { "name", "level", "HP", "HP bar", "comment" };

        // The game puts each Pokémon's windows down with its panel, in party order; the buttons come after.
        private static int PartyPutOrder(ScreenWindowLayout.Window w)
        {
            if (w.Pokemon == 0) return 100;
            return w.Pokemon * 10 + Math.Max(0, Array.IndexOf(PokemonParts, w.Part));
        }

        // Switching slides each panel with its text (pokeheartgold PartyMenu_SwapMonsPanelsStep), so a window outside it is left behind.
        private static (int X, int Y, int W, int H)? PartyBlock(ScreenWindowLayout.Window w)
        {
            if (w.Pokemon == 0) return null;
            (int x, int y) = w.Group == "Pokémon in a multi battle" ? MultiPanels[w.Pokemon - 1] : PanelOf(w.Pokemon - 1);
            PartyPanelSizePatch.Size size = PanelSize;
            return (x, y, size.Width, size.Height);
        }

        // Message boxes have a wider frame than menu buttons and Platinum's item and move menus.
        private static (int L, int T, int R, int B)? PartyFrame(ScreenWindowLayout.Window w)
        {
            if (w.Name.EndsWith(" message", StringComparison.Ordinal)) return (2, 1, 3, 1);
            if (w.Name.Contains("button", StringComparison.Ordinal) && w.Layer == 0) return (1, 1, 1, 1);
            if (w.Name is "Item menu" or "Move list") return (1, 1, 1, 1);
            return null;
        }

        // With Confirm on screen both games force the Cancel window to row 22.
        private static int? PartyFixedTop(int state, ScreenWindowLayout.Window w) =>
            state == PartyConfirm && w.Name == "Cancel button" ? 22 : null;

        // ── party states ─────────────────────────────────────────────────────────────

        private static string[] PartyStates => new[] { "Normal", "Menu open", "Item menu", "Move list", "Confirm and cancel", "Switching" };
        private const int PartyNormal = 0, PartyMenu = 1, PartyItemMenu = 2, PartyMoveList = 3, PartyConfirm = 4, PartySwitch = 5,
                          PartyFieldMoves = 6, PartyMulti = 7;
        // "Move to where?" in the short message box.
        private static int MoveToWhere(bool hg) => hg ? 31 : 30;

        /// <summary>pokeheartgold msg_0300, pokeplatinum party_menu.</summary>
        private sealed record PartyLines(int Choose, int MenuPrompt, int ItemPrompt, int MovePrompt, int Summary, int Switch,
                                         int Item, int Quit, int Give, int Take, int[] Menu);

        private static readonly PartyLines HgParty = new(29, 38, 39, 41, 129, 128, 130, 135, 143, 144,
            Enumerable.Range(128, 15).Where(n => n != 136 && n != 143).Concat(new[] { 149, 186 }).ToArray());
        private static readonly PartyLines PtParty = new(29, 37, 38, 40, 146, 145, 147, 152, 160, 161,
            Enumerable.Range(145, 15).Where(n => n != 153).Concat(new[] { 166, 200 }).ToArray());

        private static int PartyStateOf(int line, bool hg)
        {
            PartyLines t = hg ? HgParty : PtParty;
            if (line == 0) return PartyConfirm;
            if (line == MoveToWhere(hg)) return PartySwitch;
            if (line == t.ItemPrompt || line == t.Give || line == t.Take) return PartyItemMenu;
            if (line == t.MovePrompt || line == t.MovePrompt + 1) return PartyMoveList;
            if (line == t.MenuPrompt || t.Menu.Contains(line)) return PartyMenu;
            return -1;
        }

        // party_menu.c PartyMenu_SetTopScreenSelectionPanelVisibility.
        private static bool PartyPanelUp(int state) => state is PartyMenu or PartyItemMenu or PartyMoveList or PartyFieldMoves;

        /// <summary>
        /// Multi battle is a Layout-only state. A Pokémon's comment only shows for TMs, evolution stones and choosing an order, which
        /// aren't states yet.
        /// </summary>
        public static bool PartyShows(int state, string group, string name, bool hg)
        {
            if (group == "Top screen") return PartyPanelUp(state);
            if (name.EndsWith("comment", StringComparison.Ordinal)) return false;
            if (group == "Pokémon in a multi battle") return state == PartyMulti;
            if (group == "Pokémon")
                return state != PartyMulti || name.StartsWith("Pokémon 1", StringComparison.Ordinal)
                    || name.StartsWith("Pokémon 3", StringComparison.Ordinal) || name.StartsWith("Pokémon 5", StringComparison.Ordinal);
            return name switch
            {
                "Cancel button" => state is PartyNormal or PartyConfirm or PartyMulti or PartySwitch,
                "Confirm button" => state == PartyConfirm,
                "Short message" or "Long message" => state is PartyNormal or PartyConfirm or PartyMulti or PartySwitch,
                "Medium message" => state is PartyMenu or PartyItemMenu or PartyMoveList or PartyFieldMoves,
                "Item menu" => !hg && state == PartyItemMenu,
                "Context menu" => !hg && state is PartyMenu or PartyFieldMoves,
                "Move list" => !hg && state == PartyMoveList,
                "Quit button" => state is PartyMenu or PartyItemMenu or PartyMoveList or PartyFieldMoves,
                _ when name.StartsWith("Submenu button", StringComparison.Ordinal) =>
                    state == PartyMoveList || (state == PartyItemMenu && name[^1] is '1' or '2'),
                // sButtonWindowIDs: with field moves the left column (4 to 7) is used too.
                _ when name.StartsWith("Menu button", StringComparison.Ordinal) =>
                    state == PartyFieldMoves || (state == PartyMenu && name[^1] is '1' or '2' or '3'),
                _ => false,
            };
        }

        private static Shot Party(int line, bool hg)
        {
            int bank = hg ? 300 : 453;
            PartyLines t = hg ? HgParty : PtParty;
            int state = Math.Clamp(_state, 0, PartyStates.Length - 1);
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.PartyMenuGraphics);
            if (s == null) return null;
            ushort[] colours = NitroBgCodec.ReadColours(m(16));
            Palettes(s, colours);
            Layer(s, m, 3, 3, 0x10000, 15, 17);
            s.InitLayer(2, 2, 0x10000);
            (int _, ushort[] panels) = NitroBgCodec.ReadMap(m(22));
            bool menuUp = state is PartyMenu or PartyItemMenu or PartyMoveList or PartyFieldMoves;
            PartyPanelSizePatch.Size size = PanelSize;
            for (int n = 0; n < 6; n++)
            {
                PartySample.Pokemon who = PartySample.Party[n];
                // pokeheartgold sub_0207A174, pokeplatinum DrawMemberPanel.
                int from = who.Empty ? 2 * size.Height : n == 0 ? 0 : size.Height;
                // sub_0207A7F4: while switching, the Pokémon picked and the one under the cursor (the first two) share palette 7.
                int row = who.Empty ? 1 : state == PartySwitch && n <= 1 ? 10 : 3 + (n == 0 ? 4 : 0) + (!who.Egg && who.Hp == 0 ? 2 : 0);
                // Only colours 0 to 7 change with the state.
                ushort[] panel = DsBgScreen.Row(colours, who.Empty ? 1 : 3 + n);
                if (!who.Empty) Array.Copy(DsBgScreen.Row(colours, row), panel, 8);
                s.SetPalette(3 + n, panel);
                for (int y = 0; y < size.Height; y++)
                    for (int x = 0; x < size.Width; x++)
                    {
                        int at = (from + y) * 32 + x;
                        if (at < panels.Length)
                            s.Copy(2, PanelOf(n).X + x, PanelOf(n).Y + y, 1, 1,
                                   new[] { (ushort)((panels[at] & 0x0FFF) | (3 + n) << 12) }, 0, 0, 1);
                    }
                if (who.Egg)
                    s.Fill(2, size.StripTile, PanelOf(n).X + size.StripColumn, PanelOf(n).Y + size.StripRow, size.StripLength, 1, 3 + n);
            }
            byte[] rgba = s.Render();
            Screen party = Screens.First(x => x.Key == "party");
            _shownTiles = ShownTiles(party, ScreenWindowLayouts.For("party"), state);

            ushort[] row0 = DsBgScreen.Row(colours, 0);
            byte[] glyphs = NitroBgCodec.ReadCharacters(NitroBgCodec.Inflate(Members(DirNames.Fonts)?.Invoke(RomInfo.SpecialGlyphsFontEntry(hg))));
            for (int n = 0; n < 6; n++)
            {
                PartySample.Pokemon who = PartySample.Party[n];
                if (who.Empty) continue;
                _drawing = n;
                Credit(DirNames.PartyMenuGraphics, 22);
                Credit(DirNames.Fonts, RomInfo.SpecialGlyphsFontEntry(hg));
                PartyPokemon(rgba, m, n, who, bank, row0, colours, glyphs, hg);
                _drawing = -1;
            }
            for (int n = 0; n < 6; n++)
            {
                PartySample.Pokemon who = PartySample.Party[n];
                if (who.Empty) continue;
                _drawing = n;
                PartyPokemonSprites(rgba, m, n, who, hg, n == 0 && menuUp);
                _drawing = -1;
            }
            // sub_0207A89C.
            (int cx0, int cy0) = ScreenWindowLayouts.Panels()?.Cursor(0) ?? (64, 25);
            if (state == PartySwitch)
            {
                // Switch: the second cursor (animation 1 + 2) stays on the Pokémon picked, the cursor moves on in colour row 0.
                (int cx1, int cy1) = ScreenWindowLayouts.Panels()?.Cursor(1)
                                     ?? (cx0 + (PanelOf(1).X - PanelOf(0).X) * 8, cy0 + (PanelOf(1).Y - PanelOf(0).Y) * 8);
                PartySprite(rgba, m, 5, 6, 7, cx0, cy0, 3, -1);
                PartySprite(rgba, m, 5, 6, 7, cx1, cy1, 0, 0);
            }
            else PartySprite(rgba, m, 5, 6, 7, cx0, cy0, 1, menuUp ? 1 : -1);

            ushort[] talk = TalkColours, sys = SystemColours;
            int[] comments = hg ? Enumerable.Range(150, 15).ToArray() : Enumerable.Range(167, 15).ToArray();
            int[] levelUp = hg ? Enumerable.Range(169, 8).ToArray() : Enumerable.Range(185, 7).ToArray();

            // party_menu sprites 8 and 9.
            if (state is PartyNormal or PartyConfirm or PartySwitch)
            {
                // The button sprite sits 24 right of and 16 below the window's corner (8 higher outside the confirm modes), and the
                // confirm modes put the window a row lower. Layout moves all of them with the window.
                (int cx, int cy, int cw, int _) = ScreenWindowLayouts.Window("party", "Cancel button", (26, 21, 5, 2));
                PartyButton(rgba, m, cx * 8 + 24, cy * 8 + (state == PartyConfirm ? 16 : 8), state == PartyConfirm ? 2 : 0);
                if (state == PartyConfirm) cy++;
                InWindow(rgba, "Cancel button", into => Print(into, SystemFont, Text(bank, 1), cx * 8 + cw * 4, cy * 8, row0[15], row0[14], Align.Centre));
            }
            if (state == PartyConfirm)
            {
                (int fx, int fy, int fw, int _) = ScreenWindowLayouts.Window("party", "Confirm button", (25, 20, 7, 2));
                PartyButton(rgba, m, fx * 8 + 32, fy * 8 + 8, 2);
                InWindow(rgba, "Confirm button", into => Print(into, SystemFont, Text(bank, 0), fx * 8 + fw * 4, fy * 8, row0[15], row0[14], Align.Centre));
            }

            if (state is PartyNormal or PartyConfirm or PartySwitch)
            {
                if (comments.Contains(line)) Print(rgba, SystemFont, Text(bank, line), 6 * 8, 4 * 8, row0[15], row0[14]);
                if (levelUp.Contains(line))
                {
                    StandardWindow(rgba, 1, 1, 14, 12, sys[15]);
                    for (int i = 0; i < 6; i++)
                    {
                        Print(rgba, SystemFont, Text(bank, levelUp[i]), 8, 8 + 16 * i, sys[1], sys[2]);
                        Print(rgba, SystemFont, Text(bank, levelUp[6]), 8 + 104, 8 + 16 * i, sys[1], sys[2], Align.Right);
                    }
                }
                bool message = line > 1 && (line < 8 || line > 13) && PartyStateOf(line, hg) < 0 && !comments.Contains(line) && !levelUp.Contains(line);
                int shown = state == PartySwitch ? MoveToWhere(hg) : message ? line : t.Choose;
                int[] prompts = hg ? new[] { 29, 30, 31, 32, 33, 34, 35, 184 } : new[] { 29, 30, 31, 32, 33, 34, 197 };
                bool shortBox = prompts.Contains(shown);
                (int mx, int my, int mw, int mh) = shortBox
                    ? ScreenWindowLayouts.Window("party", "Short message", (2, 21, 20, 2))
                    : ScreenWindowLayouts.Window("party", "Long message", (2, 19, 27, 4));
                MessageWindow(rgba, mx, my, mw, mh, talk[15]);
                Print(rgba, TalkFont, BoxText(bank, shown, TalkFont), mx * 8, my * 8, talk[1], talk[2]);
                return new Shot($"Party graphics: {(hg ? "bottom" : "top")}", rgba);
            }

            int prompt = state switch { PartyItemMenu => t.ItemPrompt, PartyMoveList => line == t.MovePrompt + 1 ? line : t.MovePrompt, _ => t.MenuPrompt };
            List<string> choices = new List<string>();
            int picked = -1;
            if ((hg ? ScreenWindowLayouts.Menu : null) is { } open)
            {
                // party_menu.c sub_0207C8B0 lists Summary, Switch, Item, Quit, then field moves; a menu with fewer windows drops options before Quit.
                int n = open.Windows.Length;
                string[] named = open.Item
                    ? new[] { t.Give, t.Take, 132, 134 }.Take(n - 1).Select(l => Text(bank, l)).Append(Text(bank, t.Quit)).ToArray()
                    : (n switch { 2 => new[] { t.Summary, t.Quit }, 3 => new[] { t.Summary, t.Switch, t.Quit }, _ => new[] { t.Summary, t.Switch, t.Item, t.Quit } })
                      .Select(l => Text(bank, l)).Concat(new[] { 15, 19, 57, 70 }.Take(Math.Max(0, n - 4)).Select(m => Line(MoveNamesTextNumbers, m))).ToArray();
                for (int i = 0; i < n; i++)
                    PartyMenuButton(rgba, m, colours, PartyContextMenu.ButtonName(open.Windows[i]), named[i], i == 0, open.Windows[i] == PartyContextMenu.Quit, i);
                (int qx, int qy, int qw, int qh) = ScreenWindowLayouts.Window("party", "Medium message", (2, 19, 13, 4));
                MessageWindow(rgba, qx, qy, qw, qh, talk[15]);
                Print(rgba, TalkFont, BoxText(bank, open.Item ? t.ItemPrompt : t.MenuPrompt, TalkFont), qx * 8, qy * 8, talk[1], talk[2]);
                return new Shot("Party graphics: bottom", rgba);
            }
            if (state is PartyMenu or PartyFieldMoves)
            {
                int[] menu = { t.Summary, t.Switch, t.Item, t.Quit };
                // Mail, field moves, Set and other extra choices use the Item slot.
                if (t.Menu.Contains(line) && !menu.Contains(line)) menu[2] = line;
                picked = Array.IndexOf(menu, line);
                choices.AddRange(menu.Select(n => Text(bank, n)));
            }
            else if (state == PartyItemMenu)
            {
                int[] menu = { t.Give, t.Take, t.Quit };
                picked = Array.IndexOf(menu, line);
                choices.AddRange(menu.Select(n => Text(bank, n)));
            }
            else
            {
                choices.AddRange(PartySample.Moves.Select(move => Line(MoveNamesTextNumbers, move)));
                if (hg) choices.Add(Text(bank, t.Quit));
            }

            if (hg)
            {
                // G2_SetBlendBrightness(30, 8).
                for (int i = 0; i < rgba.Length; i += 4)
                    for (int c = 0; c < 3; c++) rgba[i + c] = (byte)(rgba[i + c] + (255 - rgba[i + c]) * 8 / 16);
                // pokeheartgold party_context_menu.c sButtonWindowIDs.
                string[] slots = state switch
                {
                    PartyMenu or PartyFieldMoves => new[] { "Menu button 1", "Menu button 2", "Menu button 3", "Quit button" },
                    PartyItemMenu => new[] { "Submenu button 1", "Submenu button 2", "Quit button" },
                    _ => new[] { "Submenu button 1", "Submenu button 2", "Submenu button 3", "Submenu button 4", "Quit button" },
                };
                for (int i = 0; i < slots.Length && i < choices.Count; i++)
                    PartyMenuButton(rgba, m, colours, slots[i], choices[i], i == Math.Max(0, picked), slots[i] == "Quit button", i);
            }
            else
            {
                if (state is PartyMenu or PartyFieldMoves)
                {
                    // pokeplatinum windows.c PartyMenu_DrawContextMenu: up from the Layout row's bottom.
                    (int cx, int cy, int _, int ch) = ScreenWindowLayouts.Window("party", "Context menu", (19, 15, 12, 8));
                    int top = cy + ch - 2 * choices.Count;
                    PartyList(rgba, cx, top, 12, 2 * choices.Count, choices, Math.Max(0, picked), sys);
                }
                else
                {
                    (int lx, int ly, int lw, int lh) = ScreenWindowLayouts.Window("party", state == PartyItemMenu ? "Item menu" : "Move list",
                                                                                state == PartyItemMenu ? (19, 17, 12, 6) : (19, 15, 12, 8));
                    PartyList(rgba, lx, ly, lw, lh, choices, Math.Max(0, picked), sys);
                }
            }

            (int px, int py, int pw, int ph) = ScreenWindowLayouts.Window("party", "Medium message", (2, 19, 13, 4));
            MessageWindow(rgba, px, py, pw, ph, talk[15]);
            Print(rgba, TalkFont, BoxText(bank, prompt, TalkFont), px * 8, py * 8, talk[1], talk[2]);
            return new Shot($"Party graphics: {(hg ? "bottom" : "top")}", rgba);
        }

        public static Shot DrawOther(Screen screen, int state, Func<int, int, string> archiveLine)
        {
            if (GameLanguage == GameLanguages.Japanese || screen?.OtherScreen == null) return null;
            lock (Gate)
            {
                _state = Math.Max(0, state);
                _line = archiveLine;
                _pieces = new List<(DirNames Dir, int Member, string Kind)>();
                try { return screen.OtherScreen(_state); }
                catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.Other." + screen.Key, ex); return null; }
            }
        }

        // DrawMemberTouchScreenButton draws each Pokémon's ball button on the front layer, using 5 by 5 tiles of touch_button from tile 5 in palette 1.
        private static Shot PartyBalls()
        {
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.PartyMenuGraphics);
            if (s == null) return null;
            s.SetPalette(0, NitroBgCodec.ReadColours(m(13)));
            s.SetPalette(1, NitroBgCodec.ReadColours(m(4)));
            Layer(s, m, 1, 1, 0, 12, 14);
            ExtendedColours(s, m, 1, BottomPalettesPatch.Party);
            s.InitLayer(0, 0, 0x10000);
            s.LoadTiles(0x10000, m(3));
            PartyPanelLayout panels = ScreenWindowLayouts.Panels();
            const int side = PartyPanelLayout.BallButtonTiles;
            for (int n = 0; panels?.HasBallButtons == true && n < PartySample.Party.Length; n++)
            {
                if (PartySample.Party[n].Empty) continue;
                (int bx, int by) = panels.BallButton(n);
                ushort[] button = new ushort[side * side];
                for (int i = 0; i < button.Length; i++) button[i] = (ushort)(1 << 12 | (5 + i));
                s.Put(0, button, bx, by, side, side);
            }
            Credit(DirNames.PartyMenuGraphics, 14);
            return new Shot("Party graphics: bottom", s.Render());
        }

        // pokeheartgold party_menu.c sub_02079A14. A selected Pokémon's panel slides up five tiles, showing the windows on rows 24 to 26.
        private static Shot PartyTop(int line, int state)
        {
            const int bank = 300;
            bool up = line is 8 or 130 or 182 || PartyPanelUp(state);
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.PartyMenuGraphics);
            if (s == null) return null;
            ushort[] thirteen = NitroBgCodec.ReadColours(m(13));
            s.SetPalette(0, thirteen, 0);
            s.SetPalette(1, thirteen, 16);
            Layer(s, m, 1, 2, 0, 12, 14);
            s.InitLayer(0, 1, 0x8000);
            s.LoadTiles(0x8000, m(24));
            (int _, ushort[] panel) = NitroBgCodec.ReadMap(m(25));
            int scroll = up ? 5 : 0;
            s.Copy(0, 0, 0, DsBgScreen.MapSide, DsBgScreen.MapSide - scroll, panel, 0, scroll, DsBgScreen.MapSide);
            byte[] rgba = s.Render();
            if (up)
            {
                ushort[] sys = SystemColours;
                (int nx, int ny, int _, int _) = ScreenWindowLayouts.Window("party", "Name", (7, 24, 10, 2));
                (int hx, int hy, int _, int _) = ScreenWindowLayouts.Window("party", "Item heading", (17, 24, 6, 2));
                (int ix, int iy, int _, int _) = ScreenWindowLayouts.Window("party", "Held item", (17, 26, 12, 3));
                // The first Pokémon's icon, name and held item. party_menu_sprites.c puts the icon at 30,456 less the 40 the panel rose.
                PartySample.Pokemon who = PartySample.Party[0];
                string name = who.Egg ? Line(PokemonNamesTextNumbers[0], 494) : Line(PokemonNamesTextNumbers[0], who.Species);
                if (!who.Empty) PartyIcon(rgba, who.Species, nx * 8 - 26, 456 - 40 - 256);
                // The status icon sits at 50,476, 20 right of and below the icon.
                int condition = who.Empty || who.Egg ? 0 : who.Hp == 0 ? 6 : who.Condition;
                if (condition > 0) StatusIcon(rgba, condition, nx * 8 - 6, 476 - 40 - 256, true);
                Print(rgba, SystemFont, line == 8 ? Text(bank, 8) : name, nx * 8, (ny - scroll) * 8, sys[15], sys[14]);
                Print(rgba, SystemFont, Text(bank, 130), hx * 8 + 2, (hy - scroll) * 8, sys[15], sys[14]);
                Print(rgba, SystemFont, line == 182 ? Text(bank, 182) : Line(ItemNamesTextNumber, who.ItemId), ix * 8 + 2, (iy - scroll) * 8 + 4, sys[15], sys[14]);
            }
            return new Shot("Party top screen graphics: top", rgba);
        }

        private static void PartyList(byte[] rgba, int x, int y, int w, int h, List<string> choices, int picked, ushort[] sys)
        {
            StandardWindow(rgba, x, y, w, h, sys[15]);
            for (int i = 0; i < choices.Count && i * 2 < h; i++)
            {
                Print(rgba, SystemFont, choices[i], x * 8 + 8, y * 8 + 16 * i, sys[1], sys[2]);
                if (i == picked) Print(rgba, SystemFont, Views.Controls.FieldMenuWindowView.CursorCharacter.ToString(), x * 8, y * 8 + 16 * i, sys[1], sys[2]);
            }
        }

        private static void PartyButton(byte[] rgba, Func<int, byte[]> m, int x, int y, int sequence) =>
            PartySprite(rgba, m, 9, 10, 11, x, y, sequence, -1);

        // pokeheartgold party_context_menu.c sub_0207E3A8.
        private static readonly int[] ButtonFrameTiles = { 0, 2, 6, 8, 3, 5, 1, 7 };

        private static void PartyMenuButton(byte[] rgba, Func<int, byte[]> m, ushort[] colours, string window, string text,
                                            bool picked, bool quit, int index)
        {
            (int wx, int wy, int ww, int wh) = ScreenWindowLayouts.Window("party", window, quit ? (26, 20, 5, 3) : (17, 4, 14, 2));
            ushort[] row2 = DsBgScreen.Row(colours, 2);
            byte[] tiles = NitroBgCodec.ReadCharacters(m(26));
            int set = picked ? 9 : 0;
            int rx = wx - 1, ry = wy - 1, rw = ww + 2, rh = wh + 2;
            void Tile(int tile, int tx, int ty) => PartyTile(rgba, tiles, set + ButtonFrameTiles[tile], row2, tx * 8, ty * 8);
            Tile(0, rx, ry); Tile(1, rx + rw - 1, ry); Tile(2, rx, ry + rh - 1); Tile(3, rx + rw - 1, ry + rh - 1);
            for (int yy = ry + 1; yy < ry + rh - 1; yy++) { Tile(4, rx, yy); Tile(5, rx + rw - 1, yy); }
            for (int xx = rx + 1; xx < rx + rw - 1; xx++) { Tile(6, xx, ry); Tile(7, xx, ry + rh - 1); }
            Fill(rgba, row2[picked ? 11 : 4], wx * 8, wy * 8, ww * 8, wh * 8);
            // getButtonColorRaised.
            (ushort ink, ushort shadow) = quit || index < 4 ? (row2[14], row2[15]) : (row2[9], row2[10]);
            FieldFont font = Font(4) ?? SystemFont;
            if (quit) Print(rgba, font, text, wx * 8 + ww * 4, wy * 8 + 4, ink, shadow, Align.Centre);
            else Print(rgba, font, text, wx * 8, wy * 8, ink, shadow);
        }

        private static void PartyTile(byte[] rgba, byte[] tiles, int tile, ushort[] row, int px, int py)
        {
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    int at = tile * 32 + (y * 8 + x) / 2;
                    if (tiles == null || at >= tiles.Length) return;
                    int index = ((y * 8 + x) & 1) != 0 ? tiles[at] >> 4 : tiles[at] & 0xF;
                    if (index == 0 || px + x >= W || py + y >= H) continue;
                    (byte r, byte g, byte b) = NitroBgCodec.Colour(row[index]);
                    int to = ((py + y) * W + px + x) * 4;
                    rgba[to] = r; rgba[to + 1] = g; rgba[to + 2] = b; rgba[to + 3] = 255;
                }
        }

        // ── bag lists ────────────────────────────────────────────────────────────────

        // pokeplatinum src/applications/bag.
        private static Shot BagPt(int bank, int line)
        {
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.BagGraphics);
            if (s == null) return null;
            ushort[] colours = NitroBgCodec.ReadColours(m(12));
            Palettes(s, colours);
            s.SetPalette(13, DsBgScreen.Row(NitroBgCodec.ReadColours(m(22)), 0));
            Layer(s, m, 1, 1, 0, 11, 14);
            // bag/main.c ToggleHideItemSprite writes the item box onto BG1 wherever the Item frame picture is.
            (int boxX, int boxY, int _, int _) = ScreenWindowLayouts.Window("bag", "Pictures", "Item frame", (0, 18, 5, 5));
            if (PtItemBox(true) is ushort[] itemBox) s.Copy(1, boxX, boxY, 5, 5, itemBox, 0, 0, 5);
            s.InitLayer(3, 3, 0);
            s.LoadMap(3, NitroBgCodec.ReadMap(m(13)).Entries, DsBgScreen.MapSide);
            byte[] rgba = s.Render();
            ushort[] row3 = DsBgScreen.Row(colours, 3);
            (int listX, int listY, int _, int _) = ScreenWindowLayouts.Window("bag", "Item list", (14, 0, 17, 18));

            bool itemLine = bank == TextBank(392);
            // The berry menu and Poffin cooking open on the Berries pocket (4), whose first item is the Cheri Berry (149).
            bool berries = _state is 2 or 5;
            int pocket = bank == TextBank(395) ? line : berries ? 4 : 0;
            (int _, int pocketY, int _, int _) = ScreenWindowLayouts.Window("bag", "Pocket names", (0, 13, 12, 3));
            Print(rgba, SystemFont, Text(395, Math.Clamp(pocket, 0, 7)), 50, pocketY * 8 + 2, row3[1], row3[2], Align.Centre);
            pocket = Math.Clamp(pocket, 0, 7);
            int[] sample = itemLine && !_forLayout ? null : PocketSample(pocket);
            int first = sample is { Length: > 0 } ? sample[0] : berries ? 149 : itemLine ? Math.Max(1, line) : 1;
            for (int k = 1; k < 9; k++)
            {
                int x = listX * 8, y = listY * 8 + 16 * k;
                // The eighth row shows the next item when there is one, half under the frame. Close bag comes after the last item.
                bool close = (k == 8 && (sample == null || sample.Length < 8)) || (sample != null && k - 1 == sample.Length)
                             || (!itemLine && bank == TextBank(7) && line == 41 && k == 2);
                if (close) { Print(rgba, SystemFont, Text(7, 41), x, y, row3[1], row3[2]); break; }
                Print(rgba, SystemFont, Text(392, sample != null && k - 1 < sample.Length ? sample[k - 1] : first + k - 1), x, y, row3[1], row3[2]);
                Print(rgba, SystemFont, Text(7, 38), x + 109, y, row3[1], row3[2]);
                Print(rgba, SystemFont, "1", x + 134, y, row3[1], row3[2], Align.Right);
            }
            // The list and pocket name are on BG2, under the panel on BG1: a list moved left runs under it.
            s.SetVisible(3, false);
            byte[] panel = s.Render(new byte[W * H * 4]);
            s.SetVisible(3, true);
            for (int at = 0; at < panel.Length; at += 4)
                if (panel[at + 3] != 0) Buffer.BlockCopy(panel, at, rgba, at, 4);
            Func<int, byte[]> files = i => GraphicAssets.Unsqueeze(m(i));
            // bag/windows.c BagUI_DrawPocketSelectorIcons, all eight pockets: 10 by 10 from drawing 21 (colours 22, row 13), each pocket
            // 32 across and the chosen one 16 further, at 7 + 11 per pocket and 3 down in the window at 0,11.
            ushort[] iconColours = NitroBgCodec.ReadColours(m(22));
            byte[] icons = NitroBgCodec.ReadCharacters(m(21));
            (int _, int pocketRow, int _, int _) = ScreenWindowLayouts.Window("bag", "Pocket icons", (0, 11, 12, 2));
            for (int p = 0; icons != null && iconColours != null && p < 8; p++)
                BlitBitmap(rgba, icons, 32 * 8, p * 32 + (p == pocket ? 16 : 0), 0, 10, 10, 7 + 11 * p, pocketRow * 8 + 3, DsBgScreen.Row(iconColours, 0));
            // bag/sprites.c, colours ui_elements (20): the boy's bag (animations 0, cells 1, drawing 2, colours 3) is at 48,50 and
            // animates by pocket. The pocket highlight is at 7 + 11 × pocket + 6, 97, the scroll arrows at 2 and 98, 96, and the item
            // cursor at 177,24.
            PartySprite(rgba, files, 0, 1, 2, 48, 50, pocket, -1, 3);
            PartySprite(rgba, files, 23, 24, 25, 13 + 11 * pocket, pocketRow * 8 + 9, 0, -1, 20);
            PartySprite(rgba, files, 8, 9, 10, 2, pocketRow * 8 + 8, 1, -1, 20);
            PartySprite(rgba, files, 8, 9, 10, 98, pocketRow * 8 + 8, 0, -1, 20);

            PartySprite(rgba, files, 26, 27, 28, listX * 8 + 65, 24, 0, -1, 20);
            BagTopWindowsPt(rgba, first, row3);
            // bag/sprites.c BAG_SPRITE_ITEM centres the picked item's icon on 22,172. The Item icon picture is the tiles it covers.
            (int iconX, int iconY, int _, int _) = ScreenWindowLayouts.Window("bag", "Pictures", "Item icon", (1, 19, 4, 4));
            if (BagShows(_state, "Pictures", "Item icon")) ItemIcon(rgba, first, iconX * 8 + 14, iconY * 8 + 20);

            if (bank == TextBank(7) && line != 41 && line != 38 && line != 37)
            {
                ushort[] sys = SystemColours, talk = TalkColours;
                int[] actions = { 0, 6, 16, 95, 96, 1, 2, 18, 3, 4, 5, 8 };
                if (line == 78 || line == 79)
                {
                    StandardWindow(rgba, 1, 1, 10, 4, sys[15]);
                    Print(rgba, SystemFont, Text(7, 78), 8, 8, sys[1], sys[2]);
                    Print(rgba, SystemFont, Text(7, 79), 8 + 80, 24, sys[1], sys[2], Align.Right);
                }
                else if (actions.Contains(line))
                {
                    List<int> menu = line == 8 ? new List<int> { 0, 8 } : new List<int> { line, 8 };
                    ItemActionsMenu(rgba, menu);
                }
                else
                {
                    (int x, int y, int w, int h) = ScreenWindowLayouts.Window("bag", "Long message", (2, 19, 27, 4));
                    MessageWindow(rgba, x, y, w, h, talk[15]);
                    Print(rgba, TalkFont, BoxText(7, line, TalkFont), x * 8, y * 8, talk[1], talk[2]);
                }
            }
            return new Shot("Bag graphics: top", rgba);
        }

        // Set while the Layout tab draws a whole screen state instead of one text line.
        private static bool _forLayout;

        private static Dictionary<int, int[]> _pocketSamples;
        private static string _pocketSamplesRom;

        private static int? SellPrice(int item)
        {
            try { return ItemTable.Exists(item) ? new ItemData((int)ItemTable.Read(item).ItemData).FullPrice / 2 : null; }
            catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.SellPrice", ex); return null; }
        }

        // The first eight items whose data puts them in the pocket, so the list looks like that pocket in the game.
        private static int[] PocketSample(int pocket)
        {
            if (_pocketSamplesRom != RomInfo.WorkDir) { _pocketSamples = new Dictionary<int, int[]>(); _pocketSamplesRom = RomInfo.WorkDir; }
            if (_pocketSamples.TryGetValue(pocket, out int[] known)) return known;
            List<int> found = new List<int>();
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.ItemData });
                for (int item = 1; item < ItemTable.VanillaCount && found.Count < 8; item++)
                {
                    int data = (int)ItemTable.Read(item).ItemData;
                    if ((int)new ItemData(data).FieldPocket == pocket) found.Add(item);
                }
            }
            catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.PocketSample", ex); }
            return _pocketSamples[pocket] = found.ToArray();
        }

        // Window_BlitBitmapRect from a tiled 4bpp drawing <paramref name="width"/> pixels wide, colour 0 left clear.
        private static void BlitBitmap(byte[] rgba, byte[] tiles, int width, int sx, int sy, int w, int h, int dx, int dy, ushort[] row)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int px = sx + x, py = sy + y;
                    int at = ((py / 8) * (width / 8) + px / 8) * 32 + ((py % 8) * 8 + px % 8) / 2;
                    if (at >= tiles.Length) continue;
                    int v = (px & 1) != 0 ? tiles[at] >> 4 : tiles[at] & 0xF;
                    int X = dx + x, Y = dy + y;
                    if (v == 0 || X < 0 || Y < 0 || X >= W || Y >= H) continue;
                    DsBgScreen.Put(rgba, (Y * W + X) * 4, row[v]);
                }
        }

        // Platinum bag top screen windows for the Layout state (pokeplatinum bag/windows.c). Bank lines: 42 selected, 52 and 84
        // toss, 75, 80 and 81 sell, 108 and 109 Poffin count.
        private static void BagTopWindowsPt(byte[] rgba, int item, ushort[] row3)
        {
            int was = _sampleItem;
            _sampleItem = item;
            try { BagTopWindowsPtDraw(rgba, item, row3); }
            finally { _sampleItem = was; }
        }

        // bag/windows.c BagUI_ShowItemActionsMenu: 7 wide at x 24, two rows a choice growing up from row 23 (both from the Layout).
        // In the Berries pocket the menu is 8 wide at x 23 (main.c: CHECK TAG first), moved by the Screen positions patch.
        private static void ItemActionsMenu(byte[] rgba, List<int> menu, bool berries = false)
        {
            ushort[] sys = SystemColours;
            (int x, int y, int w, int h) = berries ? ScreenWindowLayouts.Window("bag", "Berry actions", (23, 15, 8, 8))
                                                   : ScreenWindowLayouts.Window("bag", "Item actions", (24, 15, 7, 8));
            int n = menu.Count, top = y + h - 2 * n;
            StandardWindow(rgba, x, top, w, 2 * n, sys[15]);
            for (int i = 0; i < n; i++) Print(rgba, SystemFont, Text(7, menu[i]), x * 8 + 8, top * 8 + 16 * i, sys[1], sys[2]);
            // The list menu opens with its cursor on the first choice.
            if (n > 0) Print(rgba, SystemFont, Views.Controls.FieldMenuWindowView.CursorCharacter.ToString(), x * 8, top * 8, sys[1], sys[2]);
        }

        private static void BagTopWindowsPtDraw(byte[] rgba, int item, ushort[] row3)
        {
            ushort[] sys = SystemColours, talk = TalkColours;
            bool Shows(string name) => BagShows(_state, "Top screen", name);
            (int X, int Y, int W, int H) At(string name, (int, int, int, int) fallback) => ScreenWindowLayouts.Window("bag", name, fallback);
            void Talk(string name, (int, int, int, int) fallback, int message)
            {
                if (!Shows(name)) return;
                (int x, int y, int w, int h) = At(name, fallback);
                MessageWindow(rgba, x, y, w, h, talk[15]);
                Print(rgba, TalkFont, BoxText(7, message, TalkFont), x * 8, y * 8, talk[1], talk[2]);
            }
            if (Shows("Description"))
            {
                (int x, int y, int _, int _) = At("Description", (0, 18, 32, 6));
                Print(rgba, SystemFont, Line(RomInfo.ItemDescriptionsTextNumber, item), x * 8 + 40, y * 8, row3[15], row3[14]);
            }
            bool tossing = _state == 3;
            Talk("Message", (6, 19, 14, 4), tossing ? 52 : 42);
            Talk("Short message", (6, 19, 13, 4), 42);
            // Selling asks how many, then offers the price with a yes/no. Tossing asks in the same box (BagUI_PrintConfirmItemTrashMsg).
            Talk("Long message", (2, 19, 27, 4), _state switch { 6 => 76, 7 => 54, _ => 75 });
            if (_state is 4 or 6)
            {
                // bag/windows.c BagUI_PrintMoney: the money window at 1,1, 10 tiles wide, two lines.
                StandardWindow(rgba, 1, 1, 10, 4, sys[15]);
                Print(rgba, SystemFont, Text(7, 78), 8, 8, sys[1], sys[2]);
                Print(rgba, SystemFont, Text(7, 79, (_, kind, _) => kind is >= 50 and <= 55 ? "3000" : null), 8 + 80, 24, sys[1], sys[2], Align.Right);
            }
            if (_state is 6 or 7 && RomInfo.FieldMenuEntriesArchive >= 0 && RomInfo.FieldYesEntry >= 0)
            {
                // sYesNoMenuTemplate: 23,13, 7 by 4, with the common YES and NO.
                StandardWindow(rgba, 23, 13, 7, 4, sys[15]);
                Print(rgba, SystemFont, Line(RomInfo.FieldMenuEntriesArchive, RomInfo.FieldYesEntry), 23 * 8 + 8, 13 * 8, sys[1], sys[2]);
                Print(rgba, SystemFont, Line(RomInfo.FieldMenuEntriesArchive, RomInfo.FieldNoEntry), 23 * 8 + 8, 13 * 8 + 16, sys[1], sys[2]);
            }
            // A typical item's menu: USE, GIVE, TRASH and CANCEL.
            if (_state == 1) ItemActionsMenu(rgba, new List<int> { 0, 3, 1, 8 });
            if (_state == 2) ItemActionsMenu(rgba, new List<int> { 4, 0, 3, 1, 8 }, berries: true);
            if (Shows("Toss amount"))
            {
                (int x, int y, int w, int h) = At("Toss amount", (24, 19, 7, 4));
                StandardWindow(rgba, x, y, w, h, sys[15]);
                Print(rgba, SystemFont, Text(7, 84), x * 8 + 16, y * 8 + 8, sys[1], sys[2]);
                CountArrows(rgba, x * 8 + 28, y * 8 + 4);
            }
            if (Shows("Sell amount"))
            {
                (int x, int y, int w, int h) = At("Sell amount", (19, 13, 12, 4));
                StandardWindow(rgba, x, y, w, h, sys[15]);
                Print(rgba, SystemFont, Text(7, 80), x * 8, y * 8 + 8, sys[1], sys[2]);
                // The shop pays half the item's price for one.
                string price = Text(7, 81, (_, kind, _) => kind is >= 50 and <= 55 ? (SellPrice(item) ?? 1).ToString() : null);
                Print(rgba, SystemFont, price, (x + w) * 8, y * 8 + 8, sys[1], sys[2], Align.Right);
                CountArrows(rgba, x * 8 + 10, y * 8 + 4);
            }
            if (Shows("Poffin count"))
            {
                (int x, int y, int w, int h) = At("Poffin count", (1, 12, 11, 4));
                StandardWindow(rgba, x, y, w, h, sys[15]);
                Print(rgba, SystemFont, Text(7, 108), x * 8, y * 8, sys[1], sys[2]);
                Print(rgba, SystemFont, Text(7, 109), (x + w) * 8, y * 8 + 16, sys[1], sys[2], Align.Right);
            }
        }

        // bag/sprites.c BagUI_ShowItemCountArrows: shop_gra's scroll arrows, up (sequence 0) at x, y and down 24 below, colours 10.
        private static void CountArrows(byte[] rgba, int x, int y)
        {
            Func<int, byte[]> shop = Members(DirNames.ShopGraphics);
            if (shop == null) return;
            const int drawing = 4, cells = 5, animations = 6, colours = 10;
            for (int i = 0; i < 2; i++) PartySprite(rgba, shop, animations, cells, drawing, x, y + 24 * i, i, -1, colours);
        }

        // pokeheartgold src/application/bag.
        // Item button screen and background screen per FieldBagButtons page (BagApp_DrawScreenLayout); -1 on the item menu and amount
        // pages, whose buttons are sprites.
        private static readonly (int Buttons, int Back)[] BagLayouts = { (43, 39), (-1, 45), (44, 42), (-1, 52), (-1, 53) };

        /// <summary>Draws the HGSS bag's bottom screen for a FieldBagButtons page with the first items listed.</summary>
        internal static byte[] BagBottomHg(int page, BagArt art = null) => BagHg(TextBank(222), 1, BagLayouts[page], art)?.Rgba;

        /// <summary>The moved button maps and item icon spots, with the bag item buttons patch.</summary>
        internal sealed record BagArt(IReadOnlyDictionary<int, byte[]> Maps, (int X, int Y)[] Icons);

        internal static BagArt BagArtOf(FieldBagButtons bag) => bag?.MovesWholeButtons == true
            ? new BagArt(BagButtonsPatch.Maps.ToDictionary(m => m, bag.Map), Enumerable.Range(0, BagButtonsPatch.Buttons).Select(bag.IconOf).ToArray())
            : null;

        private static byte[] BagMap(Func<int, byte[]> m, BagArt art, int member) => art?.Maps.TryGetValue(member, out byte[] moved) == true ? moved : m(member);

        // title_screen.c: HGSS TOUCH TO START (719/0) at 0,18 on the bottom, RGB 27,8,0 (HG) or 0,28,31 (SS); Pt PRESS START (609/0)
        // at 2,19 on the top over the border and logo, RGB 21,0,0. Both one pixel between letters.
        private static Shot TitleStartLine()
        {
            byte[] rgba = Pt ? TitleTopPt() : Blank(Rgb(0, 0, 0));
            string text = Text(Pt ? 609 : 719, 0);
            ushort ink = Pt ? Rgb(21, 0, 0) : GameVersion == GameVersions.SoulSilver ? Rgb(0, 28, 31) : Rgb(27, 8, 0);
            int row = Pt ? 19 : 18;
            string[] lines = text.Split('\n');
            for (int l = 0; l < lines.Length; l++)
            {
                string[] letters = lines[l].Select(c => c.ToString()).ToArray();
                int width = letters.Sum(c => DsBgScreen.MeasureText(SystemFont, c) + 1) - 1;
                int x = (W - width) / 2;
                foreach (string c in letters)
                {
                    Print(rgba, SystemFont, c, x, row * 8 + 16 * l, ink, ink);
                    x += DsBgScreen.MeasureText(SystemFont, c) + 1;
                }
            }
            return new Shot($"Title screen graphics: {(Pt ? "top" : "bottom")}", rgba, () => AvaloniaEditorLauncher.OpenTitleScreenEditor());
        }

        // Platinum's top screen behind the start line: the border, then the logo scrolled up a pixel (Bg_SetOffset y 1).
        private static byte[] TitleTopPt()
        {
            TitleScreenGraphics graphics = new TitleScreenGraphics();
            byte[] rgba = Blank(Rgb(0, 0, 0));
            foreach ((RawImage piece, int down) in new[] { (graphics.ComposeBackground(), 0), (graphics.ComposeLogo(), 1) })
            {
                if (piece == null) continue;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W && x < piece.Width; x++)
                    {
                        int from = y + down;
                        if (from < 0 || from >= piece.Height) continue;
                        int at = (from * piece.Width + x) * 4;
                        if (piece.Bgra[at + 3] == 0) continue;
                        int to = (y * W + x) * 4;
                        (rgba[to], rgba[to + 1], rgba[to + 2], rgba[to + 3]) = (piece.Bgra[at + 2], piece.Bgra[at + 1], piece.Bgra[at], 255);
                    }
            }
            return rgba;
        }

        private static readonly (string Name, int Page)[] BattleHeaderPages =
            { ("Summary", 2), ("Moves", 3), ("Move info", 4), ("Restore PP", 5), ("Forget a move", 6), ("Forget the move", 7) };

        private static string[] PokegearApps => new[] { "Phone", "Radio", "Map", "Fly map", "Skins" };
        private static string[] PokegearStates => PokegearApps.Append("Phone contact menu").ToArray();
        private const int PokegearPhoneMenu = 5;

        // touchscreen_list_menu.c: frames from data/sbox_gra (colours 0, drawing 1) round each choice, three rows apart, the first
        // highlighted (frame tiles 12 on). Each choice is a window one in and one down, filled with colour 3, its text in font 4
        // centred in colours 1 and 2.
        private static void TouchListMenu(byte[] rgba, string[] items, int x, int y, int width, int cursor = 0)
        {
            Func<int, byte[]> m = Members(DirNames.TouchListGraphics);
            if (m == null || items.Length == 0) return;
            ushort[] row = DsBgScreen.Row(NitroBgCodec.ReadColours(m(0)), 0);
            DsBgScreen s = new DsBgScreen();
            s.SetPalette(1, row, 0);
            s.InitLayer(0, 0, 0);
            // One tile in, so the empty map's tile 0 stays clear.
            s.LoadTiles(0, m(1), 1);
            int last = items.Length - 1;
            void Band(int tile, int at)
            {
                s.Fill(0, tile + 1, x, at, 1, 1, 1);
                s.Fill(0, tile + 2, x + 1, at, width, 1, 1);
                s.Fill(0, tile + 3, x + width + 1, at, 1, 1, 1);
            }
            Band(cursor == 0 ? 12 : 0, y);
            Band(cursor == last ? 21 : 9, y + items.Length * 3);
            for (int i = 0; i < last; i++)
                Band(cursor == i ? 18 : cursor == i + 1 ? 24 : 6, y + i * 3 + 3);
            for (int i = 0; i < items.Length; i++)
            {
                int side = cursor == i ? 15 : 3;
                s.Fill(0, side + 1, x, y + i * 3 + 1, 1, 2, 1);
                s.Fill(0, side + 3, x + width + 1, y + i * 3 + 1, 1, 2, 1);
            }
            byte[] drawn = s.Render(rgba);
            Array.Copy(drawn, rgba, rgba.Length);
            FieldFont font = Font(4);
            for (int i = 0; i < items.Length; i++)
            {
                int wx = (x + 1) * 8, wy = (y + 1 + 3 * i) * 8;
                Fill(rgba, row[3], wx, wy, width * 8, 16);
                int at = (width * 8 - DsBgScreen.MeasureText(font, items[i])) / 2;
                Print(rgba, font, items[i], wx + at, wy, row[1], row[2]);
            }
        }

        // Platinum's states follow its top screen. HGSS's follow its bottom screen pages, plus the TM pocket.
        private static string[] BagStates => Pt ? new[] { "Items", "Item menu", "Berry menu", "Toss", "Sell", "Poffin cooking", "Sell yes/no", "Toss yes/no" }
                                                : new[] { "Items", "TMs and HMs", "Item menu", "TM menu", "Amount", "Sell" };
        // HGSS: the bottom screen page each state shows. TM menu is Item menu opened on a TM.
        private static readonly int[] BagStatePages = { 0, 0, 1, 1, 3, 4 };
        private const int BagSellHg = 5;

        /// <summary>The HGSS bag's bottom screen page for a Layout state.</summary>
        internal static int BagPageOf(int state) => BagStatePages[Math.Clamp(state, 0, BagStatePages.Length - 1)];
        private const int BagTmMenu = 3;
        /// <summary>A map block that sits where <paramref name="Window"/> is: <paramref name="Shown"/> there, <paramref name="Hidden"/> at home once it moves.</summary>
        /// <param name="Shown">Null moves the block as saved.</param>
        /// <param name="PlainFrom">The column to start looking for each row's plain square, after any frame.</param>
        public sealed record ScreenSwap(string Window, DirNames Dir, int Member, int HomeX, int HomeY, int Width, int Height,
                                        Func<ushort[]> Hidden, Func<ushort[]> Shown, int PlainFrom = 0);

        internal static ushort[] PtItemBox(bool shown)
        {
            try { return ScreenCodeData.PtItemBox(shown); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { AppLogger.WarnOnce("GameTextScreens.PtItemBox", ex); return null; }
        }

        /// <summary><paramref name="Rows"/> rows of each map from <paramref name="Above"/> rows above <paramref name="Window"/>, drawn behind it.</summary>
        public sealed record ScreenBand(string Window, DirNames Dir, int[] Members, int Above, int Rows);

        private static bool BagShowsTms => Hg && _state is 1 or BagTmMenu;

        // pokeheartgold BagApp_PrepareContextMenu, bank 10 lines: USE (0) with a field use, then REGISTER (2) for a selectable item
        // or TRASH (1) unless it can't be tossed or is a TM, GIVE (3) unless it can't be tossed, MOVE (75) outside TMs and Berries.
        // An empty slot is -1.
        private static int[] HgBagMenu(int item, bool tm)
        {
            ItemData data = null;
            try { if (ItemTable.Exists(item)) data = new ItemData((int)ItemTable.Read(item).ItemData); }
            catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.HgBagMenu", ex); }
            if (data == null) return new[] { 0, 1, 3, 75 };
            int second = data.Selectable ? 2 : !data.PreventToss && !tm ? 1 : -1;
            return new[]
            {
                data.FieldUseFunc != 0 ? 0 : -1, second, data.PreventToss ? -1 : 3,
                tm || data.FieldPocket == FieldPocket.Berries ? -1 : 75,
            };
        }

        private static Shot BagOtherScreen(int state)
        {
            if (Pt) return BagBottomPt((ScreenWindowLayouts.ButtonsInUse<TouchListButtons>() ?? PlatinumTouchLists.Bag())?.SpotsOf(0)) is byte[] bottom ? new Shot("Bag graphics: bottom", bottom) : null;
            byte[] top = BagTopHg(state);
            return top == null ? null : new Shot("Bag graphics: top", top);
        }

        // The game draws the player holding the bag as a 3D model here, which the preview leaves out. Dark grey reads on the white sky.
        private const ushort PlaceholderInk = 0x294A, PlaceholderShadow = 0x5EF7;

        private static void ModelPlaceholder(byte[] rgba, ushort ink, ushort shadow)
        {
            const int left = 88, top = 8, right = 168, bottom = 136;
            for (int x = left; x <= right; x++)
                if ((x / 4 & 1) == 0) { DsBgScreen.Put(rgba, (top * W + x) * 4, ink); DsBgScreen.Put(rgba, (bottom * W + x) * 4, ink); }
            for (int y = top; y <= bottom; y++)
                if ((y / 4 & 1) == 0) { DsBgScreen.Put(rgba, (y * W + left) * 4, ink); DsBgScreen.Put(rgba, (y * W + right) * 4, ink); }
            Print(rgba, SystemFont, "3D bag model\ngoes here", (left + right) / 2, (top + bottom) / 2 - 16, ink, shadow, Align.Centre);
        }

        // bag_app_main.c: tiles 7 serve BG2, the backdrop (map 54, or 9 once a TM's menu is open), and BG3, the player (map 94).
        // TM detail lines in bank 10: 101 type, 92 category, 89 PP, 90 power, 91 accuracy, 93 and 94 their numbers.
        private static byte[] BagTopHg(int state)
        {
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.BagGraphics);
            if (s == null) return null;
            ushort[] colours = NitroBgCodec.ReadColours(m(8));
            Palettes(s, colours);
            s.SetPalette(13, DsBgScreen.Row(NitroBgCodec.ReadColours(m(17)), 0));
            s.InitLayer(2, 1, 0x8000);
            s.InitLayer(3, 2, 0x8000);
            s.LoadTiles(0x8000, m(7));
            s.LoadMap(2, NitroBgCodec.ReadMap(m(state == BagTmMenu ? 9 : 54)).Entries, DsBgScreen.MapSide);
            s.LoadMap(3, NitroBgCodec.ReadMap(m(94)).Entries, DsBgScreen.MapSide);
            byte[] rgba = s.Render();
            ushort[] text = DsBgScreen.Row(colours, 4);
            ModelPlaceholder(rgba, PlaceholderInk, PlaceholderShadow);
            if (BagShows(state, "Top screen", "Description"))
            {
                (int x, int y, int _, int _) = ScreenWindowLayouts.Window("bag", "Description", (0, 18, 32, 6));
                // In the TM pocket the first TM (item 328) is described.
                int[] sample = PocketSample(0);
                int described = state is 1 or BagTmMenu ? 328 : sample is { Length: > 0 } ? sample[0] : 1;
                Print(rgba, SystemFont, Line(RomInfo.ItemDescriptionsTextNumber, described), x * 8 + 20, y * 8, text[15], text[14]);
            }
            if (BagShows(state, "Top screen", "TM details"))
            {
                (int x, int y, int _, int _) = ScreenWindowLayouts.Window("bag", "TM details", (0, 13, 32, 4));
                (int Line, int X, int Y)[] labels = { (101, 0, 0), (89, 0, 16), (92, 72, 0), (90, 168, 0), (91, 168, 16) };
                foreach ((int l, int lx, int ly) in labels) Print(rgba, SystemFont, Text(10, l), x * 8 + lx, y * 8 + ly, text[15], text[14]);
                // bag_app_window.c BagApp_PrintTMHMDetails and bag_app_sprite.c BagApp_DrawTMHMMoveDetails, for TM01.
                Data.MoveInfo.Facts? move = Data.MoveInfo.Of(Data.MoveInfo.TaughtByItem(328));
                if (move is Data.MoveInfo.Facts f)
                {
                    string Value(int line, string number) => Text(10, line, (_, _, _) => number);
                    Print(rgba, SystemFont, Value(93, f.Pp.ToString().PadLeft(2)), x * 8 + 48, y * 8 + 16, text[15], text[14]);
                    Print(rgba, SystemFont, f.Power <= 1 ? Text(10, 25) : Value(94, f.Power.ToString()), x * 8 + 232, y * 8, text[15], text[14]);
                    Print(rgba, SystemFont, f.Accuracy == 0 ? Text(10, 25) : Value(94, f.Accuracy.ToString()), x * 8 + 232, y * 8 + 16, text[15], text[14]);
                    GraphicAssets.Preview type = TypeIcons.PixelsFor(f.Type), category = TypeIcons.CategoryPixels(f.Category);
                    // The icons' templates sit 48 and 144 (Japanese 136) right of the window and 8 below it, and follow it.
                    int left = x * 8, below = y * 8 + 8, categoryX = RomInfo.GameLanguage == GameLanguages.Japanese ? 136 : 144;
                    if (type != null) Paste(rgba, type, left + 48 - type.Width / 2, below - type.Height / 2);
                    if (category != null) Paste(rgba, category, left + categoryX - category.Width / 2, below - category.Height / 2);
                }
            }
            return rgba;
        }

        private static bool BagShows(int state, string group, string name)
        {
            if (Pt)
            {
                if (name is "Item list" or "Pocket names" or "Pocket icons") return true;
                if (name is "Item icon" or "Item frame") name = "Description";
                return state switch
                {
                    0 => name == "Description",
                    1 => name is "Message" or "Item actions",
                    2 => name is "Short message" or "Berry actions",
                    3 => name is "Message" or "Toss amount",
                    4 => name is "Long message" or "Sell amount",
                    6 or 7 => name == "Long message",
                    _ => name is "Description" or "Poffin count",
                };
            }
            if (group != "Bottom screen") return name == "Description" || (name == "TM details" && state == BagTmMenu);
            if (name == "Cancel") return true;
            return BagStatePages[Math.Clamp(state, 0, BagStatePages.Length - 1)] switch
            {
                0 => name.StartsWith("Item ", StringComparison.Ordinal) || name == "Page",
                1 => name.StartsWith("Option ", StringComparison.Ordinal) || name is "Selected item" or "Item action",
                // Selling uses two digits.
                4 => name is "Digit 1" or "Digit 2" or "Price" or "Sell or toss" or "Prompt",
                _ => name.StartsWith("Digit ", StringComparison.Ordinal) || name is "Price" or "Sell or toss" or "Prompt" or "Item action",
            };
        }

        // bag_app_window.c BAG_APP_WINDOW_SUB_ITEM_ACTION, 2,1 (27 by 2), moved by the Screen positions patch.
        private static (int X, int Y) HgItemActionAt()
        {
            (int x, int y, int _, int _) = ScreenWindowLayouts.Window("bag", "Item action", (2, 1, 27, 2));
            return (x, y);
        }

        private static Shot BagHg(int bank, int line) =>
            BagHg(bank, line, BagLayouts[BagStatePages[Math.Clamp(_state, 0, BagStatePages.Length - 1)]], BagArtOf(ScreenWindowLayouts.ButtonsInUse<FieldBagButtons>()));

        private static Shot BagHg(int bank, int line, (int Buttons, int Back) layout, BagArt art)
        {
            if (bank == TextBank(10) && line >= 120 && line <= 127)
            {
                FieldFont font = Font(0);
                byte[] top = new MoveTextScreens().Render(MoveTextScreens.Screen.Bag, Text(10, line), font);
                return top == null ? null : new Shot("Bag graphics: top", top);
            }
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.BagGraphics);
            if (s == null) return null;
            ushort[] colours = NitroBgCodec.ReadColours(m(38));
            Palettes(s, colours);
            ushort[] items = NitroBgCodec.ReadColours(m(8));
            for (int r = 0; r < 4; r++) s.SetPalette(8 + r, items, r * 16);
            // BagApp_LoadBgPaletteByPocket: the pocket's row into rows 0, 1 and 3, the next pocket's into row 2 (Items, or TMs and HMs).
            ushort[] pockets = NitroBgCodec.ReadColours(m(41));
            int pocket = BagShowsTms ? 3 : 0;
            ushort[][] pocketRows = { DsBgScreen.Row(pockets, pocket), DsBgScreen.Row(pockets, pocket), DsBgScreen.Row(pockets, pocket + 1), DsBgScreen.Row(pockets, pocket) };
            for (int r = 0; r < pocketRows.Length; r++) s.SetPalette(r, pocketRows[r]);
            s.InitLayer(1, 2, 0);
            s.LoadTiles(0, m(46));
            if (layout.Buttons >= 0) s.LoadMap(1, NitroBgCodec.ReadMap(BagMap(m, art, layout.Buttons)).Entries, DsBgScreen.MapSide);
            s.InitLayer(2, 3, 0);
            s.LoadMap(2, NitroBgCodec.ReadMap(BagMap(m, art, layout.Back)).Entries, DsBgScreen.MapSide);
            ExtendedColours(s, m, 1, BottomPalettesPatch.Bag, gameRows: pocketRows);
            ExtendedColours(s, m, 2, BottomPalettesPatch.Bag, gameRows: pocketRows);
            byte[] rgba = s.Render();
            ushort[] row11 = DsBgScreen.Row(items, 3);
            if (layout.Buttons >= 0) BagSpritesHg(rgba, m);

            bool itemLine = bank == TextBank(222);
            int[] sample = itemLine && !_forLayout ? null : PocketSample(0);
            int first = sample is { Length: > 0 } ? sample[0] : itemLine ? Math.Max(1, line) : 1;
            (int X, int Y)[] buttons = { (4, 5), (20, 5), (4, 10), (20, 10), (4, 15), (20, 15) };
            (int X, int Y)[] templates = ScreenCodeData.HgBagIconSpots();
            // The TM pocket names each TM by its move, with its number below (bag_app_window.c BagApp_PrintTMorHMNumberOnWindow).
            int[] tms = BagShowsTms ? TMEditor.ReadMachineMoves() : null;
            ushort[] obj = BagSpriteColoursHg(m);
            for (int i = 0; i < buttons.Length && layout.Buttons >= 0; i++)
            {
                (int wx, int wy, int _, int _) = ScreenWindowLayouts.Window("bag", $"Item {i + 1}", (buttons[i].X, buttons[i].Y, 11, 4));
                int x = wx * 8, y = wy * 8;
                bool tm = tms != null && i < tms.Length;
                int listed = tm ? 328 + i : sample != null && i < sample.Length ? sample[i] : first + i;
                // bag_app_sprite.c ITEM_ICON_1 to 6 and the cursor sit 10 left of and 19 below each button's game corner. Only the
                // bag item buttons patch moves them with a window.
                (int iconX, int iconY) = art?.Icons[i] ?? (templates != null ? templates[i] : (buttons[i].X * 8 - 10, buttons[i].Y * 8 + 19));
                ItemIcon(rgba, listed, iconX, iconY);
                Print(rgba, SystemFont, tm ? Line(MoveNamesTextNumbers, tms[i]) : Text(222, listed), x, y, row11[1], row11[2]);
                if (tm) Print(rgba, SystemFont, $"No.{i + 1:00}", x, y + 16, row11[1], row11[2]);
                Print(rgba, SystemFont, Text(10, 87, (_, _, _) => "1"), x + 48, y + 16, row11[1], row11[2]);
                // sCursorSpriteParam_PocketsItemsContext: the red frame on the first item, animation 10 in colour row 9.
                if (i == 0 && obj != null && layout.Back != BagLayouts[1].Back)
                    Sprite(rgba, m, 50, 49, 51, iconX + 26, iconY - 3, 10, _ => DsBgScreen.Row(obj, 9));
            }
            if (layout.Back == BagLayouts[1].Back)
            {
                // The item menu for the selected item, its icon and "The X item is selected."
                int[] options = HgBagMenu(tms != null ? 328 : first, tms != null);
                // bag_app_sprite.c: the option buttons are animation 22 at 48 and 144 across, 144 and 176 down, only on used slots.
                // The picked item's icon moves to 86,76.
                for (int i = 0; obj != null && i < options.Length; i++)
                    if (options[i] >= 0) Sprite(rgba, m, 50, 49, 51, 48 + 96 * (i % 2), 144 + 32 * (i / 2), 22, _ => DsBgScreen.Row(obj, 8));
                for (int i = 0; i < options.Length; i++)
                {
                    if (options[i] < 0) continue;
                    (int ox, int oy, int ow, int _) = ScreenWindowLayouts.Window("bag", $"Option {i + 1}", (1 + 12 * (i % 2), 17 + 4 * (i / 2), 10, 2));
                    Print(rgba, SystemFont, Text(10, options[i]), ox * 8 + ow * 4, oy * 8, row11[1], row11[2], Align.Centre);
                }
                (int sx, int sy, int _, int _) = ScreenWindowLayouts.Window("bag", "Selected item", (12, 7, 11, 4));
                // bag_app_window.c BagApp_PrintItemNameAndMaybeQuantityOnWindow: a TM shows its move and number like the list does.
                int item = tms != null ? 328 : first;
                ItemIcon(rgba, item, 86, 76);
                Print(rgba, SystemFont, tms != null && tms.Length > 0 ? Line(MoveNamesTextNumbers, tms[0]) : Text(222, item), sx * 8 + 2, sy * 8, row11[1], row11[2]);
                if (tms != null) Print(rgba, SystemFont, "No.01", sx * 8 + 2, sy * 8 + 16, row11[1], row11[2]);
                Print(rgba, SystemFont, Text(10, 87), sx * 8 + 48, sy * 8 + 16, row11[1], row11[2]);
                if (bank != TextBank(10))
                {
                    string picked = Text(10, 43, (_, _, _) => Text(222, item));
                    (int ax, int ay) = HgItemActionAt();
                    MessageWindow(rgba, ax, ay, 27, 2, TalkColours[15]);
                    Print(rgba, TalkFont, picked, ax * 8, ay * 8, TalkColours[1], TalkColours[2]);
                }
            }
            bool amount = layout.Back == BagLayouts[3].Back || layout.Back == BagLayouts[4].Back;
            if (amount)
            {
                // BagApp_ItemContextMenu_Trash: the picked item stays, the digits read 001 and TRASH (bank 10 line 1) sits on its button.
                // All six arrows show when the bag holds 100 or more of the item.
                int item = first;
                ItemIcon(rgba, item, 86, 76);
                (int sx, int sy, int _, int _) = ScreenWindowLayouts.Window("bag", "Selected item", (12, 7, 11, 4));
                Print(rgba, SystemFont, Text(222, item), sx * 8 + 2, sy * 8, row11[1], row11[2]);
                Print(rgba, SystemFont, Text(10, 87), sx * 8 + 48, sy * 8 + 16, row11[1], row11[2]);
                // The item menu's "The X item is selected." stays up. Selling asks "How many will you sell?" in the Prompt window
                // (BagApp_PrintMessage's sub window) instead.
                bool sell = _state == BagSellHg;
                if (sell)
                {
                    (int qx, int qy, int qw, int qh) = ScreenWindowLayouts.Window("bag", "Prompt", (11, 1, 18, 4));
                    MessageWindow(rgba, qx, qy, qw, qh, TalkColours[15]);
                    Print(rgba, TalkFont, Text(10, 77, (_, _, _) => Text(222, item)), qx * 8, qy * 8, TalkColours[1], TalkColours[2]);
                }
                else
                {
                    (int ax, int ay) = HgItemActionAt();
                    MessageWindow(rgba, ax, ay, 27, 2, TalkColours[15]);
                    Print(rgba, TalkFont, Text(10, 43, (_, _, _) => Text(222, item)), ax * 8, ay * 8, TalkColours[1], TalkColours[2]);
                }
                // BagApp_PrintQuantityDigitWindows: selling shows two digits (up to 99), the rest three.
                int digits = sell ? 2 : 3;
                for (int i = 0; i < digits; i++)
                {
                    (int dx, int dy, int _, int _) = ScreenWindowLayouts.Window("bag", $"Digit {i + 1}", (16 + 4 * i, 14, 2, 3));
                    Print(rgba, SystemFont, i == digits - 1 ? "1" : "0", dx * 8, dy * 8 + 4, row11[1], row11[2]);
                }
                for (int i = 0; obj != null && i < 6; i++)
                    if (i % 3 < digits) Sprite(rgba, m, 50, 49, 51, 136 + 32 * (i % 3), i < 3 ? 104 : 152, i < 3 ? 25 : 27, _ => DsBgScreen.Row(obj, 8));
                if (obj != null) Sprite(rgba, m, 50, 49, 51, 136, 176, 37, _ => DsBgScreen.Row(obj, 8));
                (int tx, int ty, int _, int _) = ScreenWindowLayouts.Window("bag", "Sell or toss", (14, 21, 7, 2));
                Print(rgba, SystemFont, Text(10, sell ? 86 : 1), tx * 8 + 5, ty * 8, row11[15], row11[14]);
                if (sell)
                {
                    // BagApp_PrintMoneyOnWindow (0,0, 9 by 4): "Money" at 4,0 and the amount, six wide, ending 8 left of 68.
                    string money = Text(10, 81, (_, _, _) => "3000".PadLeft(6));
                    Print(rgba, SystemFont, Text(10, 80), 4, 0, row11[1], row11[2]);
                    Print(rgba, SystemFont, money, 68 - (DsBgScreen.MeasureText(SystemFont, money) + 8), 16, row11[1], row11[2]);
                    // BagApp_PrintSaleTotalInWindow: half the price for one, at 0,4 in the Price window.
                    (int px, int py, int _, int _) = ScreenWindowLayouts.Window("bag", "Price", (24, 14, 8, 3));
                    string total = Text(10, 83, (_, _, _) => (SellPrice(item) ?? 1).ToString().PadLeft(6));
                    Print(rgba, SystemFont, total, px * 8, py * 8 + 4, row11[1], row11[2]);
                }
            }
            (int pageX, int pageY, int _, int _) = ScreenWindowLayouts.Window("bag", "Page", (10, 21, 7, 2));
            // BagApp_PrintPageCounter: page and page count each padded to three on the right. Eight sample items make two pages.
            int pages = tms != null ? (tms.Length + 5) / 6 : sample is { Length: > 0 } ? (sample.Length + 5) / 6 : 1;
            string counter = Text(10, 22, (_, _, buffer) => (buffer == 0 ? 1 : pages).ToString().PadLeft(3));
            if (layout.Buttons >= 0) Print(rgba, SystemFont, counter, pageX * 8, pageY * 8, row11[15], row11[1]);
            // bag_app_sprite.c: the B button is animation 16 at 224,176, and 39 while picking an amount.
            ushort[] bObj = BagSpriteColoursHg(m);
            if (bObj != null) Sprite(rgba, m, 50, 49, 51, 224, 176, amount ? 39 : 16, _ => DsBgScreen.Row(bObj, 8));
            (int cancelX, int cancelY, int _, int _) = ScreenWindowLayouts.Window("bag", "Cancel", (24, 21, 7, 2));
            Print(rgba, SystemFont, Text(10, 8), cancelX * 8 + 32, cancelY * 8, row11[15], row11[14], Align.Centre);

            if (bank == TextBank(10) && line != 87 && line != 22 && line != 8)
            {
                ushort[] talk = TalkColours;
                (int ax, int ay) = HgItemActionAt();
                MessageWindow(rgba, ax, ay, 27, 2, talk[15]);
                Print(rgba, TalkFont, BoxText(10, line, TalkFont), ax * 8, ay * 8, talk[1], talk[2]);
            }
            return new Shot("Bag graphics: bottom", rgba);
        }

        // Template palette rows override the cells' (sSpriteTemplates); BagApp_LoadObjPaletteByPocket loads the pocket rows from file 48.
        // The bottom screen's sprite colours: file 47, with the pocket colours from file 48 over its first rows.
        private static ushort[] BagSpriteColoursHg(Func<int, byte[]> m)
        {
            ushort[] obj = NitroBgCodec.ReadColours(m(47)), pockets = NitroBgCodec.ReadColours(m(48));
            if (obj == null || pockets == null) return null;
            obj = (ushort[])obj.Clone();
            Array.Resize(ref obj, Math.Max(obj.Length, 256));
            Array.Copy(pockets, 128, obj, 0, Math.Min(128, pockets.Length - 128));
            Array.Copy(pockets, 0, obj, 0, 16);
            return obj;
        }

        private static void BagSpritesHg(byte[] rgba, Func<int, byte[]> m)
        {
            ushort[] obj = BagSpriteColoursHg(m);
            if (obj == null) return;
            for (int i = 0; i < 8; i++)
            {
                int row = i;
                Sprite(rgba, m, 50, 49, 51, 16 + 32 * i, 16, i, _ => DsBgScreen.Row(obj, row));
            }
            Sprite(rgba, m, 50, 49, 51, 24, 176, 12, _ => DsBgScreen.Row(obj, 8));
            Sprite(rgba, m, 50, 49, 51, 64, 176, 13, _ => DsBgScreen.Row(obj, 8));
        }

        // ── Platinum start menu ──────────────────────────────────────────────────────

        // pokeplatinum src/start_menu.c, with everything unlocked.
        private static readonly int[] StartMenuRows = { 0, 1, 2, 3, 4, 5, 6 };

        // pokeheartgold start_menu.c Task_StartMenu_DrawCursor: X lays a panel over the field on the top screen (a/0/1/4 drawing 12,
        // map 13, colours 15 in row 14) with a sprite at 100,144 (64, 61, 62, 63) pointing down at the touch menu.
        private static Shot StartMenuTopHg()
        {
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.FieldTouchMenu);
            if (s == null) return null;
            s.SetPalette(14, NitroBgCodec.ReadColours(m(15)), 0);
            Layer(s, m, 3, 3, 0, 12, 13);
            byte[] rgba = s.Render(Blank(FieldBackdrop));
            if (NitroBgCodec.ReadColours(m(61)) is ushort[] arrow) Sprite(rgba, m, 63, 62, 64, 100, 144, 0, _ => DsBgScreen.Row(arrow, 0));
            return new Shot("Start menu graphics: top", rgba);
        }

        // start_menu.c StartMenu_DrawBallCount: the Safari or Park ball count, 12 by 4 at 1,1 in the field frame, its label then the
        // count 16 lower. The Screen positions patch moves it.
        private static void BallCount(byte[] rgba, int line)
        {
            ushort[] sys = SystemColours;
            (int x, int y, int _, int _) = ScreenWindowLayouts.Window("startmenu", "Windows", "Ball count", (1, 1, 12, 4));
            StandardWindow(rgba, x, y, 12, 4, sys[15], FieldFrame);
            Print(rgba, SystemFont, Text(367, line == 11 ? 9 : line), x * 8, y * 8, sys[1], sys[2]);
            Print(rgba, SystemFont, Text(367, 11), x * 8, y * 8 + 16, sys[1], sys[2]);
        }

        private static Shot StartMenuPt(int line, bool ballCount = false)
        {
            const int bank = 367;
            byte[] rgba = Blank(FieldBackdrop);
            ushort[] talk = TalkColours, sys = SystemColours;

            if (line >= 9 && line <= 11)
            {
                BallCount(rgba, line);
                return new Shot("Start menu graphics: top", rgba);
            }
            if (ballCount) BallCount(rgba, 9);

            List<int> rows = StartMenuRows.ToList();
            if (line == 7 || line == 8) rows.Insert(0, line);
            int cursor = Math.Max(0, rows.IndexOf(line));
            // The window carries the cursor and icons along (RomLocator.StartMenuCodeSitesIn).
            (int mx, int my, int _, int _) = ScreenWindowLayouts.Window("startmenu", "Windows", "Menu", (20, 1, 11, 21));
            int dx = 8 * (mx - 20), dy = 8 * (my - 1);
            StandardWindow(rgba, mx, my, 11, rows.Count * 3, talk[15], FieldFrame);

            Func<int, byte[]> m = Members(DirNames.StartMenuGraphics);
            if (m != null)
            {
                ushort[] obj = NitroBgCodec.ReadColours(m(5));
                List<DsBgScreen.Oam[]> cursorCells = DsBgScreen.ReadCells(m(1));
                byte[] cursorTiles = NitroBgCodec.ReadCharacters(m(2));
                if (cursorCells.Count > 0)
                    DsBgScreen.DrawCell(rgba, cursorCells[0], cursorTiles, _ => DsBgScreen.Row(obj, 1), 204 + dx, 20 + 24 * cursor + dy);
                List<DsBgScreen.Oam[]> icons = DsBgScreen.ReadCells(m(4));
                byte[] iconTiles = NitroBgCodec.ReadCharacters(m(6));
                for (int i = 0; i < rows.Count; i++)
                {
                    int cell = rows[i] == 7 ? 7 : rows[i] == 8 ? 8 : rows[i];
                    if (cell < icons.Count)
                        DsBgScreen.DrawCell(rgba, icons[cell], iconTiles, _ => DsBgScreen.Row(obj, i == cursor ? 1 : 0), 174 + dx, 20 + 24 * i + dy);
                }
            }
            for (int i = 0; i < rows.Count; i++)
                Print(rgba, TalkFont, Text(bank, rows[i]), mx * 8 + 28, my * 8 + 4 + 24 * i, talk[1], talk[2]);
            return new Shot("Start menu graphics: top", rgba);
        }
    }
}
