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
        public enum Layout { Gym, League, Executive, Pieces, Balls, Special }

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
            /// <summary>HGSS Rocket executive backdrop: palette, tiles, then the two screens.</summary>
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
            Layout.Gym => 77 + GymExtra(s),
            Layout.League => 34 + Math.Clamp(s.ClashFrames, 0, 255),
            _ => 70,
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

        // Frames the gym intro gains when its slide or VS timings are longer than the game's.
        private static int GymExtra(Scene s) => Math.Max(0, s.Timing(DSPRE.ROMFiles.VsIntroTimingAddon.Field.GymSlide) - 4)
            + Math.Max(0, s.Timing(DSPRE.ROMFiles.VsIntroTimingAddon.Field.VsShrink) - 6)
            + 3 * Math.Max(0, s.Timing(DSPRE.ROMFiles.VsIntroTimingAddon.Field.VsGap) - 3);

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

        private static double Ease(double t) => 1 - (1 - t) * (1 - t);

        // ── Gym leaders and the HGSS rival ────────────────────────────────────────────────────────

        private byte[] Pieces(Scene s)
        {
            byte[] rgba = (s.Screen != null && s.Screen.Length >= 3 ? Background(s.Screen[0], s.Screen[1], s.Screen[2]) : null) ?? Plain();
            int count = s.Pieces?.Count ?? 0;
            for (int i = 0; i < count; i++)
                DrawSprite(Sprite(s.Pieces[i], null, rows: true), rgba, DsBgScreen.Width * (2 * i + 1) / (2 * count), DsBgScreen.Height / 2);
            return rgba;
        }

        private byte[] Gym(Scene s, int frame)
        {
            byte[] banner = s.Banner != null ? Background(s.Banner[0], s.Banner[1], s.Banner[2]) : null;
            byte[] rgba = Plain();
            if (banner != null)
            {
                if (frame < 0) Array.Copy(banner, rgba, rgba.Length);
                else
                {
                    Scroll(banner, rgba, 30 * frame);
                    if (frame < 6) Zigzag(rgba, (frame + 1) / 6.0);
                }
            }

            if (frame < 0 || frame >= 6)
            {
                int slide = s.Timing(DSPRE.ROMFiles.VsIntroTimingAddon.Field.GymSlide);
                int x = frame < 0 || frame >= 6 + slide ? s.EndX : (int)(272 + (s.EndX - 272) * Ease((frame - 6) / (double)slide));
                DrawSprite(Sprite(s.Face, s.FaceColours), rgba, x, 66);
            }
            VsMark(s, s.Vs, rgba, 72, 74, frame, 6 + s.Timing(DSPRE.ROMFiles.VsIntroTimingAddon.Field.GymSlide));
            // Measured in game: the name starts 122 pixels in, to the right of the VS mark.
            Name(s, rgba, 122, 81);

            if (frame >= 30 && frame < 36) Blend(rgba, frame < 33 ? (frame - 29) / 3.0 : (36 - frame) / 3.0);
            if (frame >= 62) Blend(rgba, (frame - 61) / 15.0);
            return rgba;
        }

        // The banner opens in eight pixel bands, alternate bands from opposite sides.
        private static void Zigzag(byte[] rgba, double shown)
        {
            int w = DsBgScreen.Width, h = DsBgScreen.Height, edge = (int)(w * shown);
            for (int y = 0; y < h; y++)
            {
                bool fromLeft = (y / 8) % 2 == 0;
                for (int x = 0; x < w; x++)
                    if (fromLeft ? x >= edge : x < w - edge) Put(rgba, (y * w + x) * 4, Field);
            }
        }

        // ── Elite Four and Champion ───────────────────────────────────────────────────────────────

        private byte[] League(Scene s, int frame)
        {
            byte[] rgba = Plain();
            const int ox = 184, oy = 64, px = 72, py = 128;
            int clash = Math.Clamp(s.ClashFrames, 0, 255), leave = 26 + clash;
            double t = frame < 0 ? 1 : Math.Min(1, Ease(frame / 6.0));
            int shake = frame >= 26 && frame < leave ? ((frame & 1) == 0 ? 2 : -2) : 0;
            int away = frame >= leave ? (frame - leave) * 24 : 0;

            ushort[] frameColours = DsBgScreen.ReadColours(_member(s.FramePalette));
            (DsBgScreen.Oam[] Cell, byte[] Tiles, Func<int, ushort[]> Colours)? frameSprite = s.Frame != null ? Sprite(new[] { -1, s.Frame[0], s.Frame[1] }, frameColours, rows: true) : null;
            int oppX = (int)(304 + (ox - 304) * t) + shake + away, oppY = oy - away;
            int plX = (int)(-48 + (px + 48) * t) - shake - away, plY = py + away;

            DrawSprite(frameSprite, rgba, oppX, oppY);
            DrawSprite(frameSprite, rgba, plX, plY);
            DrawSprite(Sprite(s.Face, s.FaceColours), rgba, oppX, oppY);
            DrawSprite(Sprite(s.PlayerFace, s.PlayerColours), rgba, plX, plY);
            if (frame < 0 || frame < leave) VsMark(s, s.Vs, rgba, 128, 96, frame, 7);
            if (frame < 0 || frame < leave) Name(s, rgba, 170, 105);

            if (frame >= 20 && frame < 26) Blend(rgba, frame < 23 ? (frame - 19) / 3.0 : (26 - frame) / 3.0);
            if (frame >= leave) Blend(rgba, (frame - leave + 1) / 8.0);
            return rgba;
        }

        // ── HGSS Rocket executives ────────────────────────────────────────────────────────────────

        private byte[] Executive(Scene s, int frame)
        {
            byte[] rgba = Plain();
            if (s.Backdrop != null && s.Backdrop.Length >= 4)
            {
                byte[] bg = Background(s.Backdrop[0], s.Backdrop[1], s.Backdrop[2], s.Backdrop[3]);
                if (bg != null)
                {
                    if (frame < 0) Array.Copy(bg, rgba, rgba.Length);
                    else Scroll(bg, rgba, 8 * frame);
                }
            }
            int x = frame < 0 || frame >= 10 ? 176 : (int)(304 + (176 - 304) * Ease(frame / 10.0));
            DrawSprite(Sprite(s.Face, null), rgba, x, 96);
            Name(s, rgba, 8, 161);
            if (frame >= 24 && frame < 30) Blend(rgba, frame < 27 ? (frame - 23) / 3.0 : (30 - frame) / 3.0);
            if (frame >= 55) Blend(rgba, (frame - 54) / 15.0);
            return rgba;
        }
    }
}
