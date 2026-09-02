using System;
using System.Collections.Generic;
using System.IO;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Both screens of a battle, drawn from the ROM's own graphics, one piece at a time.
    ///
    /// The top screen carries the backdrop, the ground the Pokemon stand on, the HP bars and the message
    /// box; the touch screen carries the command panel. Which screen a thing is on is not a guess: the
    /// gauges are on the main screen, the message window sits on its own layer below them, and every
    /// resource in battle_input.c is 2DSUB.
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
            public bool ShowCommandPanel = true;
            public string Message = "Wild PIDGEY appeared!";
            public string PokemonName = "PIDGEY";
            public int Level = 5;
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
            AddGauges(pieces);
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
                        for (int c = 0; c < 3; c++)
                            canvas[d + c] = (byte)((p.Rgba[s + c] * a + canvas[d + c] * (255 - a)) / 255);
                        canvas[d + 3] = (byte)Math.Min(255, canvas[d + 3] + a);
                    }
                }
            }
            return canvas;
        }

        private void AddBackdrop(List<Piece> pieces, Options o)
        {
            int bg = o.BackdropId >= 0 ? o.BackdropId : BattleGroundRenderer.BackdropForTerrain(o.TerrainId);
            var piece = new Piece
            {
                Name = "Backdrop",
                What = "The sky and ground behind the battle.",
                Archive = DirNames.battleBg,
            };
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

        private void AddGauges(List<Piece> pieces)
        {
            var r = _ground ??= new BattleGroundRenderer();
            foreach (bool player in new[] { false, true })
            {
                string thing = player ? "SINGLE_GAGE2" : "SINGLE_GAGE1";
                var piece = new Piece
                {
                    Name = player ? "HP bar, your side" : "HP bar, their side",
                    What = "The name, level and health of one Pokemon.",
                    Archive = DirNames.battleObj,
                    Drawing = BattleObjects.Find(thing, "Drawing"),
                    Layout = BattleObjects.Find(thing, "As it appears"),
                    Colours = BattleObjects.Find("GAGE_PALETTE", "Colours"),
                    SharedNote = "Every battle in the game draws this same bar.",
                };
                try
                {
                    var g = r.BuildGauge(player);
                    if (g?.Rgba == null) piece.Whynot = "This HP bar could not be drawn.";
                    else { piece.Rgba = g.Rgba; piece.Width = g.Width; piece.Height = g.Height; piece.Left = g.Left; piece.Top = g.Top; }
                }
                catch (Exception ex) { piece.Whynot = "This HP bar could not be drawn: " + ex.Message; }
                pieces.Add(piece);
            }
        }

        // fight.h:22-25 puts the writing at tile 2,19 and makes it 27 by 4 tiles. The frame the games draw
        // round it adds two tile columns left, three right and a row above and below, which comes to the
        // whole screen width and the bottom 48 pixels.
        public const int MessageTilesWide = 27, MessageTilesHigh = 4;
        public const int MessageTop = 144;

        private void AddMessageBox(List<Piece> pieces, Options o)
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
                var frame = FieldWindowFrame.Load(o.WindowStyle);
                if (frame == null) piece.Whynot = "The window frames could not be read from this ROM.";
                else
                {
                    piece.Rgba = frame.Compose(MessageTilesWide, MessageTilesHigh, out int w, out int h);
                    piece.Width = w; piece.Height = h;
                    PaintPaper(piece, frame.PaperArgb);
                    piece.Drawing = FieldWindowFrame.FirstGraphicEntry + o.WindowStyle;
                    piece.Colours = FieldWindowFrame.FirstPaletteEntry + o.WindowStyle;
                }
            }
            catch (Exception ex) { piece.Whynot = "This message box could not be drawn: " + ex.Message; }
            pieces.Add(piece);
        }

        // battle_input.c:204-212 names the screens the touch panel is built from, :2453 reads them out of
        // the battle background archive, and :2471 loads BATTLE_W_NCLR as their colours.
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

        /// <summary>
        /// The touch screen, layer by layer. BgMakeData in battle_input.c:1290 gives the command screen
        /// three of them and says which is in front: the background at priority 3, the move panel at 3 and
        /// the command panel at 2, so they go down background first. All three are drawn from
        /// BATTLE_W_NCGR, not from BATTLE_WBG0A, which nothing in the battle code reads.
        /// </summary>
        private void AddTouchPanel(List<Piece> pieces, Options o)
        {
            AddPanelLayer(pieces, "Touch screen background", "BATTLE_WBG0B_NSCR_BIN",
                          "The panel everything else sits on.", opaque: true);
            if (!o.ShowCommandPanel) return;
            AddPanelLayer(pieces, "Move panel", "BATTLE_WBG2A_NSCR_BIN",
                          "The part of the command screen the move buttons sit in.");
            AddPanelLayer(pieces, "Command buttons", "BATTLE_WBG1A_NSCR_BIN",
                          "Fight, Bag, Pokemon and Run.");
        }

        /// <summary>
        /// The colours the touch panel is drawn with. battle_input.c:2471 loads the whole of
        /// BATTLE_W_NCLR, then :2474 lays the scene's own first row over the top of it: one row's worth
        /// of bytes, at the start. So the scene tints row zero and the rest of the palette stays.
        ///
        /// Row one is where the buttons keep their reds. Writing the scene over row one instead paints
        /// the Fight button black, because the scene palette's fourth colour onwards are all black.
        /// </summary>
        private (byte r, byte g, byte b)[] PanelColours()
        {
            var wide = NitroBgCodec.ReadPalette(GraphicAssets.Unsqueeze(_bg.Get(BattleBgNames.Find("BATTLE_W_NCLR"))),
                                               out int count);
            var all = new (byte r, byte g, byte b)[256];
            for (int i = 0; i < all.Length && i < count; i++) all[i] = wide[i];

            int sceneAt = BattleBgNames.Find("BATTLE_W_00_NCLR");
            if (sceneAt >= 0)
            {
                var scene = NitroBgCodec.ReadPalette(GraphicAssets.Unsqueeze(_bg.Get(sceneAt)), out int sceneCount);
                for (int i = 0; i < 16 && i < sceneCount; i++) all[i] = scene[i];
            }
            return all;
        }

        private void AddPanelLayer(List<Piece> pieces, string name, string screenEntry, string what,
                                   bool opaque = false)
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
                                                    PanelColours(), 256,
                                                    GraphicAssets.Unsqueeze(_bg.Get(scr)),
                                                    transparentZero: !opaque);
                    if (bg?.Rgba == null) piece.Whynot = "This panel could not be put together.";
                    else { piece.Rgba = bg.Rgba; piece.Width = bg.Width; piece.Height = bg.Height; }
                }
            }
            catch (Exception ex) { piece.Whynot = "This panel could not be drawn: " + ex.Message; }
            pieces.Add(piece);
        }
    }
}
