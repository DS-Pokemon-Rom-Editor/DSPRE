using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The Pokémon summary screen's pages in Platinum and HGSS, drawn with a sample Pokémon. The text boxes and the HP
    /// and EXP bars are placed from the window tables and drawing code in arm9, so moving one shows where the game
    /// will put it. What each box prints, and where in the box, follows the game's own print calls (pokeplatinum
    /// pokemon_summary_screen/window.c, and the matching pokeheartgold code); the sample's words and numbers come
    /// from the ROM's text banks, personal data and move data.
    /// </summary>
    public sealed class SummaryBook : ScreenBook
    {
        private enum Ink { White, Black, Blue, Red }

        // One print call into a box: a line of a message bank (the summary's own unless Bank is set) with its
        // placeholders filled from Args, at X, Y from the box's top left, or centred or right-aligned in the box.
        private sealed record Print(int Line, string[] Args = null, Ink Colour = Ink.White, ScreenTextAlign At = ScreenTextAlign.Left,
                                    int X = 0, int Y = 0, int Bank = -1, int Font = 0, ScreenTextKind Kind = ScreenTextKind.Message);

        // One text box on a page: the table and entry it is in arm9, and the prints the game makes into it.
        private sealed record Box(string Table, int Index, string Name, Print[] Prints, string When = null);

        private sealed record Spec(string Id, string Name, int Top, int Bottom, Box[] Boxes, bool Hp = false, bool Exp = false,
                                   string[] Variants = null);

        // The sample's moves: Tackle, Rollout, Bide and Rest. Their types, power, PP and the rest are read from the ROM.
        private static readonly int[] SampleMoves = { 33, 205, 117, 156 };
        private const int SampleLevel = 50;

        private readonly DexSample _sample;
        private readonly bool _hgss;
        private readonly SummaryScreenSites _sites;
        private Spec[] _specs;
        private readonly List<ScreenPage> _pages = new();
        private readonly SummaryLayout _layout;
        private SummaryScreenLayout _now, _original;
        private readonly Dictionary<int, List<string>> _banks = new();

        /// <param name="species">The Pokémon the pages are shown with; 0 keeps the usual sample.</param>
        public SummaryBook(int species = 0)
        {
            _sample = new DexSample(species);
            _hgss = GameFamily == GameFamilies.HGSS;
            _sites = RomLocator.SummaryScreenSites;
            _specs = _sites == null ? Array.Empty<Spec>() : _hgss ? HgssSpecs() : PlatinumSpecs();
            foreach (Spec spec in _specs) _pages.Add(new ScreenPage { Id = spec.Id, Name = spec.Name, Variants = spec.Variants ?? Array.Empty<string>() });
            if (_sites == null) return;
            _layout = new SummaryLayout(this);
            try
            {
                _original = SummaryScreenLayout.FromBytes(SummaryScreenLayout.ReadBytes());
                Build(_original);
            }
            catch (Exception ex) { AppLogger.Error("Summary screen layout: " + ex.Message); }
        }

        public override string Title => "Summary screen";
        public override DirNames Archive => DirNames.SummaryGraphics;
        public override IReadOnlyList<ScreenPage> Pages => _pages;
        public override ScreenBookLayout Layout => _original == null ? null : _layout;
        public override DirNames[] PictureArchives => new[] { DirNames.PokemonBattleSprites, DirNames.MonIcons, DirNames.BattleObj, DirNames.SummarySprites, DirNames.Fonts };

        public override IEnumerable<(string Name, string Where, DirNames Dir, int Member)> MonParts(ScreenMon m)
        {
            int first = _sample.Species * 6;
            yield return ("Front", "Battle sprites " + (first + 3), DirNames.PokemonBattleSprites, first + 3);
            yield return ("Colours", "Battle sprites " + (first + 4), DirNames.PokemonBattleSprites, first + 4);
        }

        public override void Forget()
        {
            _sample.Forget();
            _banks.Clear();
            _pictures.Clear();
            _moves.Clear();
        }

        public override ScreenPageComposer MakeComposer(Func<int, FieldFont> font)
        {
            ScriptNarc archive = null;
            try
            {
                List<DirNames> dirs = new() { DirNames.SummaryGraphics, DirNames.PokemonBattleSprites, DirNames.MonIcons, DirNames.BattleObj,
                                              DirNames.PersonalPokeData, DirNames.MoveData, DirNames.Fonts };
                if (_hgss) dirs.Add(DirNames.SummarySprites);
                DSUtils.TryUnpackNarcs(dirs);
                archive = new ScriptNarc(DirNames.SummaryGraphics);
            }
            catch (Exception ex) { AppLogger.Error("Summary screen graphics: " + ex.Message); }
            ScriptNarc files = archive;
            Dictionary<DirNames, ScriptNarc> others = new();
            byte[] Other(DirNames dir, int index)
            {
                if (!others.TryGetValue(dir, out ScriptNarc narc))
                {
                    try { narc = new ScriptNarc(dir); } catch (Exception ex) { AppLogger.WarnOnce("SummaryBook.MakeComposer", ex); narc = null; }
                    others[dir] = narc;
                }
                return narc?.Get(index);
            }
            return new ScreenPageComposer(i => files?.Get(i), font, Text, _sample.Picture, _ => -1, _ => 0) { Picture = Picture, Other = Other };
        }

        // ── the sample ────────────────────────────────────────────────────────────────────────────

        private readonly Dictionary<int, MoveData> _moves = new();

        private MoveData Move(int id)
        {
            if (_moves.TryGetValue(id, out MoveData known)) return known;
            MoveData move = null;
            try { move = new MoveData(id); } catch (Exception ex) { AppLogger.Warn("Summary screen sample move: " + ex.Message); }
            _moves[id] = move;
            return move;
        }

        private PokemonPersonalData Personal()
        {
            try { return new PokemonPersonalData(_sample.Species); } catch (Exception ex) { AppLogger.WarnOnce("SummaryBook.Personal", ex); return null; }
        }

        // The sample's two types by number; the second is -1 when it has one type.
        private (int First, int Second) Types()
        {
            PokemonPersonalData data = Personal();
            if (data == null) return (-1, -1);
            return ((int)data.Type1, data.Type2 == data.Type1 ? -1 : (int)data.Type2);
        }

        // The sample's stats at its level with no individual or effort values and a neutral nature.
        private (int Hp, int Attack, int Defense, int SpAtk, int SpDef, int Speed) Stats()
        {
            PokemonPersonalData d = Personal();
            if (d == null) return (0, 0, 0, 0, 0, 0);
            int Stat(int b) => 2 * b * SampleLevel / 100 + 5;
            return (2 * d.BaseHP * SampleLevel / 100 + SampleLevel + 10, Stat(d.BaseAtk), Stat(d.BaseDef), Stat(d.BaseSpAtk), Stat(d.BaseSpDef), Stat(d.BaseSpeed));
        }

        private int Ability() => Personal()?.FirstAbility ?? 0;

        // A number as the game formats it: no padding, or padded to its digits with spaces or zeros.
        private static string Num(int value, int digits, char pad = '\0') =>
            pad == '\0' ? value.ToString() : value.ToString().PadLeft(digits, pad);

        // ── words ─────────────────────────────────────────────────────────────────────────────────

        private static readonly Regex Placeholder = new(@"\{STRVAR[^}]*\}");
        private static readonly Regex Code = new(@"\{[^}]*\}");

        private string Text(ScreenText t, string variant)
        {
            switch (t.Kind)
            {
                case ScreenTextKind.Message:
                    string line = BankLine(t.Bank >= 0 ? t.Bank : _sites.MessageBank, t.Line);
                    if (line == null) return null;
                    int next = 0;
                    line = Placeholder.Replace(line, _ => t.Args != null && next < t.Args.Length ? t.Args[next++] : "");
                    return Code.Replace(line, "");
                case ScreenTextKind.Sample:
                    // A count: the two numbers either side of the summary's own slash.
                    if (t.Align == ScreenTextAlign.Slash && t.Args?.Length == 2) return t.Args[0] + (BankLine(_sites.MessageBank, 117) ?? "/") + t.Args[1];
                    return t.Args?.FirstOrDefault();
                default: return _sample.Text(t, variant);
            }
        }

        private string BankLine(int bank, int line)
        {
            if (bank < 0 || line < 0) return null;
            if (!_banks.TryGetValue(bank, out List<string> lines))
            {
                try { lines = new TextArchive(bank).Messages; } catch (Exception ex) { AppLogger.WarnOnce("SummaryBook.BankLine", ex); lines = null; }
                _banks[bank] = lines;
            }
            if (lines == null || line >= lines.Count) return null;
            return lines[line].Replace("\\n", "\n").Replace("\\r", "\n").Replace("\\f", "\n");
        }

        // ── pictures ──────────────────────────────────────────────────────────────────────────────

        // Type and move category icons, which the summary screen borrows from the battle graphics, and the "Lv" glyph
        // its level is printed after.
        private readonly Dictionary<string, ScreenPicture> _pictures = new();

        private ScreenPicture Picture(ScreenSprite sprite)
        {
            if (_pictures.TryGetValue(sprite.Picture, out ScreenPicture known)) return known;
            ScreenPicture picture = null;
            try
            {
                string[] parts = sprite.Picture.Split(':');
                int n = int.Parse(parts[1]);
                if (parts[0] == "level") picture = LevelGlyph(n);
                else
                {
                    string[] names = parts[0] == "type" ? BattleObjects.IconOrder : BattleObjects.KindOrder;
                    int index = n >= 0 && n < names.Length ? BattleObjects.Find(names[n], "Drawing") : -1;
                    GraphicAssets.Archive battle = GraphicAssets.All.FirstOrDefault(a => a.Dir == DirNames.BattleObj);
                    GraphicAssets.Preview p = index >= 0 && battle != null ? GraphicAssets.Render(battle, index) : null;
                    if (p?.Rgba != null) picture = new ScreenPicture { Rgba = p.Rgba, Width = p.Width, Height = p.Height };
                }
            }
            catch (Exception ex) { AppLogger.Warn("Summary screen icon: " + ex.Message); }
            _pictures[sprite.Picture] = picture;
            return picture;
        }

        // The "Lv" glyph: two tiles from tile 11 of the font archive's special characters (Platinum file 4, HGSS file 5),
        // whose pixel values 1 and 2 the game turns into ink 1 and shadow 2 of the box's colour row.
        private ScreenPicture LevelGlyph(int row)
        {
            byte[] chr = NitroBgCodec.Inflate(new ScriptNarc(DirNames.Fonts).Get(_hgss ? 5 : 4));
            byte[] tiles = NitroBgCodec.ReadCharacters(chr);
            ushort[] colours = NitroBgCodec.ReadColours(new ScriptNarc(DirNames.SummaryGraphics).Get(_hgss ? 0 : 1));
            if (tiles == null || colours == null) return null;
            byte[] rgba = new byte[16 * 8 * 4];
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 16; x++)
                {
                    int at = (11 + x / 8) * 32 + (y * 8 + x % 8) / 2;
                    if (at >= tiles.Length) continue;
                    int value = (x & 1) != 0 ? tiles[at] >> 4 : tiles[at] & 0xF;
                    if (value is not (1 or 2)) continue;
                    int colour = row * 16 + value;
                    if (colour >= colours.Length) continue;
                    (byte r, byte g, byte b) = NitroBgCodec.Colour(colours[colour]);
                    int to = (y * 16 + x) * 4;
                    (rgba[to], rgba[to + 1], rgba[to + 2], rgba[to + 3]) = (r, g, b, 255);
                }
            return new ScreenPicture { Rgba = rgba, Width = 16, Height = 8 };
        }

        // ── pages ─────────────────────────────────────────────────────────────────────────────────

        // Draws every page again from a layout. The sample is read again too, since what it reads may only now be unpacked.
        private void Build(SummaryScreenLayout layout)
        {
            _now = layout;
            _specs = _hgss ? HgssSpecs() : PlatinumSpecs();
            for (int i = 0; i < _specs.Length; i++)
            {
                Spec spec = _specs[i];
                ScreenPage page = _pages[i];
                page.Main = new ScreenLayout();
                page.Sub = new ScreenLayout();
                string unpicked = Unpicked(spec);
                if (_hgss) HgssBackgrounds(page, spec);
                else PlatinumBackgrounds(page, spec, unpicked);
                foreach (Box box in BoxesOf(spec)) AddBox(page, box);
                if (spec.Hp) AddBar(page, layout.Hp);
                if (spec.Exp) AddBar(page, layout.Exp);
                AddLevelGlyph(page, unpicked);
                if (_hgss) HgssSprites(page, i);
                else PlatinumSprites(page, i, unpicked);
                // The Pokémon is drawn on the top screen's 3D layer, behind the text.
                page.Main.Mons.Add(new ScreenMon { Kind = ScreenMonKind.Front, X = _hgss ? 208 : 52, Y = 104, Priority = 1, When = unpicked });
            }
        }

        // In Platinum a picked move slides the move panel over the Pokémon, its level and its item.
        private string Unpicked(Spec spec) => !_hgss && spec.Variants?.Contains(Picked) == true ? spec.Variants[0] : null;

        private IEnumerable<Box> BoxesOf(Spec spec)
        {
            string unpicked = Unpicked(spec);
            foreach (Box box in Always()) yield return unpicked != null && box.Name != "Nickname" ? box with { When = unpicked } : box;
            foreach (Box box in spec.Boxes) yield return box;
        }

        private SummaryScreenLayout.Window WindowOf(string table, int index) =>
            _now?.Tables.FirstOrDefault(t => t.Page == table) is { } found && index < found.Windows.Count ? found.Windows[index] : null;

        private static (ScreenSide Side, int Bg) Where(int layer) => layer < 4 ? (ScreenSide.Main, layer) : (ScreenSide.Sub, layer - 4);

        private void AddBox(ScreenPage page, Box box)
        {
            SummaryScreenLayout.Window w = WindowOf(box.Table, box.Index);
            if (w == null || w.Width == 0 || w.Height == 0) return;
            (ScreenSide side, int bg) = Where(w.Layer);
            foreach (Print p in box.Prints)
            {
                (int ink, int shadow) = p.Colour switch
                {
                    Ink.Black => (1, 2),
                    Ink.Blue => (3, 4),
                    Ink.Red => (5, 6),
                    _ => _hgss ? (14, 15) : (15, 14),
                };
                page.Side(side).Texts.Add(new ScreenText
                {
                    Kind = p.Kind, Line = p.Line, Bank = p.Bank, Args = p.Args, Font = p.Font,
                    Bg = bg, X = w.X * 8 + p.X, Y = w.Y * 8 + p.Y, Align = p.At, Row = w.Palette, Ink = ink, Shadow = shadow,
                    WinX = w.X, WinY = w.Y, WinW = w.Width, WinH = w.Height, When = box.When,
                });
            }
        }

        private static void AddBar(ScreenPage page, SummaryScreenLayout.Bar bar)
        {
            (ScreenSide side, int bg) = Where(bar.Layer);
            ScreenBg layer = page.Side(side).Bgs.FirstOrDefault(b => b.Bg == bg);
            layer?.Pieces.Add(new ScreenPaste { Fill = bar.FullTile, TileX = bar.X, TileY = bar.Y, Width = bar.Tiles, Height = 1 });
        }

        // The "Lv" glyph the game blits into the level box at 0, 5 before printing the number 16 pixels in.
        private void AddLevelGlyph(ScreenPage page, string unpicked)
        {
            SummaryScreenLayout.Window w = WindowOf("Static", _hgss ? 32 : 33);
            if (w == null || w.Width == 0) return;
            (ScreenSide side, _) = Where(w.Layer);
            page.Side(side).Sprites.Add(new ScreenSprite { Name = "Level mark", Picture = "level:" + w.Palette, X = w.X * 8 + 8, Y = w.Y * 8 + 5 + 4, When = unpicked });
        }

        private static void Text(ScreenLayout screen, int bg) => screen.Bgs.Add(new ScreenBg { Bg = bg, Priority = 0 });

        private static void PlatinumBackgrounds(ScreenPage page, Spec spec, string unpicked)
        {
            page.Main.Palettes.Add(new ScreenPaletteLoad { File = 1 });
            Text(page.Main, 1);
            page.Main.Bgs.Add(new ScreenBg { Bg = 2, Priority = 1, Drawing = 0, MapWidth = 64, ScrollX = 136, When = unpicked, Pieces = { new ScreenPaste { Screen = 11 } } });
            if (unpicked != null)
                page.Main.Bgs.Add(new ScreenBg { Bg = 2, Priority = 1, Drawing = 0, MapWidth = 64, When = Picked, Pieces = { new ScreenPaste { Screen = 11 } } });
            page.Main.Bgs.Add(new ScreenBg { Bg = 3, Priority = 3, Drawing = 0, Pieces = { new ScreenPaste { Screen = spec.Top } } });
            page.Sub.Palettes.Add(new ScreenPaletteLoad { File = 14 });
            page.Sub.Bgs.Add(new ScreenBg { Bg = 1, Priority = 1, Drawing = 16, Pieces = { new ScreenPaste { Screen = spec.Bottom } } });
        }

        private static void HgssBackgrounds(ScreenPage page, Spec spec)
        {
            // A picked move slides the bottom screen's panel half a map across to show the move's details.
            bool picks = spec.Variants?.Contains(Picked) == true;
            page.Main.Palettes.Add(new ScreenPaletteLoad { File = 0 });
            Text(page.Main, 1);
            if (spec.Variants?.Contains(Athlon) == true)
            {
                // Without Pokéathlon stats the game puts up screen 11 in place of the stats panel.
                page.Main.Bgs.Add(new ScreenBg { Bg = 3, Priority = 3, Drawing = 2, When = spec.Variants[0], Pieces = { new ScreenPaste { Screen = 11 } } });
                page.Main.Bgs.Add(new ScreenBg { Bg = 3, Priority = 3, Drawing = 2, When = Athlon, Pieces = { new ScreenPaste { Screen = spec.Top } } });
            }
            else page.Main.Bgs.Add(new ScreenBg { Bg = 3, Priority = 3, Drawing = 2, Pieces = { new ScreenPaste { Screen = spec.Top } } });
            page.Sub.Palettes.Add(new ScreenPaletteLoad { File = 0 });
            Text(page.Sub, 0);
            page.Sub.Bgs.Add(new ScreenBg { Bg = 1, Priority = 2, Drawing = 20, MapWidth = 64, When = picks ? spec.Variants[0] : null, Pieces = { new ScreenPaste { Screen = 21 } } });
            if (picks)
                page.Sub.Bgs.Add(new ScreenBg { Bg = 1, Priority = 2, Drawing = 20, MapWidth = 64, ScrollX = 128, When = Picked, Pieces = { new ScreenPaste { Screen = 21 } } });
            page.Sub.Bgs.Add(new ScreenBg { Bg = 2, Priority = 3, Drawing = 1, Pieces = { new ScreenPaste { Screen = spec.Bottom } } });
        }

        // The type icon a sample move shows: its battle type, or 18 on for its contest condition.
        private int MoveIcon(int move, bool contest)
        {
            MoveData data = Move(move);
            if (data == null) return -1;
            return contest ? 18 + (int)data.ContestConditionType : (int)data.Movetype;
        }

        // Platinum's sprites, after sprites.c and subscreen.c: the page tabs and arrows, the ball, the markings, the
        // type icons, and on the touch screen the page buttons the game builds tile by tile into sub BG0.
        private void PlatinumSprites(ScreenPage page, int pageIndex, string unpicked)
        {
            List<ScreenSprite> sprites = page.Main.Sprites;
            // Each sprite takes the colour row its template gives it (the cells' own rows are overridden); the shared
            // sprite colours sit at the start of sprite colour memory.
            string[] tabs = { "Info", "Trainer memo", "Skills", "Battle moves", "Condition", "Contest moves", "Ribbons", "Exit" };
            int[] tabRows = { 1, 1, 1, 2, 1, 2, 2, 2 };
            // Eight tabs centred on 188, the open one 24 wide and the rest 16.
            int left = 188 - (24 + 7 * 16) / 2;
            for (int t = 0; t < tabs.Length; t++)
            {
                int x = t <= pageIndex ? left + t * 16 : left + 24 + (t - 1) * 16;
                sprites.Add(new ScreenSprite { Name = "Tab: " + tabs[t], Cells = 128, Anim = 127, Drawing = 129, Colours = 69, Palette = tabRows[t],
                                               Seq = t == pageIndex ? t + 8 : t, X = x, Y = 24, Priority = 1 });
            }
            sprites.Add(new ScreenSprite { Name = "Page arrow left", Cells = 60, Anim = 59, Drawing = 61, Colours = 69, Palette = 1, X = left - 12, Y = 24, When = unpicked });
            sprites.Add(new ScreenSprite { Name = "Page arrow right", Cells = 60, Anim = 59, Drawing = 61, Colours = 69, Palette = 1, Seq = 1, X = 188 + (188 - left) - 4, Y = 24, When = unpicked });
            // The ball's row holds its own one-row colours file, which the game swaps per ball.
            sprites.Add(new ScreenSprite { Name = "Ball", Cells = 48, Anim = 47, Drawing = 24, Colours = 37, Palette = 0, X = 16, Y = 32 });
            (string Name, int Drawing, int X)[] marks = { ("circle", 52, 48), ("triangle", 56, 56), ("square", 55, 64), ("heart", 57, 72), ("star", 53, 80), ("diamond", 54, 88) };
            foreach ((string name, int drawing, int x) in marks)
                sprites.Add(new ScreenSprite { Name = "Marking: " + name, Cells = 51, Anim = 50, Drawing = drawing, Colours = 69, Palette = 0, X = x, Y = 150, Priority = 3, When = unpicked });
            // The A button beside the button hint: 10 pixels left of the hint's box, 8 down.
            if (_specs[pageIndex].Boxes.Any(b => b.Table == "Static" && b.Index == 35) && WindowOf("Static", 35) is { } hint)
                sprites.Add(new ScreenSprite { Name = "A button", Cells = 18, Anim = 17, Drawing = 20, Colours = 69, Palette = 0, X = hint.X * 8 - 10, Y = 8 });

            (int first, int second) = Types();
            string id = _specs[pageIndex].Id;
            if (id == "info")
            {
                if (second < 0) sprites.Add(TypeIcon("Type", first, 216, 80, null));
                else
                {
                    sprites.Add(TypeIcon("First type", first, 199, 80, null));
                    sprites.Add(TypeIcon("Second type", second, 233, 80, null));
                }
            }
            if (id is "battle_moves" or "contest_moves")
            {
                bool contest = id == "contest_moves";
                for (int m = 0; m < SampleMoves.Length; m++)
                    sprites.Add(TypeIcon("Move " + (m + 1) + " type", MoveIcon(SampleMoves[m], contest), 151, 42 + m * 32, null));
                sprites.Add(TypeIcon("First type", first, 63, 52, Picked));
                if (second >= 0) sprites.Add(TypeIcon("Second type", second, 97, 52, Picked));
                if (!contest && Move(SampleMoves[0]) is MoveData picked)
                    sprites.Add(new ScreenSprite { Name = "Category", Picture = "kind:" + (int)picked.Split, X = 108, Y = 72, When = Picked });
                sprites.Add(new ScreenSprite { Name = "Move cursor", Cells = 131, Anim = 130, Drawing = 132, Colours = 69, Palette = 2, X = 194, Y = 48, Priority = 1, When = Picked });
                page.Main.Mons.Add(new ScreenMon { Kind = ScreenMonKind.Icon, X = contest ? 32 : 24, Y = contest ? 68 : 48, Priority = 0, When = Picked });
            }

            // The touch screen's page buttons: 5 by 5 tiles each, counting up through sub_buttons 30 tiles a row.
            (int Page, int Row, int X, int Y)[] buttons = { (0, 1, 1, 4), (1, 1, 2, 10), (2, 2, 5, 15), (3, 3, 10, 18), (4, 2, 17, 18), (5, 3, 22, 15), (6, 4, 25, 10), (7, 4, 26, 4) };
            ScreenBg sub0 = new() { Bg = 0, Priority = 0, Drawing = 2 };
            foreach ((int p, int row, int x, int y) in buttons)
            {
                int start = (p & 1) * 15 + p / 2 * 150 + 30;
                sub0.Pieces.Add(new ScreenPaste { Fill = start | row << 12, FillStride = 30, TileX = x, TileY = y, Width = 5, Height = 5 });
            }
            page.Sub.Bgs.Insert(0, sub0);
        }

        // HGSS's sprites, from its sprite templates and the code that places them (pokeheartgold unk_0208B1AC): the ball
        // and markings on the top screen, the type icons on the bottom one. The cells come from a/0/3/9.
        private void HgssSprites(ScreenPage page, int pageIndex)
        {
            const DirNames cells = DirNames.SummarySprites;
            List<ScreenSprite> top = page.Main.Sprites, bottom = page.Sub.Sprites;
            // A Poké Ball: drawing 24 + ball, colours 49 + the ball's colour set; the ball cells say row 2.
            top.Add(new ScreenSprite { Name = "Ball", Archive = cells, Cells = 48, Anim = 47, Drawing = 28, Colours = 49, Palette = 0, X = 168, Y = 39 });
            (string Name, int Drawing, int X)[] marks = { ("circle", 52, 200), ("triangle", 56, 208), ("square", 55, 216), ("heart", 57, 224), ("star", 53, 232), ("diamond", 54, 240) };
            foreach ((string name, int drawing, int x) in marks)
                top.Add(new ScreenSprite { Name = "Marking: " + name, Archive = cells, DrawingArchive = cells, Cells = 51, Anim = 50, Drawing = drawing, Colours = 61, Palette = 0, X = x, Y = 150 });

            // The party on the touch screen: an icon per Pokémon (the sample is the only one, in the first slot) and
            // the frame on the one shown, placed from the table at _021105B0 with animation 2.
            page.Sub.Mons.Add(new ScreenMon { Kind = ScreenMonKind.Icon, X = 184, Y = 50, Priority = 0 });
            bottom.Add(new ScreenSprite { Name = "Party frame", Cells = 63, Anim = 62, Drawing = 64, Colours = 61, Palette = 2, Seq = 2, X = 183, Y = 55 });

            (int first, int second) = Types();
            string id = _specs[pageIndex].Id;
            if (id == "info")
            {
                if (second < 0) bottom.Add(TypeIcon("Type", first, 108, 48, null));
                else
                {
                    bottom.Add(TypeIcon("First type", first, 91, 48, null));
                    bottom.Add(TypeIcon("Second type", second, 125, 48, null));
                }
            }
            if (id == "skills")
            {
                for (int m = 0; m < SampleMoves.Length; m++)
                    bottom.Add(TypeIcon("Move " + (m + 1) + " type", MoveIcon(SampleMoves[m], false), 24, 16 + m * 32, null));
                bottom.Add(TypeIcon("First type", first, 200, 24, Picked));
                if (second >= 0) bottom.Add(TypeIcon("Second type", second, 234, 24, Picked));
                if (Move(SampleMoves[0]) is MoveData picked)
                    bottom.Add(new ScreenSprite { Name = "Category", Picture = "kind:" + (int)picked.Split, X = 232, Y = 40, When = Picked });
            }
            if (id == "ribbons")
            {
                // Five Pokéathlon stars under each of the five stats.
                string[] stats = { "Speed", "Power", "Skill", "Stamina", "Jump" };
                for (int r = 0; r < stats.Length; r++)
                    for (int k = 0; k < 5; k++)
                        top.Add(new ScreenSprite { Name = $"{stats[r]} star {k + 1}", Cells = 67, Anim = 68, Drawing = 66, Colours = 65,
                                                   X = 64 + k * 16, Y = 48 + r * 32, When = Athlon });
            }
        }

        private static ScreenSprite TypeIcon(string name, int type, int x, int y, string when) =>
            new() { Name = name, Picture = "type:" + type, X = x, Y = y, When = when };

        // ── what the game prints ──────────────────────────────────────────────────────────────────

        private const string Picked = "A move picked";
        // HGSS shows the Pokéathlon side only for a Pokémon that has Pokéathlon stats.
        private const string Athlon = "With Pokéathlon stats";

        private static Box Label(string table, int index, string name, int line, Ink colour = Ink.White,
                                 ScreenTextAlign at = ScreenTextAlign.Left, int x = 0, int y = 0, int font = 0, string when = null) =>
            new(table, index, name, new[] { new Print(line, Colour: colour, At: at, X: x, Y: y, Font: font) }, when);

        private static Box Value(string table, int index, string name, int line, string value, Ink colour = Ink.Black,
                                 ScreenTextAlign at = ScreenTextAlign.Left, int x = 0, int y = 0, int bank = -1, string when = null) =>
            new(table, index, name, new[] { new Print(line, value == null ? null : new[] { value }, colour, at, x, y, bank) }, when);

        // A slash count printed the way both games' current-and-maximum routine does: the slash centred on x.
        private static Print Count(int current, int max, int x, int y) =>
            new(-1, new[] { current.ToString(), max.ToString() }, Ink.Black, ScreenTextAlign.Slash, x, y, Kind: ScreenTextKind.Sample);

        // The held item, level and nickname stay up on every page.
        private IEnumerable<Box> Always()
        {
            string level = Num(SampleLevel, 3);
            Print[] nickname =
            {
                new(-1, Colour: Ink.White, Kind: ScreenTextKind.SpeciesName),
                // A male sample: the game prints the male sign in blue at the box's right edge.
                new(1, Colour: Ink.Blue, At: ScreenTextAlign.BoxRight),
            };
            return _hgss
                ? new[]
                {
                    Label("Static", 6, "Held item heading", 4),
                    Value("Static", 31, "Held item", 6, null),
                    Value("Static", 32, "Level", 3, level, x: 16),
                    new Box("Static", 33, "Nickname", nickname),
                    Label("Static", 23, "Cancel", 146, at: ScreenTextAlign.BoxCentre, font: 4),
                }
                : new[]
                {
                    Label("Static", 6, "Held item heading", 4),
                    Value("Static", 32, "Held item", 6, null),
                    Value("Static", 33, "Level", 3, level, x: 16),
                    new Box("Static", 34, "Nickname", nickname),
                };
        }

        // A move box: the name at 1, 2 (white in Platinum, black in HGSS), "PP" at 16, 16, and the PP count about 60, 16.
        private Box MoveBox(string table, int index, int move, string when = null)
        {
            MoveData data = Move(move);
            int pp = data?.Pp ?? 0;
            return new Box(table, index, "Move " + (index % 8 + 1), new[]
            {
                new Print(move, Colour: _hgss ? Ink.Black : Ink.White, X: 1, Y: 2, Bank: MoveNamesTextNumbers),
                new Print(135, Colour: Ink.Black, X: 16, Y: 16),
                Count(pp, pp, 60, 16),
            }, when);
        }

        // Power and accuracy: "---" for none, else the number; Platinum pads it to three digits with spaces and
        // centres it, HGSS right-aligns it.
        private Box Attribute(string table, int index, string name, int line, int value, bool none)
        {
            ScreenTextAlign at = _hgss ? ScreenTextAlign.BoxRight : ScreenTextAlign.BoxCentre;
            return none
                ? Label(table, index, name, 154, Ink.Black, at, when: Picked)
                : Value(table, index, name, line, Num(value, 3, _hgss ? '\0' : ' '), at: at, when: Picked);
        }

        private Spec[] PlatinumSpecs()
        {
            (int hp, int attack, int defense, int spAtk, int spDef, int speed) = Stats();
            int ability = Ability();
            MoveData tackle = Move(SampleMoves[0]);
            Box[] moves(string table) => SampleMoves.Select((m, i) => MoveBox(table, i, m)).ToArray();
            return new[]
            {
                new Spec("info", "Info", 3, 15, new[]
                {
                    Label("Static", 0, "Title", 7),
                    Label("Static", 7, "Pokédex number heading", 8),
                    Label("Static", 8, "Name heading", 10),
                    Label("Static", 9, "Type heading", 12),
                    Label("Static", 10, "Trainer heading", 13),
                    Label("Static", 11, "ID heading", 15),
                    Label("Static", 12, "Exp. points heading", 17),
                    Label("Static", 13, "Next level heading", 19),
                    Value("Info", 0, "Pokédex number", 9, Num(_sample.Species, 3, '0'), at: ScreenTextAlign.BoxCentre),
                    new Box("Info", 1, "Name", new[] { new Print(-1, Colour: Ink.Black, At: ScreenTextAlign.BoxCentre, Kind: ScreenTextKind.SpeciesName) }),
                    new Box("Info", 2, "Trainer", new[] { new Print(-1, new[] { "Lucas" }, Ink.Blue, ScreenTextAlign.BoxCentre, Kind: ScreenTextKind.Sample) }),
                    Value("Info", 3, "ID", 16, Num(12345, 5, '0'), at: ScreenTextAlign.BoxCentre),
                    Value("Info", 4, "Exp. points", 18, Num(125000, 7, ' '), at: ScreenTextAlign.BoxCentre),
                    Value("Info", 5, "Next level", 21, Num(7651, 7, ' '), at: ScreenTextAlign.BoxCentre),
                }, Exp: true),
                new Spec("memo", "Trainer memo", 4, 15, new[]
                {
                    Label("Static", 1, "Title", 23),
                    // The memo's first line, the nature (Hardy for the sample).
                    Label("Memo", 0, "Memo", 24, Ink.Black),
                }),
                new Spec("skills", "Skills", 5, 15, new[]
                {
                    Label("Static", 2, "Title", 109),
                    Label("Static", 15, "HP heading", 110),
                    Label("Static", 16, "Attack heading", 111),
                    Label("Static", 17, "Defense heading", 112),
                    Label("Static", 18, "Sp. Atk heading", 113),
                    Label("Static", 19, "Sp. Def heading", 114),
                    Label("Static", 20, "Speed heading", 115),
                    Label("Static", 21, "Ability heading", 116),
                    new Box("Skills", 0, "HP", new[] { Count(hp, hp, WidthOf("Skills", 0) * 4, 0) }),
                    Value("Skills", 1, "Attack", 120, Num(attack, 3), at: ScreenTextAlign.BoxRight),
                    Value("Skills", 2, "Defense", 121, Num(defense, 3), at: ScreenTextAlign.BoxRight),
                    Value("Skills", 3, "Sp. Atk", 122, Num(spAtk, 3), at: ScreenTextAlign.BoxRight),
                    Value("Skills", 4, "Sp. Def", 123, Num(spDef, 3), at: ScreenTextAlign.BoxRight),
                    Value("Skills", 5, "Speed", 124, Num(speed, 3), at: ScreenTextAlign.BoxRight),
                    Value("Skills", 6, "Ability", 125, BankLine(AbilityNamesTextNumber, ability)),
                    Value("Skills", 7, "Ability description", ability, null, bank: _sites.AbilityDescriptionBank),
                }, Hp: true),
                new Spec("battle_moves", "Battle moves", 6, 15, new[]
                {
                    Label("Static", 4, "Title", 128),
                    Label("Static", 35, "Button hint", 129, when: "Moves"),
                    Label("Static", 35, "Button hint", 152, when: Picked),
                    Label("Static", 25, "Category heading", 149, when: Picked),
                    Label("Static", 26, "Power heading", 147, when: Picked),
                    Label("Static", 27, "Accuracy heading", 148, when: Picked),
                    Label("Static", 24, "Cancel", 146, x: 0, y: 2, when: Picked),
                    Attribute("BattleMoves", 5, "Power", 150, tackle?.Damage ?? 0, (tackle?.Damage ?? 0) <= 1),
                    Attribute("BattleMoves", 6, "Accuracy", 151, tackle?.Accuracy ?? 0, (tackle?.Accuracy ?? 0) == 0),
                    Value("BattleMoves", 7, "Move description", SampleMoves[0], null, bank: MoveDescriptionsTextNumbers, when: Picked),
                }.Concat(moves("BattleMoves")).ToArray(), Variants: new[] { "Moves", Picked }),
                new Spec("condition", "Condition", 7, 15, new[]
                {
                    Label("Static", 3, "Title", 126),
                    Label("Static", 22, "Sheen heading", 127),
                }),
                new Spec("contest_moves", "Contest moves", 8, 15, new[]
                {
                    Label("Static", 5, "Title", 157),
                    Label("Static", 35, "Button hint", 158, when: "Moves"),
                    Label("Static", 29, "Appeal points heading", 160, Ink.Black, ScreenTextAlign.BoxCentre, when: Picked),
                    // The contest effect's description: line 46 + effect - 1 of the contest effects bank.
                    Value("ContestMoves", 5, "Move description", 46 + (tackle?.ContestAppeal ?? 1) - 1, null, bank: _sites.ContestEffectBank, when: Picked),
                }.Concat(moves("ContestMoves")).ToArray(), Variants: new[] { "Moves", Picked }),
                new Spec("ribbons", "Ribbons", 9, 15, new[]
                {
                    Label("Static", 31, "Title", 179),
                    Label("Static", 30, "Ribbon count heading", 182, Ink.Black),
                    // The sample has no ribbons, so the game prints 0 and no button hint.
                    Value("Ribbons", 0, "Ribbon count", 183, Num(0, 3)),
                }),
                new Spec("exit", "Exit", 10, 15, new[]
                {
                    Label("Static", 28, "Close window", 162, at: ScreenTextAlign.BoxCentre),
                    Label("Static", 35, "Button hint", 161),
                }),
            };
        }

        private Spec[] HgssSpecs()
        {
            (int hp, int attack, int defense, int spAtk, int spDef, int speed) = Stats();
            int ability = Ability();
            MoveData tackle = Move(SampleMoves[0]);
            return new[]
            {
                new Spec("info", "Info", 13, 12, new[]
                {
                    Label("Static", 1, "Top title", 23),
                    Label("Static", 0, "Title", 7),
                    Label("Static", 7, "Pokédex number heading", 8),
                    Label("Static", 8, "Name heading", 10),
                    Label("Static", 9, "Type heading", 12),
                    Label("Static", 10, "Trainer heading", 13),
                    Label("Static", 11, "ID heading", 15),
                    Label("Static", 12, "Exp. points heading", 17),
                    Label("Static", 13, "Next level heading", 19),
                    Value("Info", 0, "Pokédex number", 9, Num(_sample.Species, 3, '0'), at: ScreenTextAlign.BoxCentre),
                    new Box("Info", 1, "Name", new[] { new Print(-1, Colour: Ink.Black, At: ScreenTextAlign.BoxCentre, Kind: ScreenTextKind.SpeciesName) }),
                    new Box("Info", 2, "Trainer", new[] { new Print(-1, new[] { "Ethan" }, Ink.Blue, ScreenTextAlign.BoxCentre, Kind: ScreenTextKind.Sample) }),
                    Value("Info", 3, "ID", 16, Num(12345, 5, '0'), at: ScreenTextAlign.BoxCentre),
                    Value("Info", 4, "Exp. points", 18, Num(125000, 7), at: ScreenTextAlign.BoxRight),
                    Value("Info", 5, "Next level", 21, Num(7651, 7), at: ScreenTextAlign.BoxRight),
                    // The memo's first line, the nature, printed 6 pixels in.
                    Label("Info", 6, "Memo", 24, Ink.Black, x: 6),
                    // Shiny Leaf: printed only once the Pokémon has a leaf, which the sample doesn't.
                    new Box("Info", 7, "Shiny Leaf", Array.Empty<Print>()),
                }, Exp: true),
                new Spec("skills", "Skills and moves", 19, 17, new[]
                {
                    Label("Static", 2, "Title", 109),
                    Label("Static", 15, "HP heading", 110),
                    // A neutral nature prints the stat headings white; a raising or lowering one changes their shadow.
                    Label("Static", 16, "Attack heading", 111),
                    Label("Static", 17, "Defense heading", 112),
                    Label("Static", 18, "Sp. Atk heading", 113),
                    Label("Static", 19, "Sp. Def heading", 114),
                    Label("Static", 20, "Speed heading", 115),
                    Label("Static", 21, "Ability heading", 116, x: 3),
                    new Box("Skills", 0, "HP", new[] { Count(hp, hp, WidthOf("Skills", 0) * 4, 0) }),
                    Value("Skills", 1, "Attack", 120, Num(attack, 3), at: ScreenTextAlign.BoxRight),
                    Value("Skills", 2, "Defense", 121, Num(defense, 3), at: ScreenTextAlign.BoxRight),
                    Value("Skills", 3, "Sp. Atk", 122, Num(spAtk, 3), at: ScreenTextAlign.BoxRight),
                    Value("Skills", 4, "Sp. Def", 123, Num(spDef, 3), at: ScreenTextAlign.BoxRight),
                    Value("Skills", 5, "Speed", 124, Num(speed, 3), at: ScreenTextAlign.BoxRight),
                    Value("Skills", 6, "Ability", 125, BankLine(AbilityNamesTextNumber, ability)),
                    Value("Skills", 7, "Ability description", ability, null, x: 3, bank: _sites.AbilityDescriptionBank),
                    Label("Static", 4, "Moves title", 128),
                    MoveBox("Skills", 8, SampleMoves[0]),
                    MoveBox("Skills", 9, SampleMoves[1]),
                    MoveBox("Skills", 10, SampleMoves[2]),
                    MoveBox("Skills", 11, SampleMoves[3]),
                    Label("Static", 24, "Category heading", 149, when: Picked),
                    Label("Static", 25, "Power heading", 147, when: Picked),
                    Label("Static", 26, "Accuracy heading", 148, when: Picked),
                    Attribute("Skills", 13, "Power", 150, tackle?.Damage ?? 0, (tackle?.Damage ?? 0) <= 1),
                    Attribute("Skills", 14, "Accuracy", 151, tackle?.Accuracy ?? 0, (tackle?.Accuracy ?? 0) == 0),
                    Value("Skills", 15, "Move description", SampleMoves[0], null, bank: MoveDescriptionsTextNumbers, when: Picked),
                }, Hp: true, Variants: new[] { "Moves", Picked }),
                new Spec("ribbons", "Performance and ribbons", 10, 9, new[]
                {
                    Label("Static", 5, "Performance title", 157, when: Athlon),
                    Label("Ribbons", 3, "Speed", 187, Ink.Black, x: 4, when: Athlon),
                    Label("Ribbons", 4, "Power", 188, Ink.Black, x: 4, when: Athlon),
                    Label("Ribbons", 5, "Skill", 189, Ink.Black, x: 4, when: Athlon),
                    Label("Ribbons", 6, "Stamina", 190, Ink.Black, x: 4, when: Athlon),
                    Label("Ribbons", 7, "Jump", 191, Ink.Black, x: 4, when: Athlon),
                    Label("Static", 30, "Ribbons title", 179),
                    Label("Static", 29, "Ribbon count heading", 182, Ink.Black),
                    // The sample has no ribbons, so the game prints 0.
                    Value("Ribbons", 0, "Ribbon count", 183, Num(0, 3)),
                }, Variants: new[] { "Ribbons", Athlon }),
            };
        }

        private int WidthOf(string table, int index) => WindowOf(table, index)?.Width ?? 0;

        // ── the layout ────────────────────────────────────────────────────────────────────────────

        /// <summary>The text boxes and bars on a page, for the layout list.</summary>
        private IReadOnlyList<ScreenLayoutItem> Items(int page, string variant)
        {
            List<ScreenLayoutItem> items = new();
            if (_now == null || page < 0 || page >= _specs.Length) return items;
            Spec spec = _specs[page];
            HashSet<(string, int)> listed = new();
            foreach (Box box in BoxesOf(spec))
            {
                if (!ScreenPage.Shows(box.When, variant) || !listed.Add((box.Table, box.Index))) continue;
                SummaryScreenLayout.Window w = WindowOf(box.Table, box.Index);
                if (w == null || w.Width == 0 || w.Height == 0) continue;
                items.Add(new ScreenLayoutItem
                {
                    Name = box.Name, Side = Where(w.Layer).Side, X = w.X, Y = w.Y, Width = w.Width, Height = w.Height,
                    CanResize = true, Key = (box.Table, box.Index),
                });
            }
            if (spec.Hp) items.Add(BarItem("HP bar", _now.Hp));
            if (spec.Exp) items.Add(BarItem("EXP bar", _now.Exp));
            return items;
        }

        private static ScreenLayoutItem BarItem(string name, SummaryScreenLayout.Bar bar) => new()
        {
            Name = name, Side = Where(bar.Layer).Side, X = bar.X, Y = bar.Y, Width = bar.Tiles, Height = 1, Key = name,
        };

        private (byte[] Bytes, string Error) Place(ScreenLayoutItem item, int x, int y, int width, int height)
        {
            byte[] original = SummaryScreenLayout.ReadBytes();
            SummaryScreenLayout next = SummaryScreenLayout.FromBytes(_now.ToBytes(original));
            if (item.Key is string bar)
            {
                SummaryScreenLayout.Bar b = bar == "HP bar" ? next.Hp : next.Exp;
                if (x < 0 || y < 0 || x + b.Tiles > SummaryScreenLayout.MapColumns || y >= SummaryScreenLayout.MapRows) return (null, "That goes off the screen.");
                b.X = x;
                b.Y = y;
                return (next.ToBytes(original), null);
            }
            (string tableName, int index) = ((string, int))item.Key;
            SummaryScreenLayout.Table table = next.Tables.First(t => t.Page == tableName);
            SummaryScreenLayout.Window wanted = table.Windows[index].Copy();
            if (x < 0 || y < 0 || x + width > SummaryScreenLayout.MapColumns || y + height > SummaryScreenLayout.MapRows)
                return (null, "That goes off the screen.");
            wanted.X = x;
            wanted.Y = y;
            wanted.Width = width;
            wanted.Height = height;
            string why = next.WhyNot(table, index, wanted, _original);
            table.Windows[index] = wanted;
            // Out of tiles where it is: hand every box its tiles again, which makes room when there is any.
            if (why != null) why = next.Repack(_original);
            if (why != null) return (null, why);
            return (next.ToBytes(original), null);
        }

        private sealed class SummaryLayout : ScreenBookLayout
        {
            private readonly SummaryBook _book;
            private readonly PendingArchiveWrites.Target _target =
                new("Summary screen layout", SummaryScreenLayout.ReadBytes, SummaryScreenLayout.WriteBytes);

            public SummaryLayout(SummaryBook book) => _book = book;

            public override PendingArchiveWrites.Target Target => _target;

            public override void Use(byte[] bytes)
            {
                try { _book.Build(SummaryScreenLayout.FromBytes(bytes)); }
                catch (Exception ex) { AppLogger.Error("Summary screen layout: " + ex.Message); }
            }

            public override IReadOnlyList<ScreenLayoutItem> Items(int page, string variant) => _book.Items(page, variant);

            public override (byte[] Bytes, string Error) Place(ScreenLayoutItem item, int x, int y, int width, int height) =>
                _book.Place(item, x, y, width, height);
        }
    }
}
