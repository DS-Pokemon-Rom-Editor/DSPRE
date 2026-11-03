using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>The order the games' cursor tables store their four neighbours in.</summary>
    public enum CursorDirection { Up, Down, Left, Right }

    public static class CursorDirections
    {
        public static readonly CursorDirection[] All = { CursorDirection.Up, CursorDirection.Down, CursorDirection.Left, CursorDirection.Right };

        // Bit 0x80 on a neighbour sends the cursor back where it came from, when it came the opposite way.
        public const int Back = 0x80;

        /// <summary>Each stop, then each stop with <see cref="Back"/> set.</summary>
        public static IReadOnlyList<(int Value, string Name)> WithBack(string[] names, int stops) =>
            Enumerable.Range(0, stops).Select(s => (s, names[s]))
                .Concat(Enumerable.Range(0, stops).Select(s => (s | Back, names[s] + ", or back where it came from"))).ToList();
    }

    public static class ScreenSpots
    {
        /// <summary>Adds <paramref name="by"/> to a u8, s16 or s32 spot, kept between 0 and <paramref name="max"/>.</summary>
        public static void Shift(byte[] data, int at, int size, int by, int max)
        {
            if (size == 4) BitConverter.TryWriteBytes(data.AsSpan(at), Math.Clamp(BitConverter.ToInt32(data, at) + by, 0, max));
            else if (size == 2) BitConverter.TryWriteBytes(data.AsSpan(at), (short)Math.Clamp(BitConverter.ToInt16(data, at) + by, 0, max));
            else data[at] = (byte)Math.Clamp(data[at] + by, 0, max);
        }

        /// <summary>A u8, s16 or s32 spot.</summary>
        public static int Value(byte[] data, int at, int size) =>
            size switch { 4 => BitConverter.ToInt32(data, at), 2 => BitConverter.ToInt16(data, at), _ => data[at] };

        /// <summary>One spot table entry's x and y, in the table's units. <paramref name="at"/> is the entry's x.</summary>
        public static (int X, int Y) Entry(byte[] data, TouchListButtons.Spots s, int at) =>
            (s.YOnly ? 0 : Value(data, at, s.Size), s.XOnly ? 0 : Value(data, s.YOnly ? at : at + s.Size, s.Size));

        /// <summary>Moves one spot table entry by pixels, in its own units, without leaving the screen.</summary>
        public static void ShiftEntry(byte[] data, TouchListButtons.Spots s, int at, int dx, int dy)
        {
            int ux = (int)Math.Round(dx / (double)s.Unit), uy = (int)Math.Round(dy / (double)s.Unit);
            // An axis that doesn't move isn't clamped, so a spot the game keeps off screen stays where it is.
            if (!s.YOnly && ux != 0) Shift(data, at, s.Size, ux, 255 / s.Unit);
            if (!s.XOnly && uy != 0) Shift(data, s.YOnly ? at : at + s.Size, s.Size, uy, 191 / s.Unit);
        }
    }
}
