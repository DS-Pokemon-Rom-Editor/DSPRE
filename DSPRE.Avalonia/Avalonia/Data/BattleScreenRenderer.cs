using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Both screens of a battle, drawn from the ROM's own graphics, one piece at a time.
    ///
    /// The top screen carries the backdrop, the ground the Pokemon stand on, the HP bars and the message
    /// box; the touch screen carries the battle menus. The gauges are on the main screen, the message
    /// window sits on its own layer below them, and every menu resource is loaded to the sub engine
    /// (pokeheartgold src/battle/battle_input.c, pokeplatinum src/battle/battle_cursor.c).
    /// </summary>
    public sealed class BattleScreenRenderer
    {
        public const int ScreenWidth = 256;
        public const int ScreenHeight = 192;

        /// <summary>One thing on one of the screens, with where it sits and which files it came from.</summary>
        public sealed class Piece
        {
            public string Name;
            public string What;              // one line saying what it is
            public bool Touch;               // false = top screen
            public byte[] Rgba;
            public int Width, Height, Left, Top;
            public DirNames Archive;
            public int Drawing = -1, Layout = -1, Colours = -1, Arrangement = -1;
            public string SharedNote;        // what else changes when this one does
            public string CannotEditBecause; // set when the painter cannot be handed this one
            public string Whynot;            // set when Rgba is null

            // Hardware alpha blend weights in sixteenths; own 16 and below 0 is a plain draw.
            public int BlendOwn = 16, BlendBelow;

            // Most of these are drawn on a whole screen's worth of room and use a corner of it, so the
            // part worth clicking and outlining is the part that has any paint on it.
            public int PaintedLeft, PaintedTop, PaintedWidth, PaintedHeight;

            /// <summary>Whether this piece has paint at a point on the screen it belongs to.</summary>
            public bool Covers(int screenX, int screenY)
            {
                int x = screenX - Left, y = screenY - Top;
                if (Rgba == null || x < 0 || y < 0 || x >= Width || y >= Height) return false;
                return Rgba[(y * Width + x) * 4 + 3] != 0;
            }

            internal void MeasurePaint()
            {
                PaintedLeft = Left; PaintedTop = Top; PaintedWidth = Width; PaintedHeight = Height;
                if (Rgba == null) return;
                int minX = Width, minY = Height, maxX = -1, maxY = -1;
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                        if (Rgba[(y * Width + x) * 4 + 3] != 0)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                if (maxX < 0) return;
                PaintedLeft = Left + minX; PaintedTop = Top + minY;
                PaintedWidth = maxX - minX + 1; PaintedHeight = maxY - minY + 1;
            }
        }

        /// <summary>What the screens are showing, which the editor lets you change.</summary>
        public sealed class Options
        {
            public int TerrainId;
            public int BackdropId = -1;      // -1 = the one that goes with the terrain
            public int TimeOfDay;            // 0 day, 1 evening, 2 night
            public int WindowStyle;          // 0..19, the player's own setting
            public TouchMenu Menu = TouchMenu.Command;
            public string Message = "Wild PIDGEY appeared!";
            public string PokemonName = "PIDGEY";
            public int Level = 5;
            public BattleGaugeText.Gender Gender = BattleGaugeText.Gender.Genderless;
            public BattleGaugeText.Status Status = BattleGaugeText.Status.None;
            public bool Doubles;             // two bars a side instead of one
            public bool NumbersInsteadOfBar; // in a double battle the games swap the two over
        }

        private readonly ScriptNarc _bg = new ScriptNarc(DirNames.battleBg);
        private BattleGroundRenderer _ground;
        private BattleBgRenderer _backdrop;

        public bool Available => _bg.Available;

        /// <summary>Every piece of both screens, back to front, ready to draw.</summary>
        public List<Piece> Build(Options o)
        {
            var pieces = new List<Piece>();
            o ??= new Options();

            AddBackdrop(pieces, o);
            AddGround(pieces, o);
            AddGauges(pieces, o);
            AddMessageBox(pieces, o);
            AddTouchPanel(pieces, o);
            foreach (var p in pieces) p.MeasurePaint();
            return pieces;
        }

        /// <summary>
        /// The piece a point on one screen belongs to, which is the last one drawn there. The pieces are
        /// listed back to front, so the search runs the other way.
        /// </summary>
        public static Piece At(IReadOnlyList<Piece> pieces, bool touch, int x, int y)
        {
            for (int i = pieces.Count - 1; i >= 0; i--)
                if (pieces[i].Touch == touch && pieces[i].Covers(x, y)) return pieces[i];
            return null;
        }

        /// <summary>One screen's pieces drawn over each other, as straight RGBA.</summary>
        public static byte[] Flatten(IReadOnlyList<Piece> pieces, bool touch)
        {
            var canvas = new byte[ScreenWidth * ScreenHeight * 4];
            foreach (var p in pieces)
            {
                if (p.Touch != touch || p.Rgba == null) continue;
                for (int y = 0; y < p.Height; y++)
                {
                    int cy = p.Top + y;
                    if (cy < 0 || cy >= ScreenHeight) continue;
                    for (int x = 0; x < p.Width; x++)
                    {
                        int cx = p.Left + x;
                        if (cx < 0 || cx >= ScreenWidth) continue;
                        int s = (y * p.Width + x) * 4, d = (cy * ScreenWidth + cx) * 4;
                        int a = p.Rgba[s + 3];
                        if (a == 0) continue;
                        if (p.BlendBelow > 0 && canvas[d + 3] != 0)
                        {
                            for (int c = 0; c < 3; c++)
                                canvas[d + c] = (byte)Math.Min(255, (p.Rgba[s + c] * p.BlendOwn + canvas[d + c] * p.BlendBelow) / 16);
                            continue;
                        }
                        for (int c = 0; c < 3; c++)
                            canvas[d + c] = (byte)((p.Rgba[s + c] * a + canvas[d + c] * (255 - a)) / 255);
                        canvas[d + 3] = (byte)Math.Min(255, canvas[d + 3] + a);
                    }
                }
            }
            return canvas;
        }

        /// <summary>The backdrop being shown: the one picked, or the one that goes with the terrain.</summary>
        public static int ResolveBackdrop(Options o) =>
            o.BackdropId >= 0 ? o.BackdropId : BattleGroundRenderer.BackdropForTerrain(o.TerrainId);

        private void AddBackdrop(List<Piece> pieces, Options o)
        {
            int bg = ResolveBackdrop(o);
            var piece = new Piece
            {
                Name = "Backdrop",
                What = "The sky and ground behind the battle.",
                Archive = DirNames.battleBg,
            };

            // Which files this backdrop is made of, so the editor can hand them over. Without these
            // the piece was the only one with no way at all to reach what it is drawn from.
            if (bg >= 0)
            {
                var files = BattleBgRenderer.BackdropFiles(bg);
                piece.Drawing = files.Drawing;
                piece.Arrangement = files.Tilemap;
                piece.Colours = files.PaletteDay + Math.Clamp(o.TimeOfDay, 0, 2);
                piece.SharedNote = "The arrangement behind every backdrop is the same file.";
                // The sheet is twice as wide as the backdrop it draws, because the arrangement picks
                // tiles out of it, so a picture the size of what you see here cannot go back into it.
                piece.CannotEditBecause = "A backdrop is arranged out of a wider sheet of tiles, so a "
                                        + "picture the size of this one cannot be put back. Open the "
                                        + "sheet in the Graphics window to change it.";
            }
            try
            {
                var img = bg >= 0 ? (_backdrop ??= new BattleBgRenderer()).BuildBackdrop(bg, o.TimeOfDay) : null;
                if (img?.Rgba == null) piece.Whynot = "This backdrop could not be drawn.";
                else { piece.Rgba = img.Rgba; piece.Width = img.Width; piece.Height = img.Height; }
            }
            catch (Exception ex) { piece.Whynot = "This backdrop could not be drawn: " + ex.Message; }
            pieces.Add(piece);
        }

        private void AddGround(List<Piece> pieces, Options o)
        {
            var r = _ground ??= new BattleGroundRenderer();
            var files = BattleGroundRenderer.TerrainFiles(o.TerrainId);
            (BattleGroundRenderer.GroundImage mine, BattleGroundRenderer.GroundImage enemy) both;
            try { both = r.Build(o.TerrainId, o.TimeOfDay); }
            catch { both = (null, null); }
            foreach (bool player in new[] { false, true })
            {
                var piece = new Piece
                {
                    Name = player ? "Ground, your side" : "Ground, their side",
                    What = "The tray the Pokemon stands on.",
                    Archive = DirNames.battleObj,
                    Drawing = files.HasValue ? (player ? files.Value.MineDrawing : files.Value.EnemyDrawing) : -1,
                    Layout = files.HasValue ? (player ? files.Value.MineLayout : files.Value.EnemyLayout) : -1,
                    Colours = files.HasValue ? files.Value.PaletteDay + o.TimeOfDay : -1,
                    SharedNote = "Every place that fights on this terrain uses it.",
                };
                var g = player ? both.mine : both.enemy;
                if (g?.Rgba == null) piece.Whynot = "This ground could not be drawn.";
                else { piece.Rgba = g.Rgba; piece.Width = g.Width; piece.Height = g.Height; piece.Left = g.Left; piece.Top = g.Top; }
                pieces.Add(piece);
            }
        }

        private void AddGauges(List<Piece> pieces, Options o)
        {
            foreach (var kind in BattleGaugeComposer.ForDoubleBattle(o.Doubles))
            {
                bool player = kind == BattleGaugeComposer.Kind.PlayerSingle
                           || kind == BattleGaugeComposer.Kind.PlayerNear
                           || kind == BattleGaugeComposer.Kind.PlayerFar;
                string thing = BattleGaugeComposer.GraphicOf(kind);
                var piece = new Piece
                {
                    Name = BattleGaugeComposer.NameOf(kind),
                    What = "The name, level and health of one Pokemon.",
                    Archive = DirNames.battleObj,
                    Drawing = BattleObjects.Find(thing, "Drawing"),
                    Layout = BattleObjects.Find(thing, "As it appears"),
                    Colours = BattleObjects.Find("GAGE_PALETTE", "Colours"),
                    SharedNote = "Every battle in the game draws this same bar.",
                };
                try
                {
                    // The writing goes into the bar's own picture, the way a battle does it, so it sits
                    // where the game puts it and nothing paints over the bar's slanted edge.
                    var g = BattleGaugeComposer.Build(kind, new BattleGaugeComposer.Showing
                    {
                        Name = o.PokemonName,
                        Level = o.Level,
                        Gender = o.Gender,
                        Status = o.Status,
                        // Only your own side shows numbers, and in a double battle only once the
                        // games have been asked to swap the bar for them.
                        ShowHealthNumbers = player && (!o.Doubles || o.NumbersInsteadOfBar),
                    });
                    if (g == null && !o.Doubles)
                    {
                        // Games we cannot read the letters of still get the bar itself.
                        var plain = (_ground ??= new BattleGroundRenderer()).BuildGauge(player);
                        if (plain?.Rgba != null)
                            g = new BattleGaugeComposer.Drawn
                            {
                                Rgba = plain.Rgba, Width = plain.Width,
                                Height = plain.Height, Left = plain.Left, Top = plain.Top,
                            };
                    }

                    if (g?.Rgba == null) piece.Whynot = "This HP bar could not be drawn.";
                    else { piece.Rgba = g.Rgba; piece.Width = g.Width; piece.Height = g.Height; piece.Left = g.Left; piece.Top = g.Top; }
                }
                catch (Exception ex) { piece.Whynot = "This HP bar could not be drawn: " + ex.Message; }
                pieces.Add(piece);
            }
        }

        // The battle message window's text area starts at tile 2,19 and is 27 by 4 tiles. The frame drawn
        // round it adds two tile columns left, three right and a row above and below, which comes to the
        // whole screen width and the bottom 48 pixels.
        public const int MessageTilesWide = 27, MessageTilesHigh = 4;
        public const int MessageTop = 144;

        /// <summary>
        /// The frame's outermost pixels are see-through, but the games do not show the battle behind
        /// them: the whole band the box sits in is filled. Checked against a captured Platinum frame,
        /// where every pixel of that band outside the paper is either black or the frame's own white,
        /// and none of it is the scene. Leaving them clear let a strip of grass show along the edges.
        /// </summary>
        private static void BlackOutTheRest(Piece piece)
        {
            if (piece.Rgba == null) return;
            for (int i = 0; i < piece.Width * piece.Height; i++)
            {
                if (piece.Rgba[i * 4 + 3] != 0) continue;
                piece.Rgba[i * 4] = 0;
                piece.Rgba[i * 4 + 1] = 0;
                piece.Rgba[i * 4 + 2] = 0;
                piece.Rgba[i * 4 + 3] = 255;
            }
        }

        private void AddMessageBox(List<Piece> pieces, Options o) => pieces.Add(BuildMessageBox(o.WindowStyle));

        /// <summary>The message box alone, at <see cref="MessageTop"/> across the screen.</summary>
        public static Piece BuildMessageBox(int windowStyle)
        {
            var piece = new Piece
            {
                Name = "Message box",
                What = "The box battle text is written in. Which of the twenty frames it uses is the "
                     + "player's own setting, the same one the field uses.",
                Archive = DirNames.windowFrames,
                Top = MessageTop,
                SharedNote = "This frame is the one the whole game writes in, field and battle alike.",
            };
            try
            {
                var frame = FieldWindowFrame.Load(windowStyle);
                if (frame == null) piece.Whynot = "The window frames could not be read from this ROM.";
                else
                {
                    piece.Rgba = frame.Compose(MessageTilesWide, MessageTilesHigh, out int w, out int h);
                    piece.Width = w; piece.Height = h;
                    PaintPaper(piece, frame.PaperArgb);
                    BlackOutTheRest(piece);
                    piece.Drawing = FieldWindowFrame.FirstGraphicEntry + windowStyle;
                    piece.Colours = frame.PaletteEntry;
                }
            }
            catch (Exception ex) { piece.Whynot = "This message box could not be drawn: " + ex.Message; }
            return piece;
        }

        /// <summary>
        /// Fills the middle of the box with the paper colour. The border the games draw never covers
        /// the middle, so without this the box is a rim round a hole.
        /// </summary>
        private static void PaintPaper(Piece piece, uint paperArgb)
        {
            const int Tile = FieldWindowFrame.TileSize;
            byte a = (byte)(paperArgb >> 24), r = (byte)(paperArgb >> 16),
                 g = (byte)(paperArgb >> 8), b = (byte)paperArgb;
            int left = 2 * Tile - 1, top = Tile - 1;
            int right = (2 + MessageTilesWide) * Tile + 1, bottom = (1 + MessageTilesHigh) * Tile + 1;
            for (int y = Math.Max(0, top); y < Math.Min(piece.Height, bottom); y++)
                for (int x = Math.Max(0, left); x < Math.Min(piece.Width, right); x++)
                {
                    int at = (y * piece.Width + x) * 4;
                    if (piece.Rgba[at + 3] != 0) continue;   // the border itself stays as it is
                    piece.Rgba[at] = r; piece.Rgba[at + 1] = g; piece.Rgba[at + 2] = b; piece.Rgba[at + 3] = a;
                }
        }

        /// <summary>One of the touch screen's layers: which arrangement it shows and how the hardware stacks it.</summary>
        private sealed record PanelLayer(string Screen, string Name, string What, int Priority, int BgNumber, bool Blended);

        private static readonly PanelLayer Background = new("BATTLE_WBG0B_NSCR_BIN", "Touch screen background",
            "The panel every menu sits on.", 3, 2, false);

        /// <summary>
        /// The layers of each menu, from the game's menu table (pokeheartgold src/battle/battle_input.c
        /// sBattleMenuTemplates and sBottomScreenBgTilemapId; the same table in pokeplatinum
        /// src/battle/battle_cursor.c). Layer 0 holds the buttons, layer 1 an overlay the hardware blends,
        /// layer 2 the background. The playback menu swaps its own stop screen into layer 0 when it opens.
        /// </summary>
        private static IEnumerable<PanelLayer> LayersOf(TouchMenu menu)
        {
            yield return Background;
            switch (menu)
            {
                case TouchMenu.Command:
                    yield return new("BATTLE_WBG2A_NSCR_BIN", "Command silhouette",
                        "The shape behind the command buttons, see-through over the background.", 3, 1, true);
                    yield return new("BATTLE_WBG1A_NSCR_BIN", "Command buttons", "Fight, Bag, Pokemon and Run.", 2, 0, false);
                    break;
                case TouchMenu.Fight:
                    yield return new("BATTLE_WBG1B_NSCR_BIN", "Move buttons",
                        "The four move buttons. The game colours each one after its move's type.", 2, 0, false);
                    break;
                case TouchMenu.Target:
                    yield return new("BATTLE_WBG1C_NSCR_BIN", "Target buttons", "Which Pokemon a move is aimed at.", 2, 0, false);
                    yield return new("BATTLE_WBG3A_NSCR_BIN", "Target outlines",
                        "The outlines over the target buttons, see-through.", 1, 1, true);
                    break;
                case TouchMenu.YesNo:
                    yield return new("BATTLE_WBG1D_NSCR_BIN", "Yes and No buttons", "Two buttons, for any yes or no question.", 2, 0, false);
                    break;
                case TouchMenu.Playback:
                    yield return new("BATTLE_WBG1STOP_NSCR_BIN", "Stop button", "Stops a recorded battle.", 2, 0, false);
                    break;
            }
        }

        /// <summary>Which menus this game has.</summary>
        public static IReadOnlyList<TouchMenu> MenusForGame() =>
            RomInfo.gameFamily == GameFamilies.DP
                ? new[] { TouchMenu.Background, TouchMenu.Command, TouchMenu.Fight, TouchMenu.Target, TouchMenu.YesNo }
                : new[] { TouchMenu.Background, TouchMenu.Command, TouchMenu.Fight, TouchMenu.Target, TouchMenu.YesNo, TouchMenu.Playback };

        public static string MenuName(TouchMenu m) => m switch
        {
            TouchMenu.Background => "No menu",
            TouchMenu.Command => "Command",
            TouchMenu.Fight => "Fight",
            TouchMenu.Target => "Target",
            TouchMenu.YesNo => "Yes/No",
            _ => "Playback",
        };

        // What the Fight preview colours its four buttons after: Normal, Fire, Water and Grass.
        private static readonly int[] SampleMoveTypes = { 0, 10, 11, 12 };
        private const int FirstMoveButtonRow = 8;

        /// <summary>
        /// The touch screen for one menu. Higher priority numbers are further back, and within a priority the
        /// higher layer number is further back, so the pieces go down in that order. Every layer is drawn
        /// from BATTLE_W_NCGR, not from BATTLE_WBG0A, which nothing in the battle code reads.
        /// </summary>
        private void AddTouchPanel(List<Piece> pieces, Options o)
        {
            var colours = PanelColours(ResolveBackdrop(o), o.Menu);
            // Layer 1 is blended over every layer under it. Platinum and HeartGold weigh it 8/16 over 12/16;
            // Diamond and Pearl ask for 27 and 4, and the hardware caps a weight at 16.
            (int own, int below) blend = RomInfo.gameFamily == GameFamilies.DP ? (16, 4) : (8, 12);
            foreach (var layer in LayersOf(o.Menu).OrderByDescending(l => l.Priority).ThenByDescending(l => l.BgNumber))
            {
                var piece = AddPanelLayer(pieces, layer.Name, layer.Screen, layer.What, colours, opaque: layer == Background);
                if (layer.Blended) { piece.BlendOwn = blend.own; piece.BlendBelow = blend.below; }
            }
        }

        /// <summary>
        /// The colours the touch panel is drawn with. The game loads the whole of BATTLE_W_NCLR, then in
        /// Platinum and HeartGold lays the first 16 colours of the backdrop's own scene palette over row 0
        /// (pokeheartgold src/battle/battle_input.c sBackgroundPaletteIds): BATTLE_W_00 to _16 for backdrops
        /// 0 to 16, BATTLE_W_YAB for backdrop 17, and nothing for the rest. Diamond and Pearl keep row 0.
        ///
        /// Rows 8 to 11 are placeholders in the file; the Fight menu fills them from the type palettes in
        /// its overlay, one row per move button.
        /// </summary>
        private (byte r, byte g, byte b)[] PanelColours(int backdrop, TouchMenu menu)
        {
            var wide = NitroBgCodec.ReadPalette(GraphicAssets.Unsqueeze(_bg.Get(BattleBgNames.Find("BATTLE_W_NCLR"))),
                                               out int count);
            var all = new (byte r, byte g, byte b)[256];
            for (int i = 0; i < all.Length && i < count; i++) all[i] = wide[i];

            if (RomInfo.gameFamily != GameFamilies.DP)
            {
                string tint = backdrop >= 0 && backdrop <= 16 ? $"BATTLE_W_{backdrop:D2}_NCLR"
                            : backdrop == 17 ? "BATTLE_W_YAB_NCLR" : null;
                int sceneAt = tint == null ? -1 : BattleBgNames.Find(tint);
                if (sceneAt >= 0)
                {
                    var scene = NitroBgCodec.ReadPalette(GraphicAssets.Unsqueeze(_bg.Get(sceneAt)), out int sceneCount);
                    for (int i = 0; i < 16 && i < sceneCount; i++) all[i] = scene[i];
                }
            }

            if (menu == TouchMenu.Fight)
            {
                for (int slot = 0; slot < SampleMoveTypes.Length; slot++)
                {
                    var row = BattleUiTables.MoveButtonPalette(SampleMoveTypes[slot]);
                    if (row == null) continue;
                    for (int i = 0; i < 16; i++)
                    {
                        int c = row[i];
                        all[(FirstMoveButtonRow + slot) * 16 + i] =
                            ((byte)((c & 0x1F) << 3), (byte)(((c >> 5) & 0x1F) << 3), (byte)(((c >> 10) & 0x1F) << 3));
                    }
                }
            }
            return all;
        }

        private Piece AddPanelLayer(List<Piece> pieces, string name, string screenEntry, string what,
                                    (byte r, byte g, byte b)[] colours, bool opaque)
        {
            var piece = new Piece
            {
                Name = name, What = what, Touch = true, Archive = DirNames.battleBg,
                // Every layer of the panel is drawn from the one sheet of tiles, so there is no
                // saying which layer a painted picture should go back into.
                CannotEditBecause = "All the touch screen layers share one sheet of tiles, so a "
                                  + "picture painted here cannot be put back into just this layer. "
                                  + "Open the sheet in the Graphics window to change it.",
            };
            try
            {
                int scr = BattleBgNames.Find(screenEntry);
                int chr = BattleBgNames.Find("BATTLE_W_NCGR_BIN");
                int pal = BattleBgNames.Find("BATTLE_W_NCLR");
                piece.Arrangement = scr; piece.Drawing = chr; piece.Colours = pal;
                if (scr < 0 || chr < 0 || pal < 0)
                {
                    piece.Whynot = "This game does not name the touch screen panel files.";
                }
                else
                {
                    // Colour zero is a real colour on the bottom layer, not a hole: the panel's own green
                    // is index 0. On the layers above it, colour zero is what lets the one below show.
                    var bg = NitroBgCodec.Composite(GraphicAssets.Unsqueeze(_bg.Get(chr)),
                                                    colours, 256,
                                                    GraphicAssets.Unsqueeze(_bg.Get(scr)),
                                                    transparentZero: !opaque);
                    if (bg?.Rgba == null) piece.Whynot = "This panel could not be put together.";
                    else { piece.Rgba = bg.Rgba; piece.Width = bg.Width; piece.Height = bg.Height; }
                }
            }
            catch (Exception ex) { piece.Whynot = "This panel could not be drawn: " + ex.Message; }
            pieces.Add(piece);
            return piece;
        }
    }

    /// <summary>The touch screen menus the editor can show.</summary>
    public enum TouchMenu { Background, Command, Fight, Target, YesNo, Playback }
}
