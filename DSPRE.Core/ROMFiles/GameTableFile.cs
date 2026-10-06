using System;
using System.IO;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Reads and writes a fixed-size table where <see cref="RomInfo.SpotOf"/> says the game keeps it, in arm9 or
    /// in an overlay. Tables here are edited in place; their size is compiled into the game's code.
    /// </summary>
    public static class GameTableFile
    {
        public static string PathOf(RomInfo.TableSpot spot) =>
            spot.Overlay < 0 ? RomInfo.arm9Path : OverlayUtils.GetPath(spot.Overlay);

        /// <summary>Why the table can't be read here, or null.</summary>
        public static string WhyNot(RomInfo.GameTable table, int length)
        {
            RomInfo.TableSpot? spot = RomInfo.SpotOf(table);
            if (spot == null) return "Only US HeartGold, Platinum (Rev 1) and Diamond are supported.";
            string path = PathOf(spot.Value);
            if (!File.Exists(path)) return $"{Path.GetFileName(path)} is missing from this project.";
            if (spot.Value.Overlay < 0 && !RomInfo.IsDsRomProject && ARM9.CheckCompressionMark())
                return "arm9 is still compressed. Convert this project to ds-rom format first.";
            // A compressed overlay is shorter on disk than once it's decompressed for the read.
            long size = spot.Value.Overlay >= 0 && OverlayUtils.IsStillCompressed(spot.Value.Overlay)
                ? OverlayUtils.OverlayTable.GetUncompressedSize(spot.Value.Overlay) : new FileInfo(path).Length;
            if (size < spot.Value.Offset + length) return $"{Path.GetFileName(path)} is too short for this table.";
            return null;
        }

        public static byte[] Read(RomInfo.GameTable table, int length)
        {
            string why = WhyNot(table, length);
            if (why != null) throw new InvalidOperationException(why);
            RomInfo.TableSpot spot = RomInfo.SpotOf(table).Value;
            if (spot.Overlay >= 0 && OverlayUtils.IsCompressed(spot.Overlay)) OverlayUtils.Decompress(spot.Overlay);
            return DSUtils.ReadFromFile(PathOf(spot), spot.Offset, length);
        }

        public static void Write(RomInfo.GameTable table, byte[] data)
        {
            string why = WhyNot(table, data.Length);
            if (why != null) throw new InvalidOperationException(why);
            RomInfo.TableSpot spot = RomInfo.SpotOf(table).Value;
            if (spot.Overlay >= 0 && OverlayUtils.IsCompressed(spot.Overlay)) OverlayUtils.Decompress(spot.Overlay);
            DSUtils.WriteToFile(PathOf(spot), data, (uint)spot.Offset);
        }
    }
}
