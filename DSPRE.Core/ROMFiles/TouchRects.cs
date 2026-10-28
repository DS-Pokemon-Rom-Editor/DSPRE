using System;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The games' touch rectangles: top, bottom, left and right bytes. The hit test (touchscreen.c in both decomps) takes in the
    /// top and left edges and leaves out the bottom and right ones. An entry starting 0xFE is a circle: x, y and radius.
    /// </summary>
    public static class TouchRects
    {
        /// <summary>Why a button whose art the game places itself can't move.</summary>
        public const string FixedArt = "The game draws this in a fixed place";

        // A right edge of 256 is stored as 0.
        public const int Size = 4, MaxRight = 256, MaxBottom = 192;
        private const byte Circle = 0xFE;

        private static bool IsCircle(byte[] data, int at) => data[at] == Circle;

        /// <summary>Whether the entry reads as an area on the screen, for lists the game counts in code instead of ending.</summary>
        public static bool OnScreen(byte[] data, int at)
        {
            if (at < 0 || at + Size > data.Length) return false;
            (int top, int bottom, int left, int right) = Read(data, at);
            return top >= 0 && top < bottom && bottom <= MaxBottom && left >= 0 && left < right && right <= MaxRight;
        }

        /// <summary>Top, bottom, left and right; a circle gives the square round it.</summary>
        public static (int Top, int Bottom, int Left, int Right) Read(byte[] data, int at)
        {
            if (!IsCircle(data, at)) return (data[at], data[at + 1], data[at + 2], data[at + 3] == 0 && data[at + 2] > 0 ? MaxRight : data[at + 3]);
            (int x, int y, int r) = (data[at + 1], data[at + 2], data[at + 3]);
            return (y - r, y + r, x - r, x + r);
        }

        /// <summary>Moves the area without leaving the screen and returns how far it went.</summary>
        public static (int Dx, int Dy) Move(byte[] data, int at, int dx, int dy)
        {
            (int top, int bottom, int left, int right) = Read(data, at);
            dx = Math.Clamp(dx, -Math.Max(0, left), MaxRight - right);
            dy = Math.Clamp(dy, -Math.Max(0, top), MaxBottom - bottom);
            if (IsCircle(data, at))
            {
                (data[at + 1], data[at + 2]) = ((byte)(data[at + 1] + dx), (byte)(data[at + 2] + dy));
                return (dx, dy);
            }
            (data[at], data[at + 1], data[at + 2], data[at + 3]) = ((byte)(top + dy), (byte)(bottom + dy), (byte)(left + dx), (byte)(right + dx));
            return (dx, dy);
        }

        /// <summary>Sets the size in pixels from the top left corner. A circle keeps its middle and takes half the smaller side.</summary>
        public static void Resize(byte[] data, int at, int width, int height)
        {
            if (IsCircle(data, at))
            {
                data[at + 3] = (byte)Math.Clamp(Math.Min(width, height) / 2, 1, 127);
                return;
            }
            (int top, int _, int left, int _) = Read(data, at);
            data[at + 1] = (byte)Math.Clamp(top + Math.Max(1, height), top + 1, MaxBottom);
            data[at + 3] = (byte)Math.Clamp(left + Math.Max(1, width), left + 1, MaxRight);
        }
    }
}
