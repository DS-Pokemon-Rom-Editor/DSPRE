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
    }
}
