using System;
using System.Collections.Generic;
using SpriteParts = (DSPRE.Avalonia.Data.DsBgScreen.Oam[] Cell, byte[] Tiles, System.Func<int, ushort[]> Colours);

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The ordinary trainer intros: a flash, Poké Balls that scale, fall or sweep across, and a wipe of the field to
    /// black. The six variants and their frame counts follow the HGSS encounter effects (grass, water and cave, each
    /// early and late). The field itself isn't drawn; a plain grid stands in for it so the wipes can be seen.
    /// </summary>
    public sealed partial class VsIntroPreview
    {
        public enum BallVariant { GrassEarly, GrassLate, WaterEarly, WaterLate, CaveEarly, CaveLate }

        public static readonly string[] BallVariantNames =
            { "Grass, early", "Grass, late", "Water, early", "Water, late", "Cave, early", "Cave, late" };

        private const int FlashFrames = 12, FadeFrames = 8;

        private static int BallLength(BallVariant v) => v switch
        {
            BallVariant.GrassEarly => FlashFrames + 10 + 6 + FadeFrames,
            BallVariant.GrassLate => FlashFrames + 8 + 8 + FadeFrames,
            BallVariant.WaterEarly => FlashFrames + 8 + 8 + FadeFrames,
            BallVariant.WaterLate => FlashFrames + 6 + 4 + 2 + 6 + FadeFrames,
            BallVariant.CaveEarly => FlashFrames + 12 + 8 + FadeFrames,
            _ => FlashFrames + 3 + 5 + 48 + FadeFrames,
        };

        /// <summary>A sprite set's drawing for a sequence, <paramref name="ticks"/> frames into its animation.</summary>
        private SpriteParts? Sequence(int[] m, int sequence, int ticks = 0, bool firstRow = false)
        {
            if (m == null || m.Length < 4) return null;
            byte[] tiles = DsBgScreen.ReadCharacters(_member(m[1]));
            List<DsBgScreen.Oam[]> cells = DsBgScreen.ReadCells(_member(m[2]));
            if (tiles.Length == 0 || cells.Count == 0) return null;
            int cell = 0;
            try
            {
                byte[] nanr = _member(m[3]);
                if (nanr != null)
                {
                    NanrFile file = NanrFile.Read(nanr);
                    cell = file.CellOf(sequence, file.FrameAt(sequence, ticks));
                }
            }
            catch { }
            if (cell < 0 || cell >= cells.Count) cell = 0;
            ushort[] all = DsBgScreen.ReadColours(_member(m[0]));
            return (cells[cell], tiles, p => DsBgScreen.Row(all, firstRow ? 0 : p % Math.Max(1, all.Length / 16)));
        }

        private static void Turned(SpriteParts? s, byte[] rgba, double x, double y,
                                   double degrees, double scaleX, double scaleY)
        {
            if (s == null || scaleX <= 0.001 || scaleY <= 0.001) return;
            DsBgScreen.DrawCellTurned(rgba, s.Value.Cell, s.Value.Tiles, s.Value.Colours, (int)Math.Round(x), (int)Math.Round(y), degrees, scaleX, scaleY);
        }

        // A stand-in for the overworld: the field colour with a light grid.
        private static byte[] FieldGrid()
        {
            int w = DsBgScreen.Width, h = DsBgScreen.Height;
            byte[] rgba = new byte[w * h * 4];
            ushort line = Rgb(9, 12, 15);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    Put(rgba, (y * w + x) * 4, x % 16 == 0 || y % 16 == 0 ? line : Field);
            return rgba;
        }

        private static void Black(byte[] rgba, int left, int top, int right, int bottom)
        {
            int w = DsBgScreen.Width, h = DsBgScreen.Height;
            for (int y = Math.Max(0, top); y < Math.Min(h, bottom); y++)
                for (int x = Math.Max(0, left); x < Math.Min(w, right); x++)
                {
                    int at = (y * w + x) * 4;
                    rgba[at] = rgba[at + 1] = rgba[at + 2] = 0;
                    rgba[at + 3] = 255;
                }
        }

        // Master brightness: positive towards white, negative towards black, in sixteenths.
        private static void Brightness(byte[] rgba, double level)
        {
            if (Math.Abs(level) < 0.01) return;
            double t = Math.Min(1, Math.Abs(level) / 16);
            for (int i = 0; i < rgba.Length; i += 4)
                for (int k = 0; k < 3; k++)
                    rgba[i + k] = (byte)(level > 0 ? rgba[i + k] + (255 - rgba[i + k]) * t : rgba[i + k] * (1 - t));
        }

        // Two flashes, 3 frames out and 3 back each.
        private static double FlashLevel(int frame, bool white)
        {
            if (frame < 0 || frame >= FlashFrames) return 0;
            int t = frame % 6;
            double amount = t < 3 ? (t + 1) / 3.0 : (6 - t - 1) / 3.0;
            return (white ? 16 : -16) * amount;
        }

        // The encounter effects' accelerating moves start slow and speed up.
        private static double Accel(double t) => Math.Clamp(t, 0, 1) * Math.Clamp(t, 0, 1);

        private byte[] Balls(Scene s, int frame)
        {
            BallVariant v = s.Variant;
            bool settled = frame < 0;
            // The settled look is the ball at its fullest, before the wipe.
            if (settled) frame = FlashFrames + (v == BallVariant.GrassEarly ? 9 : v == BallVariant.WaterEarly ? 7 : 3);
            byte[] rgba = FieldGrid();
            int f = frame - FlashFrames;
            switch (v)
            {
                case BallVariant.GrassEarly:
                {
                    // The large ball spins up from nothing at the centre, then the field splits into 96-pixel bands that slide
                    // apart, each half of the ball going with its band.
                    SpriteParts? whole = Sequence(s.Large, 0);
                    if (f < 10)
                    {
                        double t = Accel((f + 1) / 10.0);
                        if (f >= 0) Turned(whole, rgba, 128, 96, 360 * t, Math.Max(0.01, t), Math.Max(0.01, t));
                    }
                    else
                    {
                        double x = 255 * Accel((f - 10 + 1) / 6.0);
                        Slice(rgba, 96, (int)x);
                        Turned(Sequence(s.Large, 1), rgba, 128 - x, 96, 0, 1, 1);
                        Turned(Sequence(s.Large, 2), rgba, 128 + x, 96, 0, 1, 1);
                    }
                    break;
                }
                case BallVariant.GrassLate:
                {
                    // Two small balls sweep across in opposite directions, turning twice, then a diagonal cut closes.
                    SpriteParts? ball = Sequence(s.Small, 0);
                    double t = Math.Clamp((f + 1) / 8.0, 0, 1);
                    if (f >= 0)
                    {
                        double sweep = -192 + 384 * t;
                        if (f >= 8) Slant(rgba, Math.Clamp((f - 8 + 1) / 8.0, 0, 1));
                        Turned(ball, rgba, 128 + sweep, 64, 720 * t, 1, 1);
                        Turned(ball, rgba, 128 - sweep, 128, -720 * t, 1, 1);
                    }
                    break;
                }
                case BallVariant.WaterEarly:
                {
                    // The field ripples; the large ball fades in turning once, then shrinks away.
                    if (f >= 0) Ripple(rgba, f);
                    SpriteParts? whole = Sequence(s.Large, 0);
                    if (f >= 0 && f < 8) Faded(whole, rgba, 128, 96, 360 * (f + 1) / 8.0, 1, (f + 1) / 8.0);
                    else if (f >= 8) { double sc = 1 - 0.99 * Accel((f - 8 + 1) / 8.0); Turned(whole, rgba, 128, 96, 0, sc, sc); }
                    break;
                }
                case BallVariant.WaterLate:
                {
                    // Three small balls rise one after another, each pulling a black column up behind it.
                    if (f >= 0) Ripple(rgba, f);
                    SpriteParts? ball = Sequence(s.Small, 0);
                    int[] xs = { 43, 215, 129 }, starts = { 0, 6, 10 };
                    for (int i = 0; i < 3; i++)
                    {
                        int g = f - starts[i];
                        if (g < 0) continue;
                        double t = Math.Clamp((g + 1) / 6.0, 0, 1);
                        double y = 231 + (-32 - 231) * t, top = 312 - 312 * t;
                        Black(rgba, xs[i] - 43, (int)top, xs[i] + 43, DsBgScreen.Height);
                        Turned(ball, rgba, xs[i], y, (i == 1 ? -360 : 360) * t, 1, 1);
                    }
                    break;
                }
                case BallVariant.CaveEarly:
                {
                    // A small ball drops from above, growing and turning, until it covers the screen.
                    SpriteParts? ball = Sequence(s.Small, 0);
                    if (f >= 0)
                    {
                        double t = Accel((Math.Min(f, 11) + 1) / 12.0);
                        double y = -32 + 256 * t, sc = 0.1 + 1.9 * t;
                        if (f < 12) Turned(ball, rgba, 128, y, 360 * t, sc, sc);
                    }
                    break;
                }
                default:
                {
                    // Three small balls drop in turn, then 32-pixel black blocks stack up, one a frame.
                    SpriteParts? ball = Sequence(s.Small, 0);
                    int[] xs = { 128, 208, 48 }, starts = { 0, 1, 3 };
                    for (int i = 0; i < 3; i++)
                    {
                        int g = f - starts[i];
                        if (g < 0 || g > 5) continue;
                        double t = Math.Clamp((g + 1) / 5.0, 0, 1);
                        Turned(ball, rgba, xs[i], -32 + 224 * t, (i == 1 ? -360 : 360) * t, 1, 1);
                    }
                    int blocks = Math.Clamp(f - 8 + 1, 0, 48);
                    int[] order = s.BlockOrder ?? new[] { 0, 2, 5, 7, 1, 6, 3, 4 };
                    for (int b = 0; b < blocks; b++)
                    {
                        int x = order[b % 8] * 32, y = 176 - (b / 8) * 32;
                        Black(rgba, x, y - 16, x + 32, y + 16);
                    }
                    break;
                }
            }
            if (!settled)
            {
                Brightness(rgba, FlashLevel(frame, v is BallVariant.GrassLate or BallVariant.WaterLate or BallVariant.CaveLate));
                int fadeFrom = BallLength(v) - FadeFrames;
                if (frame >= fadeFrom) Brightness(rgba, -16.0 * (frame - fadeFrom + 1) / FadeFrames);
            }
            return rgba;
        }

        // Bands of the field shift left and right alternately; what they uncover is black.
        private static void Slice(byte[] rgba, int band, int shift)
        {
            int w = DsBgScreen.Width, h = DsBgScreen.Height;
            byte[] copy = (byte[])rgba.Clone();
            for (int y = 0; y < h; y++)
            {
                int dx = (y / band) % 2 == 0 ? shift : -shift;
                for (int x = 0; x < w; x++)
                {
                    int sx = x + dx, at = (y * w + x) * 4;
                    if (sx < 0 || sx >= w) { rgba[at] = rgba[at + 1] = rgba[at + 2] = 0; continue; }
                    Array.Copy(copy, (y * w + sx) * 4, rgba, at, 4);
                }
            }
        }

        // Black closes in from two opposite corners along the diagonal.
        private static void Slant(byte[] rgba, double t)
        {
            int w = DsBgScreen.Width, h = DsBgScreen.Height;
            double reach = (w + h) / 2.0 * t;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (x + y < reach || (w - x) + (h - y) < reach)
                    {
                        int at = (y * w + x) * 4;
                        rgba[at] = rgba[at + 1] = rgba[at + 2] = 0;
                    }
        }

        // Rows of the field sway on a sine wave 12 pixels deep, as the water raster does.
        private static void Ripple(byte[] rgba, int frame)
        {
            int w = DsBgScreen.Width, h = DsBgScreen.Height;
            byte[] copy = (byte[])rgba.Clone();
            for (int y = 0; y < h; y++)
            {
                int dx = (int)Math.Round(12 * Math.Sin((y * 2.0 / 192 + frame * 800.0 / 65536) * 2 * Math.PI));
                for (int x = 0; x < w; x++) Array.Copy(copy, (y * w + ((x + dx) % w + w) % w) * 4, rgba, (y * w + x) * 4, 4);
            }
        }

        private static void Faded(SpriteParts? s, byte[] rgba, double x, double y,
                                  double degrees, double scale, double alpha)
        {
            if (s == null) return;
            byte[] layer = new byte[rgba.Length];
            Turned(s, layer, x, y, degrees, scale, scale);
            alpha = Math.Clamp(alpha, 0, 1);
            for (int i = 0; i < rgba.Length; i += 4)
                if (layer[i + 3] != 0)
                    for (int k = 0; k < 3; k++) rgba[i + k] = (byte)(rgba[i + k] * (1 - alpha) + layer[i + k] * alpha);
        }
    }
}
