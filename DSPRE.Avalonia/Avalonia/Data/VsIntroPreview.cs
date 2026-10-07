using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// A special trainer intro put together from its archive files, still or frame by frame. Positions and
    /// timings follow the games' code where they are known; the rest is a close approximation, and the field
    /// behind the intro is a plain backdrop.
    /// </summary>
    public sealed partial class VsIntroPreview
    {
        public enum Layout { Gym, League, Executive, Pieces, Balls, Special, Frontier }

        /// <summary>What to draw. Member arrays are palette, tiles, cells (and animation); null leaves a part out.</summary>
        public sealed class Scene
        {
            public Layout Kind;
            public int[] Face, Banner, Vs, PlayerFace;
            /// <summary>Colours to use instead of the face's own palette file, as the field intro does in Platinum.</summary>
            public ushort[] FaceColours, PlayerColours;
            /// <summary>League frame: tiles, cells, and the palette file the record picks.</summary>
            public int[] Frame;
            public int FramePalette = -1, NamePalette = -1, EndX = 214, ClashFrames = 32;
            /// <summary>HGSS Rocket executive backdrop: palette, tiles, then the streaks, outlined R and filled R screens.</summary>
            public int[] Backdrop;
            /// <summary>
            /// For intros the preview doesn't animate (the terrain Poké Balls, Red, Team Rocket, Kimono Girl): the sprite
            /// sets they use, laid out side by side, and a background screen (palette, tiles, screen) behind them.
            /// </summary>
            public List<int[]> Pieces;
            /// <summary>The ordinary trainer intros: which of the six, and the large and small Poké Ball sets.</summary>
            public BallVariant Variant;
            public int[] Large, Small;
            /// <summary>The Team Rocket, Red and Kimono Girl intros: which, and the emblem set they draw.</summary>
            public SpecialVariant Special;
            public int[] Emblem;
            public int[] Screen;
            /// <summary>The ROM's emblem flights and block wipe column order, when read; null keeps the shipped values.</summary>
            public IReadOnlyList<DSPRE.ROMFiles.VsIntroMotion.Flight> Flights;
            public int[] BlockOrder;
            /// <summary>The class's intro timings by VsIntroTimingAddon field, or null for the game's own.</summary>
            public int[] Timings;
            public string Name;

            internal int Timing(DSPRE.ROMFiles.VsIntroTimingAddon.Field f) =>
                Timings != null && (int)f < Timings.Length && Timings[(int)f] > 0 ? Timings[(int)f] : DSPRE.ROMFiles.VsIntroTimingAddon.GameValues[(int)f];
        }

        private readonly Func<int, byte[]> _member;
        private readonly FieldFont _font;
        private static readonly ushort Field = Rgb(6, 8, 10), White = Rgb(31, 31, 31);

        public VsIntroPreview(Func<int, byte[]> member, FieldFont font)
        {
            _member = i => { try { return i < 0 ? null : GraphicAssets.Unsqueeze(member(i)); } catch { return null; } };
            _font = font;
        }

        private static ushort Rgb(int r, int g, int b) => (ushort)(r | (g << 5) | (b << 10));

        /// <summary>How many frames the animation runs.</summary>
        public static int Length(Scene s) => s.Kind switch
        {
            Layout.Pieces => 1,
            Layout.Balls => BallLength(s.Variant),
            Layout.Special => SpecialLength(s),
            Layout.Gym => GymTimes(s).End,
            Layout.League => LeagueTimes(s).End,
            Layout.Frontier => FrontierTimes(s).End,
            _ => ExecTimes(s).End,
        };

        /// <summary>The intro at <paramref name="frame"/>, or its settled look for a negative frame, as 256 by 192 RGBA.</summary>
        public byte[] Draw(Scene s, int frame)
        {
            return s.Kind switch
            {
                Layout.Gym => Gym(s, frame),
                Layout.League => League(s, frame),
                Layout.Pieces => Pieces(s),
                Layout.Balls => Balls(s, frame),
                Layout.Special => Special(s, frame),
                Layout.Frontier => Frontier(s, frame),
                _ => Executive(s, frame),
            };
        }

        // ── Pieces ────────────────────────────────────────────────────────────────────────────────

        private byte[] Plain()
        {
            byte[] rgba = new byte[DsBgScreen.Width * DsBgScreen.Height * 4];
            for (int i = 0; i < rgba.Length; i += 4) Put(rgba, i, Field);
            return rgba;
        }

        private static void Put(byte[] rgba, int at, ushort c)
        {
            rgba[at] = (byte)((c & 31) << 3); rgba[at + 1] = (byte)(((c >> 5) & 31) << 3); rgba[at + 2] = (byte)(((c >> 10) & 31) << 3); rgba[at + 3] = 255;
        }

        /// <summary>A background from palette, tiles and screen files, with the field showing through colour 0.</summary>
        private byte[] Background(int nclr, int ncgr, params int[] screens)
        {
            byte[] tiles = _member(ncgr), pal = _member(nclr);
            if (tiles == null || pal == null) return null;
            DsBgScreen screen = new DsBgScreen();
            ushort[] colours = DsBgScreen.ReadColours(pal);
            int rows = Math.Max(1, colours.Length / 16);
            for (int slot = 0; slot < 16; slot++) screen.SetPalette(slot, DsBgScreen.Row(colours, rows == 1 ? 0 : slot % rows));
            ushort[] backdrop = DsBgScreen.Row(colours, 0);
            backdrop[0] = Field;
            screen.SetPalette(0, backdrop);
            screen.LoadTiles(0, tiles);
            int bg = 0;
            foreach (int nscr in screens)
            {
                (int w, ushort[] entries) = DsBgScreen.ReadMap(_member(nscr));
                if (w <= 0) continue;
                screen.InitLayer(bg, bg, 0);
                screen.LoadMap(bg, entries, w);
                bg++;
            }
            return bg == 0 ? null : screen.Render();
        }

        private (DsBgScreen.Oam[] Cell, byte[] Tiles, Func<int, ushort[]> Colours)? Sprite(int[] m, ushort[] colours, bool rows = false)
        {
            if (m == null || m.Length < 3) return null;
            byte[] tiles = DsBgScreen.ReadCharacters(_member(m[1]));
            List<DsBgScreen.Oam[]> cells = DsBgScreen.ReadCells(_member(m[2]));
            if (tiles.Length == 0 || cells.Count == 0) return null;
            ushort[] all = colours ?? DsBgScreen.ReadColours(_member(m[0]));
            return (cells[0], tiles, p => DsBgScreen.Row(all, rows ? p % Math.Max(1, all.Length / 16) : 0));
        }

        private static void DrawSprite((DsBgScreen.Oam[] Cell, byte[] Tiles, Func<int, ushort[]> Colours)? s, byte[] rgba, int x, int y, double scale = 1)
        {
            if (s == null) return;
            DsBgScreen.DrawCellTurned(rgba, s.Value.Cell, s.Value.Tiles, s.Value.Colours, x, y, 0, scale, scale);
        }

        private void Name(Scene s, byte[] rgba, int x, int y)
        {
            if (string.IsNullOrEmpty(s.Name) || _font == null) return;
            ushort[] c = DsBgScreen.Row(DsBgScreen.ReadColours(_member(s.NamePalette)), 0);
            ushort letter = c[1] != 0 ? c[1] : White, shadow = c[2] != 0 ? c[2] : Rgb(10, 10, 10);
            DsBgScreen.DrawText(rgba, _font, s.Name, x, y, letter, shadow);
        }

        private static void Blend(byte[] rgba, double toWhite)
        {
            if (toWhite <= 0) return;
            toWhite = Math.Min(1, toWhite);
            for (int i = 0; i < rgba.Length; i += 4)
                for (int k = 0; k < 3; k++) rgba[i + k] = (byte)(rgba[i + k] + (255 - rgba[i + k]) * toWhite);
        }

        private static void Scroll(byte[] from, byte[] to, int dx)
        {
            int w = DsBgScreen.Width, h = DsBgScreen.Height;
            dx = ((dx % w) + w) % w;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    Array.Copy(from, (y * w + (x + dx) % w) * 4, to, (y * w + x) * 4, 4);
        }

        // ── Shared steps of the cut-in intros ─────────────────────────────────────────────────────

        // As recorded in game: two ticks out, three held, two back; a second flash ten ticks after the first.
        private const int FlashPeriod = 10;
        private static readonly double[] FlashSteps = { 16 / 3.0, 32 / 3.0, 16, 16, 16, 32 / 3.0, 16 / 3.0 };
        private static double CutinFlash(int t, int count, bool white) =>
            t < 0 || t >= FlashPeriod * count || t % FlashPeriod >= FlashSteps.Length ? 0 : (white ? 1 : -1) * FlashSteps[t % FlashPeriod];

        private static int FlashesEnd(int count) => FlashPeriod * (count - 1) + FlashSteps.Length + 1;

        // The portrait's colours change while the screen is white.
        private static double RevealFlash(int k) => k >= 0 && k < FlashSteps.Length ? FlashSteps[k] : 0;

        // The class's own flash count when the timing table has one; the game flashes once.
        private static int Flashes(Scene s) =>
            s.Timings != null && s.Timings.Length > 0 && s.Timings[(int)DSPRE.ROMFiles.VsIntroTimingAddon.Field.FlashCount] > 0
                ? s.Timings[(int)DSPRE.ROMFiles.VsIntroTimingAddon.Field.FlashCount] : 1;

        // Before the reveal the game fades the portrait's palette 14 sixteenths towards black.
        private ushort[] Silhouette(int[] m, ushort[] colours)
        {
            ushort[] all = colours ?? (m != null && m.Length > 0 ? DsBgScreen.ReadColours(_member(m[0])) : null);
            if (all == null) return null;
            ushort[] dark = new ushort[all.Length];
            for (int i = 0; i < all.Length; i++)
                dark[i] = Rgb((all[i] & 31) * 2 / 16, ((all[i] >> 5) & 31) * 2 / 16, ((all[i] >> 10) & 31) * 2 / 16);
            return dark;
        }

        private static void ToWhite(byte[] rgba, int t, int n) { if (t >= 0) Blend(rgba, Math.Min(1, (t + 1) / (double)n)); }

        private static int VsLength(Scene s) =>
            3 * s.Timing(DSPRE.ROMFiles.VsIntroTimingAddon.Field.VsGap) + s.Timing(DSPRE.ROMFiles.VsIntroTimingAddon.Field.VsShrink) + 1;

        // Three burst copies play the mark's second sequence while shrinking from double size, three frames apart,
        // and hide once full size; the fourth is the letters, playing the first sequence at full size.
        private void VsMark(Scene s, int[] vs, byte[] rgba, int x, int y, int frame, int start)
        {
            int shrink = s.Timing(DSPRE.ROMFiles.VsIntroTimingAddon.Field.VsShrink), gap = s.Timing(DSPRE.ROMFiles.VsIntroTimingAddon.Field.VsGap);
            if (vs == null) return;
            if (vs.Length < 4) { VsMarkStill(Sprite(vs, null), rgba, x, y, frame, start); return; }
            if (frame < 0) { DrawSprite(Sequence(vs, 0, 0, true), rgba, x, y); return; }
            for (int k = 0; k < 4; k++)
            {
                int t = frame - start - gap * k;
                if (t < 0) continue;
                if (k == 3) { DrawSprite(Sequence(vs, 0, t, true), rgba, x, y); continue; }
                if (t >= shrink) continue;
                DrawSprite(Sequence(vs, 1, t, true), rgba, x, y, 2 - t / (double)shrink);
            }
        }

        // A mark without an animation file: four copies of its first drawing shrink one after another.
        private static void VsMarkStill((DsBgScreen.Oam[] Cell, byte[] Tiles, Func<int, ushort[]> Colours)? vs, byte[] rgba, int x, int y, int frame, int start)
        {
            if (frame < 0) { DrawSprite(vs, rgba, x, y); return; }
            for (int k = 0; k < 4; k++)
            {
                int t = frame - start - 3 * k;
                if (t < 0) continue;
                DrawSprite(vs, rgba, x, y, 2 - Math.Min(1, t / 6.0));
            }
        }

        private byte[] Pieces(Scene s)
        {
            byte[] rgba = (s.Screen != null && s.Screen.Length >= 3 ? Background(s.Screen[0], s.Screen[1], s.Screen[2]) : null) ?? Plain();
            int count = s.Pieces?.Count ?? 0;
            for (int i = 0; i < count; i++)
                DrawSprite(Sprite(s.Pieces[i], null, rows: true), rgba, DsBgScreen.Width * (2 * i + 1) / (2 * count), DsBgScreen.Height / 2);
            return rgba;
        }

        // ── Gym leaders and the HGSS rival ────────────────────────────────────────────────────────

        private static (int Open, int Vs, int Slide, int FlashOut, int Reveal, int Back, int Wipe, int End) GymTimes(Scene s)
        {
            // Calibrated against a HeartGold recording; each state change costs the encounter task a tick.
            int open = FlashesEnd(Flashes(s)) + 4;
            int vs = open + 6 + 12;
            int slide = vs + VsLength(s);
            int flashOut = slide + s.Timing(DSPRE.ROMFiles.VsIntroTimingAddon.Field.GymSlide) + 14;
            int reveal = flashOut + 3, back = flashOut + 7, wipe = back + 28;
            return (open, vs, slide, flashOut, reveal, back, wipe, wipe + 15);
        }

        private byte[] Gym(Scene s, int frame)
        {
            (int Open, int Vs, int Slide, int FlashOut, int Reveal, int Back, int Wipe, int End) t = GymTimes(s);
            int f = frame < 0 ? t.Back : frame;
            byte[] rgba = FieldGrid();
            if (f >= t.Reveal) Brightness(rgba, -14);

            (byte[] Rgba, int Width, int Height)? banner = WideScreen(s.Banner);
            if (banner != null && f >= t.Open)
            {
                int k = f - t.Open;
                ZigzagLayer(rgba, banner.Value, 30 * k, 255 - 255 * Math.Min(k, 6) / 6);
            }

            VsMark(s, s.Vs, rgba, 72, 74, f, t.Vs);
            if (f >= t.Slide)
            {
                int slide = s.Timing(DSPRE.ROMFiles.VsIntroTimingAddon.Field.GymSlide);
                int x = f >= t.Slide + slide ? s.EndX : (int)Math.Round(AddMove(272, s.EndX, -64, slide, f - t.Slide));
                DrawSprite(Sprite(s.Face, f < t.Reveal ? Silhouette(s.Face, s.FaceColours) : s.FaceColours), rgba, x, 66);
            }
            if (f >= t.Reveal) Name(s, rgba, s.EndX - 92, 81);

            if (frame < 0) return rgba;
            Brightness(rgba, CutinFlash(f, Flashes(s), true));
            Brightness(rgba, RevealFlash(f - t.FlashOut));
            if (f >= t.Wipe) ToWhite(rgba, f - t.Wipe, 15);
            return rgba;
        }

        private static void ZigzagLayer(byte[] rgba, (byte[] Rgba, int Width, int Height) layer, int scrollX, int edge)
        {
            int w = DsBgScreen.Width, h = DsBgScreen.Height;
            for (int y = 0; y < h && y < layer.Height; y++)
            {
                int step = (y % 8) * 16 / 8, lean = (y / 8) % 2 == 0 ? step : 16 - step;
                int from = Math.Max(0, edge - lean);
                for (int x = from; x < w; x++)
                {
                    int sx = ((x + scrollX) % layer.Width + layer.Width) % layer.Width, at = (y * layer.Width + sx) * 4;
                    if (layer.Rgba[at + 3] == 0) continue;
                    Array.Copy(layer.Rgba, at, rgba, (y * w + x) * 4, 4);
                }
            }
        }

        // ── Elite Four and Champion ───────────────────────────────────────────────────────────────

        // The fire particles are 3D and not drawn here; this is how long the game waits for them.
        private const int LeagueFireTicks = 33;
        private static readonly double[] LeagueReveal = { 16 / 3.0, 32 / 3.0, 16, 16, 16, 16 * 5 / 6.0, 16 * 4 / 6.0, 16 * 3 / 6.0, 16 * 2 / 6.0, 16 / 6.0 };

        private static (int In, int Vs, int FlashOut, int Reveal, int Shake, int Leave, int End) LeagueTimes(Scene s)
        {
            int start = FlashesEnd(Flashes(s)) + 4;
            int vs = start + 3, flashOut = vs + LeagueFireTicks, reveal = flashOut + 3, shake = flashOut + LeagueReveal.Length + 8;
            int leave = shake + Math.Clamp(s.ClashFrames, 0, 255);
            return (start, vs, flashOut, reveal, shake, leave, leave + 16);
        }

        private byte[] League(Scene s, int frame)
        {
            (int In, int Vs, int FlashOut, int Reveal, int Shake, int Leave, int End) t = LeagueTimes(s);
            int f = frame < 0 ? t.Shake : frame;
            // The 3D layer replaces the field after the flash, so the scene is all but black behind.
            byte[] rgba = FieldGrid();
            if (f >= FlashesEnd(Flashes(s))) Brightness(rgba, -15);

            int k = f - t.In;
            double px = AddMove(-128, 56, 80, 6, k), ox = AddMove(384, 200, -80, 6, k), py = 92, oy = 92;
            if (f >= t.Shake && f < t.Leave)
            {
                int wait = f - t.Shake + 1;
                double amp = AddMove(0, -2, 0, Math.Max(1, t.Leave - t.Shake), wait);
                int sign = (wait / 2) % 2 == 0 ? 1 : -1;
                px += sign * amp; py += sign * amp; ox -= sign * amp; oy -= sign * amp;
            }
            if (f >= t.Leave)
            {
                double d = AddMove(0, 192, 24, 16, f - t.Leave + 1);
                px -= d; py -= d; ox += d; oy += d;
            }

            if (f >= t.In)
            {
                bool dark = f < t.Reveal;
                ushort[] frameColours = DsBgScreen.ReadColours(_member(s.FramePalette));
                (DsBgScreen.Oam[] Cell, byte[] Tiles, Func<int, ushort[]> Colours)? frameSprite =
                    s.Frame != null ? Sprite(new[] { -1, s.Frame[0], s.Frame[1] }, frameColours, rows: true) : null;
                DrawSprite(frameSprite, rgba, (int)Math.Round(px + 16), (int)Math.Round(py + 4));
                DrawSprite(frameSprite, rgba, (int)Math.Round(ox - 16), (int)Math.Round(oy + 4));
                DrawSprite(Sprite(s.PlayerFace, dark ? Silhouette(s.PlayerFace, s.PlayerColours) : s.PlayerColours), rgba, (int)Math.Round(px), (int)Math.Round(py));
                DrawSprite(Sprite(s.Face, dark ? Silhouette(s.Face, s.FaceColours) : s.FaceColours), rgba, (int)Math.Round(ox), (int)Math.Round(oy));
            }
            VsMark(s, s.Vs, rgba, 128, 96, f, t.Vs);
            if (f >= t.Vs && f < t.Leave) Name(s, rgba, 168, 105);

            if (frame < 0) return rgba;
            Brightness(rgba, CutinFlash(f, Flashes(s), true));
            int r = f - t.FlashOut;
            if (r >= 0 && r < LeagueReveal.Length) Brightness(rgba, LeagueReveal[r]);
            if (f >= t.Leave) ToWhite(rgba, f - t.Leave, 15);
            return rgba;
        }

        // ── HGSS Rocket executives ────────────────────────────────────────────────────────────────

        // The streaks start while the flash is still running, as recorded.
        private const int ExecFlashAt = 4, ExecScroll = 12, ExecMask = 4, ExecMaskWait = 13, ExecIn = 5, ExecFlush = 8, ExecHold = 19, ExecOut = 6;

        private static (int Scroll, int Mask, int Full, int In, int Flush, int Hold, int Out, int End) ExecTimes(Scene s)
        {
            int scroll = 1, mask = scroll + ExecScroll, full = mask + ExecMask, cut = full + ExecMaskWait;
            int flush = cut + ExecIn, hold = flush + ExecFlush, @out = hold + ExecHold;
            return (scroll, mask, full, cut, flush, hold, @out, @out + ExecOut + 4);
        }

        private static double ExecX(int k)
        {
            double x = 320;
            for (int i = 0; i <= k; i++)
            {
                x += (128 - x) * 2 / 3;
                if (x <= 130) return 128;
            }
            return x;
        }

        private byte[] Executive(Scene s, int frame)
        {
            (int Scroll, int Mask, int Full, int In, int Flush, int Hold, int Out, int End) t = ExecTimes(s);
            int f = frame < 0 ? t.Hold : frame;
            int[] b = s.Backdrop;
            (byte[] Rgba, int Width, int Height)? streaks = b != null && b.Length >= 3 ? WideScreen(new[] { b[0], b[1], b[2] }) : null;
            (byte[] Rgba, int Width, int Height)? outline = b != null && b.Length >= 4 ? WideScreen(new[] { b[0], b[1], b[3] }) : null;
            (byte[] Rgba, int Width, int Height)? filled = b != null && b.Length >= 5 ? WideScreen(new[] { b[0], b[1], b[4] }) : null;

            byte[] rgba = f >= t.Flush ? Fill(Rgb(5, 5, 5)) : FieldGrid();
            if (f >= t.Scroll && f < t.Full && streaks != null) Layer(rgba, streaks.Value, 24 * Math.Min(f - t.Scroll + 1, ExecScroll + ExecMask));
            if (f >= t.Mask && f < t.Out && outline != null)
            {
                int half = f >= t.Full ? 96 : (int)AddMove(0, 96, 4, 4, f - t.Mask + 1);
                byte[] band = Fill(Rgb(5, 5, 5));
                Layer(band, outline.Value, 0);
                for (int y = 96 - half; y < 96 + half; y++)
                    if (y >= 0 && y < DsBgScreen.Height) Array.Copy(band, y * DsBgScreen.Width * 4, rgba, y * DsBgScreen.Width * 4, DsBgScreen.Width * 4);
            }

            if (f >= t.In)
            {
                int k = f - t.In;
                if (filled != null) Over(rgba, filled.Value, Math.Min(16, 4 * (k + 1)) / 16.0);
                double x = f >= t.Out ? AddMove(128, -96, 2, 8, f - t.Out + 1) : ExecX(k);
                DrawSprite(Sprite(s.Face, s.FaceColours), rgba, (int)Math.Round(x), 128);
                if (k >= 1 && x + 8 > 0) Name(s, rgba, (int)Math.Round(x) + 8, 161);
            }

            if (frame < 0) return rgba;
            Brightness(rgba, CutinFlash(f - ExecFlashAt, Flashes(s), true));
            if (f >= t.Flush && f < t.Hold) Brightness(rgba, 16 - 16.0 * (f - t.Flush) / ExecFlush);
            if (f >= t.Out + ExecOut) ToWhite(rgba, f - t.Out - ExecOut, 4);
            return rgba;
        }

        private static byte[] Fill(ushort c)
        {
            byte[] rgba = new byte[DsBgScreen.Width * DsBgScreen.Height * 4];
            for (int i = 0; i < rgba.Length; i += 4) Put(rgba, i, c);
            return rgba;
        }

        private static void Over(byte[] rgba, (byte[] Rgba, int Width, int Height) layer, double alpha)
        {
            int w = DsBgScreen.Width, h = DsBgScreen.Height;
            for (int y = 0; y < h && y < layer.Height; y++)
                for (int x = 0; x < w && x < layer.Width; x++)
                {
                    int from = (y * layer.Width + x) * 4, to = (y * w + x) * 4;
                    if (layer.Rgba[from + 3] == 0) continue;
                    for (int c = 0; c < 3; c++) rgba[to + c] = (byte)(rgba[to + c] * (1 - alpha) + layer.Rgba[from + c] * alpha);
                }
        }

        // ── Frontier Brains ───────────────────────────────────────────────────────────────────────

        private static (int Open, int Vs, int Slide, int FlashOut, int Reveal, int Back, int Wipe, int End) FrontierTimes(Scene s)
        {
            // Calibrated against a HeartGold recording of a style 13 battle.
            int open = 11, vs = open + 18, slide = vs + VsLength(s), flashOut = slide + 16;
            int reveal = flashOut + 4, back = flashOut + 8, wipe = back + 30;
            return (open, vs, slide, flashOut, reveal, back, wipe, wipe + 14);
        }

        private static readonly double[] FrontierFade = { 16 / 3.0, 32 / 3.0, 16, 16, 16, 16, 32 / 3.0, 16 / 3.0 };
        private static double FrontierFlash(int k) => k >= 0 && k < FrontierFade.Length ? FrontierFade[k] : 0;

        private byte[] Frontier(Scene s, int frame)
        {
            (int Open, int Vs, int Slide, int FlashOut, int Reveal, int Back, int Wipe, int End) t = FrontierTimes(s);
            int f = frame < 0 ? t.Back : frame;
            byte[] rgba = FieldGrid();
            if (f >= t.Reveal) Brightness(rgba, -14);

            (byte[] Rgba, int Width, int Height)? zigzag = WideScreen(s.Banner, f % 8);
            if (zigzag != null && f >= t.Open)
            {
                int half = Math.Min(34, 8 * (f - t.Open + 1));
                byte[] band = Plain();
                Layer(band, zigzag.Value, 0);
                for (int y = 80 - half; y < 80 + half; y++)
                    Array.Copy(band, y * DsBgScreen.Width * 4, rgba, y * DsBgScreen.Width * 4, DsBgScreen.Width * 4);
            }

            VsMark(s, s.Vs, rgba, 72, 82, f, t.Vs);
            if (f >= t.Slide)
            {
                int x = Math.Max(208, 256 - 15 * (f - t.Slide));
                DrawSprite(Sprite(s.Face, f < t.Reveal ? Silhouette(s.Face, s.FaceColours) : s.FaceColours), rgba, x, 80);
            }
            if (f >= t.Reveal) Name(s, rgba, 116, 89);

            if (frame < 0) return rgba;
            Brightness(rgba, FrontierFlash(f));
            Brightness(rgba, FrontierFlash(f - t.FlashOut));
            if (f >= t.Wipe) ToWhite(rgba, f - t.Wipe, 15);
            return rgba;
        }
    }
}
