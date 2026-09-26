using System;
using System.Collections.Generic;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The places the game puts the player without a warp: where they wake after blacking out, where Fly lands, and
    /// (HGSS) where a town's warp unlocks. Read from the same arm9 table the Fly / Warp Editor edits.
    /// </summary>
    public static class SpawnPoints
    {
        public readonly record struct Point(string Kind, int Header, int X, int Z, bool Global);

        public static List<Point> Read()
        {
            var points = new List<Point>();
            using var reader = new ARM9.Reader(FlyTableOffset);
            for (int i = 0; i < FlyTableRows; i++)
            {
                if (gameFamily == GameFamilies.HGSS)
                {
                    reader.ReadByte();
                    byte flags = reader.ReadByte();
                    int blackout = reader.ReadUInt16(), bx = reader.ReadByte(), bz = reader.ReadByte();
                    int fly = reader.ReadUInt16(), fx = reader.ReadUInt16(), fz = reader.ReadUInt16();
                    int unlock = reader.ReadUInt16(), ux = reader.ReadUInt16(), uz = reader.ReadUInt16();
                    if ((flags & 0x01) != 0) points.Add(new Point("Blackout point", blackout, bx, bz, false));
                    if ((flags & 0x02) != 0) points.Add(new Point("Fly point", fly, fx, fz, true));
                    points.Add(new Point("Warp unlock point", unlock, ux, uz, true));
                }
                else
                {
                    int blackout = reader.ReadUInt16(), bx = reader.ReadUInt16(), bz = reader.ReadUInt16();
                    int fly = reader.ReadUInt16(), fx = reader.ReadUInt16(), fz = reader.ReadUInt16();
                    reader.ReadByte(); reader.ReadByte(); reader.ReadUInt16();
                    points.Add(new Point("Blackout point", blackout, bx, bz, false));
                    points.Add(new Point("Fly point", fly, fx, fz, true));
                }
            }
            return points;
        }
    }
}
