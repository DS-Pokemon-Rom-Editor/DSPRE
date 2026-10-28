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
            public string[] LayoutStates { get; init; }
            /// <summary>Whether this game has a Layout-only state; null when it has all of them.</summary>
            public Func<int, bool> HasLayoutState { get; init; }
            /// <summary>The state a Layout-only state is drawn as.</summary>
            public Func<int, int> DrawnAs { get; init; }
            /// <summary>Draws the screen the shots don't show, for the Layout tab; null if there is none.</summary>
            public Func<int, Shot> OtherScreen { get; init; }
            /// <summary>Rows a background is scrolled up in a state.</summary>
            public Func<int, int, int> RowsScrolled { get; init; }
            /// <summary>The order the game puts windows down; null keeps the table order.</summary>
            public Func<ScreenWindowLayout.Window, int> PutOrder { get; init; }
            /// <summary>The block the game moves a window with, which the window must stay inside; null if none.</summary>
            public Func<ScreenWindowLayout.Window, (int X, int Y, int W, int H)?> Block { get; init; }
            /// <summary>The row the game's code forces a window to in a state; null if the table decides.</summary>
            public Func<int, ScreenWindowLayout.Window, int?> FixedTop { get; init; }
            /// <summary>Windows that take turns share this name; null for the rest.</summary>
            public Func<ScreenWindowLayout.Window, string> TakesTurns { get; init; }
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
                    Draw = (bank, line) => Pt ? OptionsPt(line) : OptionsHg(line), FirstLine = 3 },
            new() { Key = "mainmenu", Title = "Main menu", Banks = () => Pt ? Us(550) : Hg ? Us(442) : Array.Empty<int>(),
                    Draw = (bank, line) => MainMenu(line, Hg) },
            new() { Key = "startmenu", Title = "Start menu", Banks = () => Pt ? Us(367) : Array.Empty<int>(),
                    Draw = (bank, line) => StartMenuPt(line) },
            new() { Key = "banner", Title = "Place-name banner",
                    Banks = () => (Pt || Hg) && LocationNamesTextNumber >= 0 ? new[] { LocationNamesTextNumber } : Array.Empty<int>(),
                    Draw = (bank, line) => Banner(line, Hg), FirstLine = 1 },
            new() { Key = "naming", Title = "Naming screen", Banks = () => Pt ? Us(422) : Hg ? Us(249) : Array.Empty<int>(),
                    Draw = (bank, line) => NamingPrompt(line, Hg) },
            new() { Key = "trainercard", Title = "Trainer card", Banks = () => Pt ? Us(616) : Hg ? Us(727) : Array.Empty<int>(),
                    Draw = (bank, line) => TrainerCard(line, Hg) },
            new() { Key = "box", Title = "PC boxes", Banks = () => Pt ? Us(19, 18) : Hg ? Us(25, 24) : Array.Empty<int>(),
                    Draw = (bank, line) => Pt ? BoxPt(bank == TextBank(19), line) : BoxHg(bank == TextBank(25), line), FirstLine = 7,
                    LayoutStates = BoxStates, HasLayoutState = state => state != BoxSelected || Hg, OtherScreen = state => Hg ? BoxTopHg(state) : null,
                    // With a Pokémon picked, the menu covers the right of the bottom screen and hides the messages and yes/no.
                    Shows = (state, group, name) => state != BoxSelected || name is not ("Message" or "Short message" or "Second message" or "Yes/no") },
            new() { Key = "party", Title = "Party", Banks = () => Pt ? Us(453) : Hg ? Us(300) : Array.Empty<int>(),
                    Draw = (bank, line) => Party(line, Hg), FirstLine = 29,
                    States = PartyStates, LayoutStates = PartyStates.Append("Menu with field moves").Append("Multi battle").ToArray(),
                    // Retail Platinum only uses the single layout, so the multi tables never show.
                    HasLayoutState = state => state != PartyMulti || Hg,
                    DrawnAs = state => state == PartyFieldMoves ? PartyMenu : state == PartyMulti ? PartyNormal : state,
                    OtherScreen = state => Hg ? PartyTop(-1, state) : PartyBalls(),
                    RowsScrolled = (state, layer) => Hg && layer >= 4 && PartyPanelUp(state) ? 5 : 0,
                    PutOrder = PartyPutOrder, Block = PartyBlock, FixedTop = PartyFixedTop,
                    TakesTurns = w => w.Name.EndsWith(" message", StringComparison.Ordinal) ? "message" : null, Frame = PartyFrame, StateOf = (bank, line) => PartyStateOf(line, Hg), Shows = (state, group, name) => PartyShows(state, group, name, Hg) },
            new() { Key = "bag", Title = "Bag", Banks = () => Pt ? Us(392, 7, 395) : Hg ? Us(222, 10) : Array.Empty<int>(),
                    Draw = (bank, line) => Pt ? BagPt(bank, line) : BagHg(bank, line), FirstLine = 1 },
            new() { Key = "pokegear", Title = "Pokégear",
                    Banks = () => Hg ? Us(new[] { 640 }.Concat(Enumerable.Range(641, 76).Where(b => b != 668))
                                           .Concat(Enumerable.Range(409, 12)).Concat(new[] { 273, 271 }).ToArray()) : Array.Empty<int>(),
                    Draw = Pokegear, FirstLine = 1 },
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
        public static (int X, int Y, int W, int H) Placed(Screen screen, ScreenWindowLayout.Window w, int state) =>
            (w.X, screen.FixedTop?.Invoke(state, w) ?? w.Y, w.Width, w.Height);

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

        private static ushort[] SystemColours => FontColours(GameFamily == GameFamilies.HGSS ? 7 : 6);
        internal static ushort[] TalkColours => FontColours(GameFamily == GameFamilies.HGSS ? 8 : 7);

        private static ushort Rgb(int r, int g, int b) => (ushort)(r | g << 5 | b << 10);

        /// <summary>Fills placeholders with sample words and removes markup, keeping line breaks.</summary>
        private static string Text(int usBank, int id) => Line(TextBank(usBank), id);

        private static string Line(int bank, int id)
        {
            string raw = _line(bank, id) ?? "";
            string expanded = FieldStringVars.Expand(raw, SampleWord);
            expanded = expanded.Replace("\\n", "\n").Replace("\\r", "\n").Replace("\\f", "\n");
            return global::System.Text.RegularExpressions.Regex.Replace(expanded, @"\{[^}]*\}", "");
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

        public static string SampleWord(int family, int kind, int buffer)
        {
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

        private static void StandardWindow(byte[] rgba, int x, int y, int w, int h, ushort paper)
        {
            (byte r, byte g, byte b) = NitroBgCodec.Colour(paper);
            FieldWindowFrame frame = WindowFrames.LoadStandard();
            if (frame == null) { Fill(rgba, paper, x * 8, y * 8, w * 8, h * 8); return; }
            byte[] piece = frame.ComposeStandard(w, h, out int pw, out int ph, 0xFF000000u | (uint)r << 16 | (uint)g << 8 | b);
            Blit(rgba, piece, pw, ph, (x - 1) * 8, (y - 1) * 8);
        }

        /// <summary>Two tiles left, three right, one above and below.</summary>
        internal static void MessageWindow(byte[] rgba, int x, int y, int w, int h, ushort paper)
        {
            Fill(rgba, paper, x * 8 - 1, y * 8 - 1, w * 8 + 2, h * 8 + 2);
            FieldWindowFrame frame = WindowFrames.Load(0);
            if (frame == null) return;
            byte[] piece = frame.Compose(w, h, out int pw, out int ph);
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

            Palettes(s, NitroBgCodec.ReadColours(m(0)), 0, 1);
            s.InitLayer(2, 3, 0);
            s.LoadTiles(0, m(1));
            s.Fill(2, 1, 0, 0, 32, 32, 0);
            // The cursor bar is the arrangement's top two rows, scrolled to the cursor's row.
            s.InitLayer(0, 1, 0);
            s.Copy(0, 0, 3 + row * 2, 32, 2, NitroBgCodec.ReadMap(m(2)).Entries, 0, 0, 32);
            byte[] rgba = s.Render();

            ushort[] sys = SystemColours, talk = TalkColours;
            Print(rgba, SystemFont, Text(bank, 0), 8 + 2, 2, sys[1], sys[2]);
            StandardWindow(rgba, 1, 3, 30, 14, sys[15]);
            for (int r = 0; r < PtOptionRows.Length; r++)
            {
                (int label, int[] choices, int _) = PtOptionRows[r];
                int y = 24 + 16 * r;
                Print(rgba, SystemFont, Text(bank, label), 8 + 4, y, sys[1], sys[2]);
                int[] shown = r == 5 ? new[] { line >= 22 && line <= 41 ? line : 22 } : choices;
                int x = 100;
                for (int i = 0; i < shown.Length; i++)
                {
                    bool picked = r == 5 || (shown[i] == line ? true : !choices.Contains(line) && i == 0);
                    int at = r == 5 ? 148 : r == 4 ? x : 100 + 48 * i;
                    string word = Text(bank, shown[i]);
                    Print(rgba, SystemFont, word, 8 + at, y, picked ? sys[3] : sys[1], picked ? sys[4] : sys[2]);
                    x += DsBgScreen.MeasureText(SystemFont, word) + 12;
                }
            }
            MessageWindow(rgba, 2, 19, 27, 4, talk[15]);
            int about = line == 49 ? 49 : PtOptionRows[row].About;
            Print(rgba, TalkFont, BoxText(bank, about, TalkFont), 16 + 4, 152, talk[1], talk[2]);
            return new Shot("Options graphics: top", rgba);
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
            MessageWindow(upper, 2, 19, 27, 4, talk[15]);
            int frameLine = line >= 40 && line <= 59 ? line : 40;
            Print(upper, TalkFont, BoxText(bank, frameLine, TalkFont), 16 + 4, 152, talk[1], talk[2]);

            Palettes(s, NitroBgCodec.ReadColours(m(2)), 0, 2);
            s.SetPalette(13, SystemColours);
            Layer(s, m, 2, 2, 0, 7, screen == null ? 17 : -1);
            if (screen != null) s.LoadMap(2, NitroBgCodec.ReadMap(screen).Entries, DsBgScreen.MapSide);
            s.InitLayer(0, 0, 0);
            s.Copy(0, 0, 1, 32, 7, NitroBgCodec.ReadMap(m(18)).Entries, 0, 0, 32);
            byte[] lower = s.Render();
            ushort[] sys = SystemColours;
            Print(lower, SystemFont, Text(bank, 0), 8 + 2, 5, sys[15], sys[2]);
            for (int r = 0; r < HgOptionRows.Length; r++)
            {
                (int label, int[] choices, int[] xs) = HgOptionRows[r];
                if (labelXs != null && r < labelXs.Length) xs = labelXs[r];
                int y = 24 + 24 * r + 5;
                Print(lower, SystemFont, Text(bank, label), 8 + 4, y, sys[15], sys[2]);
                int[] shown = r == 5 ? new[] { line >= 20 && line <= 39 ? line : 20 } : choices;
                for (int i = 0; i < shown.Length; i++)
                {
                    bool picked = r == 5 || shown[i] == line || (!choices.Contains(line) && i == 0);
                    Print(lower, SystemFont, Text(bank, shown[i]), 8 + xs[i], y, picked ? sys[1] : sys[15], sys[2], Align.Centre);
                }
            }
            // OptionsApp_SetupWindows puts Confirm's 7-tile window at 15,21 and Quit's at 24,21.
            (int cx, int cy) = confirm ?? (15, 21);
            (int qx, int qy) = quit ?? (24, 21);
            Print(lower, SystemFont, Text(bank, 7), cx * 8 + 28, cy * 8 + 6, sys[15], sys[2], Align.Centre);
            Print(lower, SystemFont, Text(bank, 8), qx * 8 + 28, qy * 8 + 6, sys[15], sys[2], Align.Centre);

            bool onTop = line >= 40 && line <= 59;
            return new Shot($"Options graphics: {(onTop ? "top" : "bottom")}", onTop ? upper : lower);
        }

        // ── main menu ────────────────────────────────────────────────────────────────

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

            // CONTINUE is selected, so its window is white and the others grey.
            StandardWindow(rgba, 3, 1, width, 10, sys[15]);
            Print(rgba, SystemFont, Text(bank, 0), 24, 8, sys[1], sys[2]);
            for (int i = 0; i < 4; i++)
            {
                int y = 8 + 16 * (i + 1);
                Print(rgba, SystemFont, Text(bank, labels[i]), 24 + 32, y, sys[7], sys[8]);
                Print(rgba, SystemFont, Text(bank, values[i]), 24 + valueRight, y, sys[7], sys[8], Align.Right);
            }
            List<int> titles = new List<int> { 1 };
            bool inContinue = line == 0 || labels.Contains(line) || values.Contains(line);
            if (!inContinue && line != 1) titles.Add(line);
            if (hg && !titles.Contains(9)) titles.Add(9);
            int top = 13;
            foreach (int title in titles)
            {
                if (top + others > 24) break;
                StandardWindow(rgba, 3, top, width, others, grey);
                Print(rgba, SystemFont, Text(bank, title), 24, top * 8, sys[1], sys[2]);
                top += others + 2;
            }
            return new Shot($"Main menu graphics: {(hg ? "bottom" : "top")}", rgba);
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

        private static Dictionary<int, int> _bannerStyles;
        private static string _bannerRom;

        // Maps with no banner use style 0.
        private static int BannerStyle(int name, bool hg)
        {
            if (_bannerStyles == null || _bannerRom != WorkDir)
            {
                _bannerRom = WorkDir;
                _bannerStyles = new Dictionary<int, int>();
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
            return new Restore(() => { _usingBanner = before; _bannerStep = step; });
        }

        private sealed class Restore : IDisposable
        {
            private readonly Action _undo;
            public Restore(Action undo) => _undo = undo;
            public void Dispose() => _undo();
        }

        public static Shot BannerOf(int line) => Banner(line, Hg);

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
            Print(rgba, SystemFont, name, left + textX, top + (hg ? 8 : 16), hg ? colours[1] : colours[3], colours[2]);
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
            MessageWindow(rgba, 2, 19, 27, 4, talk[15]);
            Print(rgba, TalkFont, BoxText(hg ? 249 : 422, line, TalkFont), 16, 152, talk[1], talk[2]);
            return new Shot("Naming screen graphics: top", rgba, () => AvaloniaEditorLauncher.OpenNamingScreenEditor());
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

            byte[] rgba = Blank(0);
            DsBgScreen.DrawImage(rgba, back ? card.ComposeCardBack(0) : card.ComposeCardFront(0), 0, 0);
            if (!back)
            {
                RawImage trainer = card.ComposeTrainer(true);
                if (hg) DrawImagePart(rgba, trainer, 168, 40, 80, 88);
                else DsBgScreen.DrawImage(rgba, trainer, 0, 0);
            }

            ushort[] rank = DsBgScreen.Row(NitroBgCodec.ReadColours(Members(DirNames.TrainerCardGraphics)?.Invoke(TrainerCardMembers.rankPalettes[0])), 15);
            ushort ink = rank[1], shadow = rank[2];
            foreach (CardRow r in back ? (hg ? HgBack : PtBack) : (hg ? HgFront : PtFront))
            {
                int x = r.TileX * 8, y = r.TileY * 8;
                Print(rgba, SystemFont, Text(bank, r.Label), x, y, ink, shadow);
                string value = r.Value switch { -1 => "00000", -2 => "PLAYER", -3 => "0", _ => Text(bank, r.Value) };
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
                if (back) Print(rgba, touch, Text(bank, 16), hx * 8 + 48, hy * 8 + 4, rank[5], rank[4], Align.Centre);
                else Print(rgba, SystemFont, Text(bank, 15), hx * 8, hy * 8 + 4, rank[5], rank[4]);
                Print(rgba, touch, Text(bank, 14), cx * 8 + 24, cy * 8 + 4, rank[5], rank[4], Align.Centre);
                if (line == 17 || line == 18)
                {
                    ushort[] talk = TalkColours;
                    MessageWindow(rgba, 2, 1, 27, 4, talk[15]);
                    Print(rgba, TalkFont, BoxText(bank, line, TalkFont), 16, 8, talk[1], talk[2]);
                }
            }
            return new Shot($"Trainer card graphics: {(hg ? "bottom" : "top")}", rgba, AvaloniaEditorLauncher.OpenTrainerCardEditor);
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
            s.InitLayer(3, 3, 0x10000);
            s.LoadTiles(0x10000, m(29));
            s.Fill(3, 0x18, 0, 0, 32, 32, 9);
            (int _, ushort[] wall) = NitroBgCodec.ReadMap(m(30));
            for (int y = 0; y < 20; y++)
                for (int x = 0; x < 21; x++)
                {
                    int at = y * 21 + x;
                    if (at < wall.Length) s.Copy(3, 11 + x, y, 1, 1, new[] { (ushort)((wall[at] & 0x0FFF) | 9 << 12) }, 0, 0, 1);
                }
            byte[] rgba = s.Render();

            ushort[] wallRow = DsBgScreen.Row(NitroBgCodec.ReadColours(m(28)), 0);
            int name = !messages && line >= 6 && line <= 23 ? line : 6;
            Print(rgba, SystemFont, Text(18, name), 88 + 84, 13, wallRow[2], wallRow[1], Align.Centre);

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

        // pokeheartgold overlay 14, laid out like Platinum's box screens.
        private static string[] BoxStates => new[] { "Normal", "Pokémon selected" };
        private const int BoxSelected = 1;

        private static Shot BoxHg(bool messages, int line)
        {
            // A Pokémon picked in the box opens the menu on the right (HgssMenuButtons.Pc layout 3).
            if (_state == BoxSelected) return DrawBox(3) is byte[] menu ? new Shot("PC box graphics: bottom", menu) : null;
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
            if (messages)
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

        // Draws the PC's top screen (overlay 14) for the hovered Pokémon, with its moves once it's picked.
        private static Shot BoxTopHg(int state)
        {
            Func<int, byte[]> m = Members(DirNames.BoxGraphics);
            if (m == null) return null;
            DsBgScreen top = new DsBgScreen();
            Palettes(top, NitroBgCodec.ReadColours(m(7)));
            Layer(top, m, 1, 2, 0, 6, 5);
            byte[] rgba = top.Render();
            PartySample.Pokemon who = PartySample.Party[0];
            PokemonPersonalData data = new PokemonPersonalData(who.Species);
            ushort[] sys = SystemColours;
            string species = Line(PokemonNamesTextNumbers[0], who.Species);
            void Put(string window, (int X, int Y, int W, int H) fallback, string text, Align align = Align.Left)
            {
                (int x, int y, int w, int _) = ScreenWindowLayouts.Window("box", window, fallback);
                Print(rgba, SystemFont, text, align == Align.Right ? (x + w) * 8 : x * 8, y * 8, sys[1], sys[2], align);
            }
            Put("Title", (1, 0, 30, 3), Text(25, 52));
            Put("Dex number", (1, 5, 6, 2), $"No.{who.Species:000}");
            Put("Species", (8, 5, 8, 2), species);
            Put("Nickname", (1, 7, 8, 2), species);
            Put("Level", (4, 9, 6, 2), $"Lv.{who.Level}");
            Put("Nature", (1, 13, 8, 2), Text(34, 0));
            Put("Ability", (1, 17, 11, 2), Line(AbilityNamesTextNumber, data.FirstAbility));
            Put("Held item", (1, 21, 12, 2), who.Item > 0 ? Line(ItemNamesTextNumber, who.Item) : "None");
            int typeX = 144;
            foreach (int type in new[] { (int)data.Type1, (int)data.Type2 }.Distinct())
            {
                Paste(rgba, TypeIcons.PixelsFor(type), typeX, 40);
                typeX += 34;
            }
            if (new DexSample(who.Species).Picture(new ScreenMon { Kind = ScreenMonKind.Front }) is { Rgba: not null } picture)
                Blit(rgba, picture.Rgba, picture.Width, picture.Height, 144 - picture.Width / 2, 80 - picture.Height / 2);
            if (state == BoxSelected)
            {
                // The moves panel slides out from the right edge.
                Fill(rgba, Rgb(31, 31, 31), 166, 104, 90, 64);
                int[] moves = { 53, 52, 98, 108 };
                for (int i = 0; i < moves.Length; i++)
                {
                    Fill(rgba, Rgb(20, 20, 20), 166, 104 + 16 * i + 15, 90, 1);
                    Print(rgba, SystemFont, Line(MoveNamesTextNumbers, moves[i]), 168, 104 + 16 * i, sys[1], sys[2]);
                }
            }
            return new Shot("PC box graphics: top", rgba);
        }

        // ── Pokégear ─────────────────────────────────────────────────────────────────

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
                ushort[] row = DsBgScreen.Row(colours, 1);
                bool contact = contacts.Contains(bank);
                if (contact && line > 0) Print(rgba, SystemFont, Line(bank, 0), 32, 128, row[1], row[2]);
                Print(rgba, SystemFont, Box(bank, line, SystemFont), 16, 152, row[1], row[2]);
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
                    Print(under, SystemFont, Line(bank, 0), 40, 8, r0[1], r0[2]);
                    return new Shot("Pokégear radio graphics: bottom", under);
                }
                ushort[] colours = NitroBgCodec.ReadColours(m(4));
                Palettes(s, colours, 0, 12);
                Layer(s, m, 3, 2, 0, 34, 40);
                byte[] rgba = s.Render();
                ushort[] row = DsBgScreen.Row(colours, 0);
                Print(rgba, SystemFont, Line(bank, 1), 32, 128, row[1], row[2]);
                if (line >= 2)
                {
                    // The show scrolls up a line at a time, with two lines visible.
                    string[] said = Text(bank, line).Split('\n');
                    _byItself = true;
                    _stops = Math.Max(1, said.Length - 1);
                    int from = Math.Min(_stop % _stops, Math.Max(0, said.Length - 2));
                    Print(rgba, SystemFont, string.Join("\n", said.Skip(from).Take(2)), 16, 152, row[1], row[2]);
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
                ushort[] row = DsBgScreen.Row(colours, 1);
                if (line <= 1) Print(rgba, SystemFont, Line(bank, line), 8 + 2, 64 + 4, row[1], row[2]);
                else Print(rgba, SystemFont, Line(bank, line), 8, 112, row[1], row[2]);
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
            string template = _line(bank, 8 + n) ?? "";
            string shown = global::System.Text.RegularExpressions.Regex.Replace(
                FieldStringVars.Expand(template, (_, _, _) => name), @"\{[^}]*\}", "");
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

        /// <summary>The first frame of <paramref name="sequence"/> centred at x, y, with each OAM palette row from <paramref name="rows"/>.</summary>
        internal static void Sprite(byte[] rgba, Func<int, byte[]> m, int nanr, int ncer, int ncgr, int x, int y, int sequence, Func<int, ushort[]> rows)
        {
            try
            {
                NanrFile anim = NanrFile.Read(m(nanr));
                List<DsBgScreen.Oam[]> cells = DsBgScreen.ReadCells(m(ncer));
                byte[] tiles = NitroBgCodec.ReadCharacters(m(ncgr));
                if (anim == null || sequence >= anim.Sequences.Count || anim.Sequences[sequence].Frames.Count == 0) return;
                int resultAt = anim.Sequences[sequence].Frames[0].ResultAt;
                NanrFile.Result result = anim.Results.FirstOrDefault(r => r.Offset == resultAt);
                if (result == null || result.Cell >= cells.Count) return;
                DsBgScreen.DrawCell(rgba, cells[result.Cell], tiles, rows, x, y);
            }
            catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.Sprite", ex); }
        }

        /// <summary>The sample party's icons at <paramref name="spots"/>, one per Pokémon.</summary>
        internal static void PartyIcons(byte[] rgba, IReadOnlyList<(int X, int Y)> spots) => WithArchive(() =>
        {
            for (int i = 0; i < spots.Count && i < PartySample.Party.Length; i++)
                if (!PartySample.Party[i].Empty) PartyIcon(rgba, PartySample.Party[i].Species, spots[i].X, spots[i].Y);
            return 0;
        });

        private static void PartyIcon(byte[] rgba, int species, int x, int y)
        {
            try
            {
                if (Without(DirNames.MonIcons, species + 7)) return;
                GraphicAssets.Archive icons = GraphicAssets.All.FirstOrDefault(a => a.Dir == DirNames.MonIcons);
                GraphicAssets.Preview p = icons == null ? null : GraphicAssets.Render(icons, species + 7);
                if (p?.Rgba == null || p.Width < 32) return;
                _pieces.Add((DirNames.MonIcons, species + 7, "Drawing"));
                Credit(DirNames.MonIcons, species + 7);
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

        private static string[] PartyStates => new[] { "Normal", "Menu open", "Item menu", "Move list", "Confirm and cancel" };
        private const int PartyNormal = 0, PartyMenu = 1, PartyItemMenu = 2, PartyMoveList = 3, PartyConfirm = 4, PartyFieldMoves = 5, PartyMulti = 6;

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
                "Cancel button" => state is PartyNormal or PartyConfirm or PartyMulti,
                "Confirm button" => state == PartyConfirm,
                "Short message" or "Long message" => state is PartyNormal or PartyConfirm or PartyMulti,
                "Medium message" => state is PartyMenu or PartyItemMenu or PartyMoveList or PartyFieldMoves,
                "Item menu" => !hg && state == PartyItemMenu,
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
                // sub_0207A7F4.
                int row = who.Empty ? 1 : 3 + (n == 0 ? 4 : 0) + (!who.Egg && who.Hp == 0 ? 2 : 0);
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
            byte[] glyphs = NitroBgCodec.ReadCharacters(NitroBgCodec.Inflate(Members(DirNames.Fonts)?.Invoke(hg ? 5 : 4)));
            for (int n = 0; n < 6; n++)
            {
                PartySample.Pokemon who = PartySample.Party[n];
                if (who.Empty) continue;
                _drawing = n;
                Credit(DirNames.PartyMenuGraphics, 22);
                Credit(DirNames.Fonts, hg ? 5 : 4);
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
            PartySprite(rgba, m, 5, 6, 7, cx0, cy0, 1, menuUp ? 1 : -1);

            ushort[] talk = TalkColours, sys = SystemColours;
            int[] comments = hg ? Enumerable.Range(150, 15).ToArray() : Enumerable.Range(167, 15).ToArray();
            int[] levelUp = hg ? Enumerable.Range(169, 8).ToArray() : Enumerable.Range(185, 7).ToArray();

            // party_menu sprites 8 and 9.
            if (state is PartyNormal or PartyConfirm)
            {
                PartyButton(rgba, m, 232, state == PartyConfirm ? 184 : 176, state == PartyConfirm ? 2 : 0);
                (int cx, int cy, int cw, int _) = ScreenWindowLayouts.Window("party", "Cancel button", (26, 21, 5, 2));
                if (state == PartyConfirm) cy = 22;
                InWindow(rgba, "Cancel button", into => Print(into, SystemFont, Text(bank, 1), cx * 8 + cw * 4, cy * 8, row0[15], row0[14], Align.Centre));
            }
            if (state == PartyConfirm)
            {
                PartyButton(rgba, m, 232, 168, 2);
                (int fx, int fy, int fw, int _) = ScreenWindowLayouts.Window("party", "Confirm button", (25, 20, 7, 2));
                InWindow(rgba, "Confirm button", into => Print(into, SystemFont, Text(bank, 0), fx * 8 + fw * 4, fy * 8, row0[15], row0[14], Align.Centre));
            }

            if (state is PartyNormal or PartyConfirm)
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
                int shown = message ? line : t.Choose;
                int[] prompts = hg ? new[] { 29, 30, 32, 33, 34, 35, 184 } : new[] { 29, 30, 31, 32, 33, 34, 197 };
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
                choices.AddRange(new[] { "MOVE", "MOVE", "MOVE", "MOVE" });
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
                    // pokeplatinum windows.c PartyMenu_DrawContextMenu.
                    int top = 23 - 2 * choices.Count;
                    PartyList(rgba, 19, top, 12, 2 * choices.Count, choices, Math.Max(0, picked), sys);
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
                Print(rgba, SystemFont, Text(bank, 8), nx * 8, (ny - scroll) * 8, sys[15], sys[14]);
                Print(rgba, SystemFont, Text(bank, 130), hx * 8 + 2, (hy - scroll) * 8, sys[15], sys[14]);
                Print(rgba, SystemFont, Text(bank, 182), ix * 8 + 2, (iy - scroll) * 8 + 4, sys[15], sys[14]);
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

        private static void PartyButton(byte[] rgba, Func<int, byte[]> m, int x, int y, int sequence)
        {
            try
            {
                NanrFile anim = NanrFile.Read(m(9));
                List<DsBgScreen.Oam[]> cells = DsBgScreen.ReadCells(m(10));
                byte[] tiles = NitroBgCodec.ReadCharacters(m(11));
                ushort[] obj = NitroBgCodec.ReadColours(m(8));
                if (anim == null || sequence >= anim.Sequences.Count || anim.Sequences[sequence].Frames.Count == 0) return;
                int resultAt = anim.Sequences[sequence].Frames[0].ResultAt;
                NanrFile.Result result = anim.Results.FirstOrDefault(r => r.Offset == resultAt);
                if (result == null || result.Cell >= cells.Count) return;
                DsBgScreen.DrawCell(rgba, cells[result.Cell], tiles, row => DsBgScreen.Row(obj, row), x, y);
            }
            catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.PartyButton", ex); }
        }

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
            s.InitLayer(3, 3, 0);
            s.LoadMap(3, NitroBgCodec.ReadMap(m(13)).Entries, DsBgScreen.MapSide);
            byte[] rgba = s.Render();
            ushort[] row3 = DsBgScreen.Row(colours, 3);

            bool itemLine = bank == TextBank(392);
            int pocket = bank == TextBank(395) ? line : 0;
            Print(rgba, SystemFont, Text(395, Math.Clamp(pocket, 0, 7)), 50, 13 * 8 + 2, row3[1], row3[2], Align.Centre);

            int first = itemLine ? Math.Max(1, line) : 1;
            for (int k = 1; k < 9; k++)
            {
                int y = 16 * k;
                bool close = k == 8 || (!itemLine && bank == TextBank(7) && line == 41 && k == 2);
                if (close) { Print(rgba, SystemFont, Text(7, 41), 112, y, row3[1], row3[2]); break; }
                Print(rgba, SystemFont, Text(392, first + k - 1), 112, y, row3[1], row3[2]);
                Print(rgba, SystemFont, Text(7, 38), 112 + 109, y, row3[1], row3[2]);
                Print(rgba, SystemFont, "1", 112 + 134, y, row3[1], row3[2], Align.Right);
            }

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
                    List<int> menu = line == 5 ? new List<int> { 0, 5 } : new List<int> { line, 5 };
                    int n = menu.Count, top = 23 - 2 * n;
                    StandardWindow(rgba, 24, top, 7, 2 * n, sys[15]);
                    for (int i = 0; i < n; i++) Print(rgba, SystemFont, Text(7, menu[i]), 24 * 8 + 8, top * 8 + 16 * i, sys[1], sys[2]);
                }
                else
                {
                    MessageWindow(rgba, 2, 19, 27, 4, talk[15]);
                    Print(rgba, TalkFont, BoxText(7, line, TalkFont), 16, 152, talk[1], talk[2]);
                }
            }
            return new Shot("Bag graphics: top", rgba);
        }

        // pokeheartgold src/application/bag.
        // Item button screen and background screen per FieldBagButtons page (BagApp_DrawScreenLayout); -1 on the item menu and amount
        // pages, whose buttons are sprites.
        private static readonly (int Buttons, int Back)[] BagLayouts = { (43, 39), (-1, 45), (44, 42), (-1, 52), (-1, 53) };

        /// <summary>Draws the HGSS bag's bottom screen for a FieldBagButtons page with the first items listed.</summary>
        internal static byte[] BagBottomHg(int page) => BagHg(TextBank(222), 1, BagLayouts[page])?.Rgba;

        private static Shot BagHg(int bank, int line) => BagHg(bank, line, BagLayouts[0]);

        private static Shot BagHg(int bank, int line, (int Buttons, int Back) layout)
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
            ushort[] pocket = DsBgScreen.Row(NitroBgCodec.ReadColours(m(41)), 0);
            for (int r = 0; r < 4; r++) s.SetPalette(r, pocket);
            s.InitLayer(1, 2, 0);
            s.LoadTiles(0, m(46));
            if (layout.Buttons >= 0) s.LoadMap(1, NitroBgCodec.ReadMap(m(layout.Buttons)).Entries, DsBgScreen.MapSide);
            s.InitLayer(2, 3, 0);
            s.LoadMap(2, NitroBgCodec.ReadMap(m(layout.Back)).Entries, DsBgScreen.MapSide);
            byte[] rgba = s.Render();
            ushort[] row11 = DsBgScreen.Row(items, 3);
            if (layout.Buttons >= 0) BagSpritesHg(rgba, m);

            bool itemLine = bank == TextBank(222);
            int first = itemLine ? Math.Max(1, line) : 1;
            (int X, int Y)[] buttons = { (4, 5), (20, 5), (4, 10), (20, 10), (4, 15), (20, 15) };
            for (int i = 0; i < buttons.Length && layout.Buttons >= 0; i++)
            {
                int x = buttons[i].X * 8, y = buttons[i].Y * 8;
                Print(rgba, SystemFont, Text(222, first + i), x, y, row11[1], row11[2]);
                Print(rgba, SystemFont, Text(10, 87), x + 48, y + 16, row11[1], row11[2]);
            }
            if (layout.Buttons >= 0) Print(rgba, SystemFont, Text(10, 22), 10 * 8, 21 * 8, row11[15], row11[1]);
            Print(rgba, SystemFont, Text(10, 8), 24 * 8 + 32, 21 * 8, row11[15], row11[14], Align.Centre);

            if (bank == TextBank(10) && line != 87 && line != 22 && line != 8)
            {
                ushort[] talk = TalkColours;
                MessageWindow(rgba, 2, 1, 27, 2, talk[15]);
                Print(rgba, TalkFont, BoxText(10, line, TalkFont), 16, 8, talk[1], talk[2]);
            }
            return new Shot("Bag graphics: bottom", rgba);
        }

        // Template palette rows override the cells' (sSpriteTemplates); BagApp_LoadObjPaletteByPocket loads the pocket rows from file 48.
        private static void BagSpritesHg(byte[] rgba, Func<int, byte[]> m)
        {
            ushort[] obj = NitroBgCodec.ReadColours(m(47)), pockets = NitroBgCodec.ReadColours(m(48));
            if (obj == null || pockets == null) return;
            obj = (ushort[])obj.Clone();
            Array.Resize(ref obj, Math.Max(obj.Length, 256));
            Array.Copy(pockets, 128, obj, 0, Math.Min(128, pockets.Length - 128));
            Array.Copy(pockets, 0, obj, 0, 16);
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

        private static Shot StartMenuPt(int line)
        {
            const int bank = 367;
            byte[] rgba = Blank(FieldBackdrop);
            ushort[] talk = TalkColours, sys = SystemColours;

            if (line >= 9 && line <= 11)
            {
                StandardWindow(rgba, 1, 1, 12, 4, sys[15]);
                Print(rgba, SystemFont, Text(bank, line == 11 ? 9 : line), 8, 8, sys[1], sys[2]);
                Print(rgba, SystemFont, Text(bank, 11), 8, 24, sys[1], sys[2]);
                return new Shot("Start menu graphics: top", rgba);
            }

            List<int> rows = StartMenuRows.ToList();
            if (line == 7 || line == 8) rows.Insert(0, line);
            int cursor = Math.Max(0, rows.IndexOf(line));
            StandardWindow(rgba, 20, 1, 11, rows.Count * 3, talk[15]);

            Func<int, byte[]> m = Members(DirNames.StartMenuGraphics);
            if (m != null)
            {
                ushort[] obj = NitroBgCodec.ReadColours(m(5));
                List<DsBgScreen.Oam[]> cursorCells = DsBgScreen.ReadCells(m(1));
                byte[] cursorTiles = NitroBgCodec.ReadCharacters(m(2));
                if (cursorCells.Count > 0)
                    DsBgScreen.DrawCell(rgba, cursorCells[0], cursorTiles, _ => DsBgScreen.Row(obj, 1), 204, 20 + 24 * cursor);
                List<DsBgScreen.Oam[]> icons = DsBgScreen.ReadCells(m(4));
                byte[] iconTiles = NitroBgCodec.ReadCharacters(m(6));
                for (int i = 0; i < rows.Count; i++)
                {
                    int cell = rows[i] == 7 ? 7 : rows[i] == 8 ? 8 : rows[i];
                    if (cell < icons.Count)
                        DsBgScreen.DrawCell(rgba, icons[cell], iconTiles, _ => DsBgScreen.Row(obj, i == cursor ? 1 : 0), 174, 20 + 24 * i);
                }
            }
            for (int i = 0; i < rows.Count; i++)
                Print(rgba, TalkFont, Text(bank, rows[i]), 160 + 28, 8 + 4 + 24 * i, talk[1], talk[2]);
            return new Shot("Start menu graphics: top", rgba);
        }
    }
}
