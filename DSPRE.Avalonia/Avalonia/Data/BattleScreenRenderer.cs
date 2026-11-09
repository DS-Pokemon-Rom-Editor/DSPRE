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
            public bool TakesButtonArt;      // the Command buttons use the Import buttons window
            public string Whynot;            // set when Rgba is null
            public bool UsesBarPieces;       // the battle copies its fill and status in from the bar pieces

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
            public int BackdropId = -1;      // -1 = match the terrain
            public int TimeOfDay;            // 0 day, 1 evening, 2 night
            public int WindowStyle;          // 0..19, the player's own setting
            public TouchMenu Menu = TouchMenu.Command;
            public string Message = "Wild PIDGEY appeared!";
            public string PokemonName = "PIDGEY";
            /// <summary>The Pokémon standing on both sides.</summary>
            public int Species = 16;
            public int Level = 5;
            public BattleGaugeText.Gender Gender = BattleGaugeText.Gender.Genderless;
            public BattleGaugeText.Status Status = BattleGaugeText.Status.None;
            public bool Doubles;             // two bars a side instead of one
            public int BallCounter;          // 1 the Safari Zone, 2 Pal Park: the player's bar is the ball counter
            public bool NumbersInsteadOfBar; // in a double battle the games swap the two over
            public double HealthLeft = 1.0;
            /// <summary>Command button tiles the menu clears, x, y, width and height: the first battle's and Pal Park's.</summary>
            public (int X, int Y, int W, int H)[] BlankedCommandTiles;
            /// <summary>Pal Park hides the shape behind the command buttons.</summary>
            public bool NoSilhouette;
            /// <summary>Battle Frontier loads other tiles, colours and background under the same buttons.</summary>
            public bool Frontier;
            /// <summary>Draws these squares from this page to show a pressed button.</summary>
            public (BattlePressFrames.Rect Rect, int Page)? Pressed;
        }

        private readonly ScriptNarc _bg = new ScriptNarc(DirNames.BattleBg);
        private BattleGroundRenderer _ground;
        private BattleBgRenderer _backdrop;

        public bool Available => _bg.Available;

        /// <summary>Every piece of both screens, back to front, ready to draw.</summary>
        public List<Piece> Build(Options o)
        {
            List<Piece> pieces = new List<Piece>();
            o ??= new Options();

            AddBackdrop(pieces, o);
            AddGround(pieces, o);
            AddPokemon(pieces, o);
            AddGauges(pieces, o);
            AddMessageBox(pieces, o);
            AddTouchPanel(pieces, o);
            foreach (Piece p in pieces) p.MeasurePaint();
            return pieces;
        }

        public List<Piece> BuildTouch(Options o)
        {
            List<Piece> pieces = new List<Piece>();
            AddTouchPanel(pieces, o);
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
            byte[] canvas = new byte[ScreenWidth * ScreenHeight * 4];
            foreach (Piece p in pieces)
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

        /// <summary>The backdrop shown: the picked one, or the terrain's.</summary>
        public static int ResolveBackdrop(Options o) =>
            o.BackdropId >= 0 ? o.BackdropId : BattleGroundRenderer.BackdropForTerrain(o.TerrainId);

        private void AddBackdrop(List<Piece> pieces, Options o)
        {
            int bg = ResolveBackdrop(o);
            Piece piece = new Piece
            {
                Name = "Backdrop",
                What = "The sky and ground behind the battle.",
                Archive = DirNames.BattleBg,
            };

            // The backdrop's files, so the editor can open them.
            if (bg >= 0)
            {
                (int Drawing, int Tilemap, int PaletteDay) files = BattleBgRenderer.BackdropFiles(bg);
                piece.Drawing = files.Drawing;
                piece.Arrangement = files.Tilemap;
                piece.Colours = files.PaletteDay + Math.Clamp(o.TimeOfDay, 0, 2);
                piece.SharedNote = "Shared by all backdrops";
                // The sheet is twice as wide as the backdrop it draws, because the arrangement picks
                // tiles out of it, so a picture the size of what you see here cannot go back into it.
                piece.CannotEditBecause = "A backdrop is cut from a wider sheet of tiles, so a picture this size can't go "
                                        + "back. Open the sheet in All graphics to change it.";
            }
            try
            {
                BattleBgRenderer.BgImage img = bg >= 0 ? (_backdrop ??= new BattleBgRenderer()).BuildBackdrop(bg, o.TimeOfDay) : null;
                if (img?.Rgba == null) piece.Whynot = "This backdrop couldn't be drawn.";
                else { piece.Rgba = img.Rgba; piece.Width = img.Width; piece.Height = img.Height; }
            }
            catch (Exception ex) { piece.Whynot = "This backdrop couldn't be drawn: " + ex.Message; }
            pieces.Add(piece);
        }

        private void AddGround(List<Piece> pieces, Options o)
        {
            BattleGroundRenderer r = _ground ??= new BattleGroundRenderer();
            (int PlayerDrawing, int OpponentDrawing, int PlayerLayout, int OpponentLayout, int PaletteDay)? files = BattleGroundRenderer.TerrainFiles(o.TerrainId);
            (BattleGroundRenderer.GroundImage playerGround, BattleGroundRenderer.GroundImage opponent) both;
            try { both = r.Build(o.TerrainId, o.TimeOfDay); }
            catch (Exception ex) { AppLogger.WarnOnce("BattleScreenRenderer.AddGround", ex); both = (null, null); }
            foreach (bool player in new[] { false, true })
            {
                Piece piece = new Piece
                {
                    Name = player ? "Player ground" : "Opponent ground",
                    What = "The tray the Pokémon stands on.",
                    Archive = DirNames.BattleObj,
                    Drawing = files.HasValue ? (player ? files.Value.PlayerDrawing : files.Value.OpponentDrawing) : -1,
                    Layout = files.HasValue ? (player ? files.Value.PlayerLayout : files.Value.OpponentLayout) : -1,
                    Colours = files.HasValue ? files.Value.PaletteDay + o.TimeOfDay : -1,
                    SharedNote = "Shared by this terrain",
                };
                BattleGroundRenderer.GroundImage g = player ? both.playerGround : both.opponent;
                if (g?.Rgba == null) piece.Whynot = "This ground couldn't be drawn.";
                else { piece.Rgba = g.Rgba; piece.Width = g.Width; piece.Height = g.Height; piece.Left = g.Left; piece.Top = g.Top; }
                pieces.Add(piece);
            }
        }

        // battle_display.c places the sprites at gEncounterCoords' y, 50 for the opponent and 112 for the player, at x 192 and 64. Each one
        // is drawn its height-table value lower and its sprite record's FrontY higher. The opponent's shadow is centred 36 below 50.
        private const int OpponentX = 192, OpponentY = 50, PlayerX = 64, PlayerY = 112, ShadowBelow = 36;

        private IBattleOffsetSource _offsets;

        private void AddPokemon(List<Piece> pieces, Options o)
        {
            if (o.Species <= 0) return;
            BattleOffsetRecord rec = default;
            try
            {
                _offsets ??= GameFamily switch
                {
                    GameFamilies.HGSS or GameFamilies.Plat => new CombinedTailSource(new OffsetNarc(DirNames.PokemonSpriteOffsets, SpeciesSpriteData.Size), withHeights: true),
                    GameFamilies.DP => new SeparateByteSource(DirNames.PokeYofs, DirNames.PokeShadowOfx, DirNames.PokeShadow),
                    _ => null,
                };
                _offsets?.TryLoad(DSUtils.ResolveBaseSpeciesId(o.Species), out rec);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { AppLogger.WarnOnce("BattleScreenRenderer.AddPokemon", ex); }

            GraphicAssets.Archive sprites = GraphicAssets.All.First(a => a.Dir == DirNames.PokemonBattleSprites);
            int shadow = ShadowSquares(rec.ShadowSize);
            if (shadow >= 0)
            {
                GraphicAssets.Archive other = GraphicAssets.All.First(a => a.Dir == DirNames.OtherPokemonBattleSprites);
                ScriptNarc otherNarc = new ScriptNarc(DirNames.OtherPokemonBattleSprites);
                int count = otherNarc.Available ? otherNarc.Count : 0;
                // The other sprites' last two members are the shadows (three 64 by 16 sizes) and their colours.
                GraphicAssets.Preview p = count >= 2 ? GraphicAssets.Render(other, count - 2, paletteIndex: count - 1) : null;
                pieces.Add(SpritePiece("Shadow", "The shadow under the front sprite.", DirNames.OtherPokemonBattleSprites, count - 2, p,
                                       0, shadow * 16, 64, 16, OpponentX + rec.ShadowX - 32, OpponentY + ShadowBelow - 8));
            }
            pieces.Add(SpritePiece("Front sprite", "The opponent's Pokémon.", DirNames.PokemonBattleSprites, o.Species * 6 + 3,
                                   GraphicAssets.Render(sprites, o.Species * 6 + 3), 0, 0, 80, 80, OpponentX - 40, OpponentY + rec.FrontM - 40 - rec.FrontY));
            pieces.Add(SpritePiece("Back sprite", "The player's Pokémon.", DirNames.PokemonBattleSprites, o.Species * 6 + 1,
                                   GraphicAssets.Render(sprites, o.Species * 6 + 1), 0, 0, 80, 80, PlayerX - 40, PlayerY + rec.BackM - 40));
        }

        // pokemon_sprite.c sShadowTextureCoords: small, medium and large are rows 0, 1 and 2. Size 0 draws none.
        private static int ShadowSquares(int size) => size is >= 1 and <= 3 ? size - 1 : -1;

        private static Piece SpritePiece(string name, string what, DirNames dir, int member, GraphicAssets.Preview p,
                                         int fromX, int fromY, int w, int h, int left, int top)
        {
            Piece piece = new Piece
            {
                Name = name, What = what, Archive = dir, Drawing = member, Left = left, Top = top,
                CannotEditBecause = "Pokémon sprites are changed in the Pokémon editor.",
            };
            if (p?.Rgba == null || p.Width < fromX + w || p.Height < fromY + h) { piece.Whynot = "This sprite couldn't be drawn."; return piece; }
            byte[] rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++) Array.Copy(p.Rgba, ((fromY + y) * p.Width + fromX) * 4, rgba, y * w * 4, w * 4);
            piece.Rgba = rgba; piece.Width = w; piece.Height = h;
            return piece;
        }

        private const int SampleMostHealth = 20;

        private void AddGauges(List<Piece> pieces, Options o)
        {
            foreach (BattleGaugeComposer.Kind kind in BattleGaugeComposer.ForDoubleBattle(o.Doubles))
            {
                bool player = kind == BattleGaugeComposer.Kind.PlayerSingle
                           || kind == BattleGaugeComposer.Kind.PlayerNear
                           || kind == BattleGaugeComposer.Kind.PlayerFar;
                string thing = BattleGaugeComposer.GraphicOf(kind);
                Piece piece = new Piece
                {
                    Name = BattleGaugeComposer.NameOf(kind),
                    What = "The name, level and health of one Pokémon.",
                    Archive = DirNames.BattleObj,
                    Drawing = BattleObjects.Find(thing, "Drawing"),
                    Layout = BattleObjects.Find(thing, "As it appears"),
                    Colours = BattleObjects.Find("HpBar.Shared", "Colours"),
                    SharedNote = "Shared by all battles",
                    UsesBarPieces = true,
                };
                try
                {
                    // The writing goes into the bar's own picture, the way a battle does it, so it sits
                    // where the game puts it and nothing paints over the bar's slanted edge.
                    BattleGaugeComposer.Drawn g = BattleGaugeComposer.Build(kind, new BattleGaugeComposer.Showing
                    {
                        Name = o.PokemonName,
                        Level = o.Level,
                        Gender = o.Gender,
                        Status = o.Status,
                        // Only the player's bar shows numbers, and in a double battle only after Start swaps them in.
                        ShowHealthNumbers = player && (!o.Doubles || o.NumbersInsteadOfBar),
                        Health = (int)Math.Round(Math.Clamp(o.HealthLeft, 0, 1) * SampleMostHealth),
                        MostHealth = SampleMostHealth,
                        FillGauges = true,
                    });
                    if (player && !o.Doubles && o.BallCounter > 0)
                    {
                        g = BattleGaugeComposer.BuildBallCounter(o.BallCounter == 2, 30);
                        piece.Name = o.BallCounter == 2 ? "Park Ball counter" : "Safari Ball counter";
                        piece.What = "The balls left, in place of the player's HP bar.";
                        piece.Drawing = BattleObjects.Find("Safari.Counter", "Drawing");
                        piece.Layout = BattleObjects.Find("Safari.Counter", "As it appears");
                        piece.Colours = BattleObjects.Find("Safari.Shared", "Colours");
                        piece.UsesBarPieces = false;
                    }
                    if (g == null && !o.Doubles)
                    {
                        // Games we cannot read the letters of still get the bar itself.
                        BattleGroundRenderer.GroundImage plain = (_ground ??= new BattleGroundRenderer()).BuildGauge(player);
                        if (plain?.Rgba != null)
                            g = new BattleGaugeComposer.Drawn
                            {
                                Rgba = plain.Rgba, Width = plain.Width,
                                Height = plain.Height, Left = plain.Left, Top = plain.Top,
                            };
                    }

                    if (g?.Rgba == null) piece.Whynot = "This HP bar couldn't be drawn.";
                    else { piece.Rgba = g.Rgba; piece.Width = g.Width; piece.Height = g.Height; piece.Left = g.Left; piece.Top = g.Top; }
                }
                catch (Exception ex) { piece.Whynot = "This HP bar couldn't be drawn: " + ex.Message; }
                pieces.Add(piece);
            }
        }

        // The battle message window's text area starts at tile 2,19 and is 27 by 4 tiles. The frame drawn
        // round it adds two tile columns left, three right and a row above and below, which comes to the
        // whole screen width and the bottom 48 pixels.
        public const int MessageTilesWide = 27, MessageTilesHigh = 4;
        public const int MessageTop = 144;

        /// <summary>
        /// The games fill the whole band behind the box, so the frame's see-through edge shows black, not the scene.
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
            Piece piece = new Piece
            {
                Name = "Message box",
                What = "The box battle text appears in. The player picks its frame, one of 20, in Options, and the field uses "
                     + "the same one.",
                Archive = DirNames.WindowFrames,
                Top = MessageTop,
                SharedNote = "Shared by field and battle",
            };
            try
            {
                FieldWindowFrame frame = WindowFrames.Load(windowStyle);
                if (frame == null) piece.Whynot = "The window frames couldn't be read from this ROM.";
                else
                {
                    piece.Rgba = frame.Compose(MessageTilesWide, MessageTilesHigh, out int w, out int h, frame.PaperArgb);
                    piece.Width = w; piece.Height = h;
                    BlackOutTheRest(piece);
                    piece.Drawing = FieldWindowFrame.FirstGraphicEntry + windowStyle;
                    piece.Colours = frame.PaletteEntry;
                }
            }
            catch (Exception ex) { piece.Whynot = "This message box couldn't be drawn: " + ex.Message; }
            return piece;
        }

        /// <summary>One of the touch screen's layers: which arrangement it shows and how the hardware stacks it.</summary>
        private sealed record PanelLayer(string Screen, string Name, string What, int Priority, int BgNumber, bool Blended);

        private const string CommandButtonsScreen = "TouchScreen.Layer.CommandButtons:Screen";

        private static readonly PanelLayer Background = new("TouchScreen.Layer.Background:Screen", "Touch screen background",
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
                    yield return new("TouchScreen.Layer.CommandSilhouette:Screen", "Command silhouette",
                        "The shape behind the command buttons, see-through over the background.", 3, 1, true);
                    yield return new(CommandButtonsScreen, "Command buttons", "Fight, Bag, Pokémon and Run.", 2, 0, false);
                    break;
                case TouchMenu.Fight:
                    yield return new("TouchScreen.Layer.MoveButtons:Screen", "Move buttons",
                        "The four move buttons. The game colours each one after its move's type.", 2, 0, false);
                    break;
                case TouchMenu.Target:
                    yield return new("TouchScreen.Layer.TargetButtons:Screen", "Target buttons", "Which Pokémon a move is aimed at.", 2, 0, false);
                    yield return new("TouchScreen.Layer.TargetOutlines:Screen", "Target outlines",
                        "The outlines over the target buttons, see-through.", 1, 1, true);
                    break;
                case TouchMenu.YesNo:
                    yield return new("TouchScreen.Layer.YesNoButtons:Screen", "Yes and No buttons", "Two buttons, for any yes or no question.", 2, 0, false);
                    break;
                case TouchMenu.Playback:
                    yield return new("TouchScreen.Layer.StopButton:Screen", "Stop button", "Stops a recorded battle.", 2, 0, false);
                    break;
            }
        }

        /// <summary>Which menus this game has.</summary>
        public static IReadOnlyList<TouchMenu> MenusForGame() =>
            RomInfo.GameFamily == GameFamilies.DP
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
        /// from the panel's drawing, not from the unused layer's own, which nothing in the battle code reads.
        /// </summary>
        private void AddTouchPanel(List<Piece> pieces, Options o)
        {
            (byte r, byte g, byte b)[] colours = PanelColours(ResolveBackdrop(o), o.Menu, o.Frontier);
            // Layer 1 is blended over every layer under it. Platinum and HeartGold weigh it 8/16 over 12/16;
            // Diamond and Pearl ask for 27 and 4, and the hardware caps a weight at 16.
            (int own, int below) blend = RomInfo.GameFamily == GameFamilies.DP ? (16, 4) : (8, 12);
            foreach (PanelLayer layer in LayersOf(o.Menu).OrderByDescending(l => l.Priority).ThenByDescending(l => l.BgNumber))
            {
                if (o.NoSilhouette && o.Menu == TouchMenu.Command && layer.Blended) continue;
                Piece piece = AddPanelLayer(pieces, layer.Name, layer.Screen, layer.What, colours, opaque: layer == Background,
                                            o.Frontier, layer.BgNumber == 0 ? o.Pressed : null);
                if (layer.Screen == CommandButtonsScreen) { piece.TakesButtonArt = true; piece.CannotEditBecause = "These buttons share their tiles, so import a PNG to change them."; }
                if (layer.Blended) { piece.BlendOwn = blend.own; piece.BlendBelow = blend.below; }
                if (layer.BgNumber == 0 && o.Menu == TouchMenu.Command) Clear(piece, o.BlankedCommandTiles);
            }
        }

        // pokeplatinum battle_subscreen.c fills these with an empty tile.
        private static void Clear(Piece piece, (int X, int Y, int W, int H)[] tiles)
        {
            if (piece.Rgba == null || tiles == null) return;
            const int Tile = FieldWindowFrame.TileSize;
            foreach ((int x, int y, int w, int h) in tiles)
                for (int py = y * Tile; py < Math.Min(piece.Height, (y + h) * Tile); py++)
                    for (int px = x * Tile; px < Math.Min(piece.Width, (x + w) * Tile); px++)
                        piece.Rgba[(py * piece.Width + px) * 4 + 3] = 0;
        }

        /// <summary>
        /// The colours the touch panel is drawn with. The game loads the whole of the panel's colours, then in
        /// Platinum and HeartGold lays the first 16 colours of the backdrop's own tint over row 0
        /// (pokeheartgold src/battle/battle_input.c sBackgroundPaletteIds), for backdrops 0 to 17 and
        /// nothing for the rest. Diamond and Pearl keep row 0.
        ///
        /// Rows 8 to 11 are placeholders in the file; the Fight menu fills them from the type palettes in
        /// its overlay, one row per move button.
        /// </summary>
        private (byte r, byte g, byte b)[] PanelColours(int backdrop, TouchMenu menu, bool frontier)
        {
            int panel = frontier ? BattlePressFrames.Panel.FrontierPalette : BattleBgNames.Find("TouchScreen.Panel:Colours");
            (byte r, byte g, byte b)[] wide = NitroBgCodec.ReadPalette(GraphicAssets.Unsqueeze(_bg.Get(panel)), out int count);
            (byte r, byte g, byte b)[] all = new (byte r, byte g, byte b)[256];
            for (int i = 0; i < all.Length && i < count; i++) all[i] = wide[i];

            if (RomInfo.GameFamily != GameFamilies.DP && !frontier)
            {
                string tint = backdrop >= 0 && backdrop <= 17 ? $"TouchScreen.Tint.{backdrop}:Colours" : null;
                int sceneAt = tint == null ? -1 : BattleBgNames.Find(tint);
                if (sceneAt >= 0)
                {
                    (byte r, byte g, byte b)[] scene = NitroBgCodec.ReadPalette(GraphicAssets.Unsqueeze(_bg.Get(sceneAt)), out int sceneCount);
                    for (int i = 0; i < 16 && i < sceneCount; i++) all[i] = scene[i];
                }
            }

            if (menu == TouchMenu.Fight)
            {
                for (int slot = 0; slot < SampleMoveTypes.Length; slot++)
                {
                    ushort[] row = BattleUiTables.MoveButtonPalette(SampleMoveTypes[slot]);
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
                                    (byte r, byte g, byte b)[] colours, bool opaque, bool frontier, (BattlePressFrames.Rect Rect, int Page)? pressed)
        {
            Piece piece = new Piece
            {
                Name = name, What = what, Touch = true, Archive = DirNames.BattleBg,
                CannotEditBecause = "All the touch screen layers share one sheet of tiles, so this layer can't be painted on its own.",
            };
            try
            {
                int scr = BattleBgNames.Find(screenEntry);
                int chr = BattleBgNames.Find("TouchScreen.Panel:Drawing");
                if (frontier)
                {
                    chr = BattlePressFrames.Panel.FrontierSheet;
                    if (screenEntry == Background.Screen) scr = BattlePressFrames.Panel.FrontierBackground;
                }
                int pal = BattleBgNames.Find("TouchScreen.Panel:Colours");
                piece.Arrangement = scr; piece.Drawing = chr; piece.Colours = pal;
                if (scr < 0 || chr < 0 || pal < 0)
                {
                    piece.Whynot = "This game doesn't name the touch screen panel files.";
                }
                else
                {
                    // Colour zero is a real colour on the bottom layer, not a hole: the panel's own green
                    // is index 0. On the layers above it, colour zero is what lets the one below show.
                    byte[] screen = GraphicAssets.Unsqueeze(_bg.Get(scr));
                    if (pressed != null) screen = Press(screen, pressed.Value.Rect, pressed.Value.Page);
                    NitroBgCodec.BgImage bg = NitroBgCodec.Composite(GraphicAssets.Unsqueeze(_bg.Get(chr)),
                                                    colours, 256, screen, transparentZero: !opaque);
                    if (bg?.Rgba == null) piece.Whynot = "This panel couldn't be put together.";
                    else { piece.Rgba = bg.Rgba; piece.Width = bg.Width; piece.Height = bg.Height; }
                }
            }
            catch (Exception ex) { piece.Whynot = "This panel couldn't be drawn: " + ex.Message; }
            pieces.Add(piece);
            return piece;
        }

        private static byte[] Press(byte[] screen, BattlePressFrames.Rect rect, int page)
        {
            byte[] outp = (byte[])screen.Clone();
            ushort[][] pages = BattlePressFrames.ReadPages(outp);
            if (page >= pages.Length) return outp;
            (int width, _, int mapAt) = NitroBgCodec.ReadScreenHeader(outp);
            int entryBytes = NitroBgCodec.EntryBytes(outp), cols = width / 8;
            for (int y = rect.Top; y <= rect.Bottom; y++)
                for (int x = rect.Left; x <= rect.Right; x++)
                    NitroBgCodec.PutEntry(outp, mapAt, NitroBgCodec.SquareIndex(cols, x, y), entryBytes, pages[page][y * BattlePressFrames.Columns + x]);
            return outp;
        }
    }

    /// <summary>The touch screen menus the editor can show.</summary>
    public enum TouchMenu { Background, Command, Fight, Target, YesNo, Playback }
}
