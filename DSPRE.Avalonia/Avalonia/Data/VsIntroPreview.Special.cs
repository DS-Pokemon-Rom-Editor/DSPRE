using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;
using SpriteParts = (DSPRE.Avalonia.Data.DsBgScreen.Oam[] Cell, byte[] Tiles, System.Func<int, ushort[]> Colours);

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The Team Rocket, Red and Kimono Girl intros, with the frame counts of the HGSS encounter effects: six Rocket R's
    /// that fly in and shrink away, Red's random black blocks under the old Poké Ball, and the shoji doors that slide
    /// shut and open again.
    /// </summary>
    public sealed partial class VsIntroPreview
    {
        public enum SpecialVariant { Rocket, Red, Shoji }

        // The shipped emblem flights, for a ROM whose table wasn't read: they are the same in every game.
        private static readonly VsIntroMotion.Flight[] ShippedFlights =
        {
            Fly(260, 0, -30, 20, 4, 2), Fly(-16, 160, 30, -20, 3, 1), Fly(0, -16, 30, 20, 4, -3),
            Fly(140, 160, -10, -20, 2, -2), Fly(260, 80, -30, 1, 3, -3), Fly(0, 160, 30, -20, 3, 1),
        };
        private const int RocketFlashOut = 12;

        private static VsIntroMotion.Flight Fly(int sx, int sy, int ssx, int ssy, int wait, int turns) => new VsIntroMotion.Flight
        {
            StartX = sx, EndX = 128, SpeedX = ssx, StartY = sy, EndY = 100, SpeedY = ssy, Wait = wait, Turns = turns,
        };

        private static int[] RocketStarts(IReadOnlyList<VsIntroMotion.Flight> flights)
        {
            int[] starts = new int[flights.Count];
            int at = FlashFrames;
            for (int i = 0; i < flights.Count; i++) { at += flights[i].Wait + 1; starts[i] = at; }
            return starts;
        }

        private static int Move(Scene s) => s.Timing(VsIntroTimingAddon.Field.EmblemFlight);
        private static int Close(Scene s) => s.Timing(VsIntroTimingAddon.Field.DoorsClose);
        private static int Hold(Scene s) => s.Timing(VsIntroTimingAddon.Field.DoorsHold);
        private static int Open(Scene s) => s.Timing(VsIntroTimingAddon.Field.DoorsOpen);

        private static int SpecialLength(Scene s) => s.Special switch
        {
            SpecialVariant.Rocket => RocketStarts(s.Flights ?? ShippedFlights)[^1] + Move(s) + RocketFlashOut,
            SpecialVariant.Red => FlashFrames + 48 + FadeFrames,
            _ => FlashFrames + Close(s) + Hold(s) + Open(s) + FadeFrames,
        };

        // The encounter effects' accelerated moves: from s to e in n frames, starting at speed ss.
        private static double AddMove(double s, double e, double ss, int n, double t)
        {
            t = Math.Clamp(t, 0, n);
            double a = 2 * (e - s - ss * n) / (n * n);
            return s + ss * t + 0.5 * a * t * t;
        }

        private byte[] Special(Scene s, int frame)
        {
            bool settled = frame < 0;
            byte[] rgba = FieldGrid();
            switch (s.Special)
            {
                case SpecialVariant.Rocket:
                {
                    SpriteParts? r = Sequence(s.Emblem, 0);
                    IReadOnlyList<VsIntroMotion.Flight> flights = s.Flights ?? ShippedFlights;
                    int[] starts = RocketStarts(flights);
                    if (settled) { Turned(r, rgba, flights[^1].EndX, flights[^1].EndY, 0, 1, 1); return rgba; }
                    for (int i = 0; i < flights.Count; i++)
                    {
                        int g = frame - starts[i];
                        if (g < 0 || g >= Move(s)) continue;
                        VsIntroMotion.Flight p = flights[i];
                        double t = g + 1;
                        double x = AddMove(p.StartX, p.EndX, p.SpeedX, Move(s), t), y = AddMove(p.StartY, p.EndY, p.SpeedY, Move(s), t);
                        double sc = AddMove(2.0, 0.01, -0.4, Move(s), t);
                        Turned(r, rgba, x, y, 360.0 * p.Turns * t / Move(s), sc, sc);
                    }
                    Brightness(rgba, FlashLevel(frame, false));
                    int outFrom = starts[^1] + Move(s);
                    if (frame >= outFrom)
                    {
                        int k = frame - outFrom;
                        Brightness(rgba, k < RocketFlashOut / 2 ? 16.0 * (k + 1) / (RocketFlashOut / 2) : -16.0 * (k - RocketFlashOut / 2 + 1) / (RocketFlashOut / 2));
                    }
                    return rgba;
                }
                case SpecialVariant.Red:
                {
                    // Blocks in a fixed shuffled order stand in for the game's non-repeating random one.
                    int blocks = settled ? 24 : Math.Clamp(frame - FlashFrames + 1, 0, 48);
                    int[] order = Shuffled(48);
                    for (int b = 0; b < blocks; b++) Black(rgba, order[b] % 8 * 32, order[b] / 8 * 32, order[b] % 8 * 32 + 32, order[b] / 8 * 32 + 32);
                    Turned(Sequence(s.Emblem, 0), rgba, 128, 96, 0, 1, 1);
                    if (!settled)
                    {
                        Brightness(rgba, FlashLevel(frame, false));
                        int fadeFrom = SpecialLength(s) - FadeFrames;
                        if (frame >= fadeFrom) Brightness(rgba, -16.0 * (frame - fadeFrom + 1) / FadeFrames);
                    }
                    return rgba;
                }
                default:
                {
                    // Two layers of the doors' 512-wide screen scroll in from 128 and 384 to 0, stay shut while the field is
                    // hidden, then open again.
                    int close = Close(s), hold = Hold(s), open = Open(s);
                    int f = settled ? close : frame - FlashFrames;
                    double x;
                    if (f < 0) x = 128;
                    else if (f < close) x = AddMove(128, 0, -5, close, f + 1);
                    else if (f < close + hold) x = 0;
                    else x = AddMove(0, 128, 5, open, f - close - hold + 1);
                    bool shut = f >= close && f < close + hold;
                    if (shut || f >= close + hold) rgba = Plain();
                    (byte[] Rgba, int Width, int Height)? wide = WideScreen(s.Screen);
                    if (wide != null && f >= 0)
                    {
                        Layer(rgba, wide.Value, (int)Math.Round(x));
                        Layer(rgba, wide.Value, 384 - (int)Math.Round(x));
                    }
                    if (!settled)
                    {
                        Brightness(rgba, FlashLevel(frame, false));
                        int fadeFrom = SpecialLength(s) - FadeFrames;
                        if (frame >= fadeFrom) Brightness(rgba, -16.0 * (frame - fadeFrom + 1) / FadeFrames);
                    }
                    return rgba;
                }
            }
        }

        private static int[] Shuffled(int n)
        {
            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            uint seed = 0x2D1F;
            for (int i = n - 1; i > 0; i--)
            {
                seed = seed * 1103515245 + 12345;
                int j = (int)((seed >> 16) % (uint)(i + 1));
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }

        // A whole background screen (palette, tiles, screen) as RGBA, transparent where colour 0 is.
        private (byte[] Rgba, int Width, int Height)? WideScreen(int[] m)
        {
            if (m == null || m.Length < 3) return null;
            byte[] tiles = DsBgScreen.ReadCharacters(_member(m[1]));
            ushort[] colours = DsBgScreen.ReadColours(_member(m[0]));
            (int w, ushort[] entries) = DsBgScreen.ReadMap(_member(m[2]));
            if (w <= 0 || entries == null || tiles.Length == 0) return null;
            int cols = w, rows = entries.Length / cols, width = cols * 8, height = rows * 8;
            byte[] rgba = new byte[width * height * 4];
            for (int i = 0; i < entries.Length; i++)
            {
                int e = entries[i], tile = e & 0x3FF, row = (e >> 12) & 0xF;
                // Screens wider than 32 squares are stored as 32 by 32 blocks side by side.
                int block = cols > 32 ? i / 1024 : 0, inBlock = cols > 32 ? i % 1024 : i;
                int tx = cols > 32 ? (block * 32 + inBlock % 32) * 8 : i % cols * 8;
                int ty = cols > 32 ? inBlock / 32 * 8 : i / cols * 8;
                for (int py = 0; py < 8; py++)
                    for (int px = 0; px < 8; px++)
                    {
                        int lx = (e & 0x400) != 0 ? 7 - px : px, ly = (e & 0x800) != 0 ? 7 - py : py;
                        int at = tile * 32 + (ly * 8 + lx) / 2;
                        if (at >= tiles.Length) continue;
                        int index = (lx & 1) != 0 ? tiles[at] >> 4 : tiles[at] & 0xF;
                        if (index == 0 || row * 16 + index >= colours.Length) continue;
                        Put(rgba, ((ty + py) * width + tx + px) * 4, colours[row * 16 + index]);
                    }
            }
            return (rgba, width, height);
        }

        private static void Layer(byte[] rgba, (byte[] Rgba, int Width, int Height) layer, int scrollX)
        {
            int w = DsBgScreen.Width, h = DsBgScreen.Height;
            for (int y = 0; y < h && y < layer.Height; y++)
                for (int x = 0; x < w; x++)
                {
                    int sx = ((x + scrollX) % layer.Width + layer.Width) % layer.Width, from = (y * layer.Width + sx) * 4;
                    if (layer.Rgba[from + 3] == 0) continue;
                    Array.Copy(layer.Rgba, from, rgba, (y * w + x) * 4, 4);
                }
        }
    }
}
