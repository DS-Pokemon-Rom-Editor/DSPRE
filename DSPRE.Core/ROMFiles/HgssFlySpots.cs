using System;
using System.Collections.Generic;
using System.IO;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The Pokégear town map's fly spots (pokeheartgold gMapFlypointParams, overlay 101): 27 entries of 14 bytes. Fly sends the
    /// player to the first Fly table row whose map is the spot's warp map, and the spot shows once its fly flag is set.
    /// </summary>
    public static class HgssFlySpots
    {
        public const int Count = 27, Size = 14;

        /// <summary>The town map in tiles (pokeheartgold region map screen 11). Map rows, which locations and the cursor use, are
        /// <see cref="RowsDown"/> below spot rows.</summary>
        public const int MapColumns = 47, MapRows = 20, RowsDown = 2;

        public sealed class Spot
        {
            public int NameMap, WarpMap;
            public byte Flag, Region;
            /// <summary>Cells of the main world matrix.</summary>
            public byte X, Y;
            public byte PatchX, PatchY;
            /// <summary>The click box in tiles, each 0 to 15.</summary>
            public byte Width, Height;
            public byte PatchWidth, PatchHeight, PatchLeft, PatchUp;

            /// <summary>Whether the click box fits on the town map.</summary>
            public bool OnMap => X + Math.Max(1, (int)Width) <= MapColumns && Y + Math.Max(1, (int)Height) <= MapRows - RowsDown;
        }

        /// <summary>A place on the town map (pokeheartgold sLocationSpecs, 100 entries of 16 bytes just before the spots), in map rows.
        /// The cursor shows its name and description inside it.</summary>
        public readonly record struct Location(int Map, int X, int Y, int Width, int Height);

        public const int LocationCount = 100, LocationSize = 16;
        // The location table ends 2 bytes before the spot table on every dump.
        private const int LocationsBefore = 0x642;

        public static List<Location> ReadLocations()
        {
            RomLocator.Spot spot = RomLocator.Where(RomLocator.Thing.HgssFlySpots);
            if (spot == null || RomLocator.WhyNot(RomLocator.Thing.HgssFlySpots, Count * Size) != null) return null;
            byte[] file = RomLocator.ReadFile(RomLocator.Thing.HgssFlySpots);
            int at = spot.Offset - LocationsBefore;
            if (at < 0 || at + LocationCount * LocationSize > file.Length) return null;
            List<Location> locations = new List<Location>(LocationCount);
            for (int i = 0; i < LocationCount; i++)
            {
                int o = at + i * LocationSize;
                locations.Add(new Location(BitConverter.ToUInt16(file, o), file[o + 2], file[o + 3], file[o + 4] & 0xF, file[o + 4] >> 4));
            }
            return locations;
        }

        /// <summary>Null when this ROM's table isn't known or doesn't look like the game's.</summary>
        public static List<Spot> Read()
        {
            if (RomLocator.WhyNot(RomLocator.Thing.HgssFlySpots, Count * Size) != null) return null;
            byte[] table = RomLocator.Read(RomLocator.Thing.HgssFlySpots, Count * Size);
            List<Spot> spots = new List<Spot>(Count);
            for (int i = 0; i < Count; i++)
            {
                int o = i * Size;
                Spot s = new Spot
                {
                    NameMap = BitConverter.ToUInt16(table, o), WarpMap = BitConverter.ToUInt16(table, o + 2),
                    Flag = table[o + 4], Region = table[o + 5], X = table[o + 6], Y = table[o + 7],
                    PatchX = table[o + 8], PatchY = table[o + 9],
                    Width = (byte)(table[o + 10] & 0xF), Height = (byte)(table[o + 10] >> 4),
                    PatchWidth = (byte)(table[o + 11] & 0xF), PatchHeight = (byte)(table[o + 11] >> 4),
                    PatchLeft = (byte)(table[o + 12] & 0xF), PatchUp = (byte)(table[o + 12] >> 4),
                };
                if (s.NameMap == 0 || s.WarpMap == 0 || !s.OnMap) return null;
                spots.Add(s);
            }
            return spots;
        }

        /// <summary>Writes each spot's place, warp map and flag. Other bytes are written back as read.</summary>
        public static void Write(IReadOnlyList<Spot> spots)
        {
            if (spots.Count != Count) throw new InvalidDataException("The town map has 27 fly spots.");
            for (int i = 0; i < Count; i++)
                if (!spots[i].OnMap) throw new InvalidDataException($"Fly spot {i + 1} is off the town map.");
            byte[] table = RomLocator.Read(RomLocator.Thing.HgssFlySpots, Count * Size);
            for (int i = 0; i < Count; i++)
            {
                Spot s = spots[i];
                int o = i * Size;
                BitConverter.GetBytes((ushort)s.WarpMap).CopyTo(table, o + 2);
                table[o + 4] = s.Flag;
                table[o + 6] = s.X;
                table[o + 7] = s.Y;
            }
            RomLocator.Write(RomLocator.Thing.HgssFlySpots, table);
        }
    }
}
