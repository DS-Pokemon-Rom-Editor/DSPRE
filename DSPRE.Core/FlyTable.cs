using System.Collections.Generic;
using System.IO;

namespace DSPRE
{
    /// <summary>
    /// The ARM9 table pairing each town's fly spot with the warp a blackout or game over sends you to.
    /// Row 0 is the starting town.
    /// </summary>
    public static class FlyTable
    {
        public sealed class Row
        {
            public int HeaderIdGameOver;
            public ushort LocalX, LocalY;
            public int HeaderIdFly;
            public ushort GlobalX, GlobalY;

            // Diamond, Pearl and Platinum.
            public bool IsTeleportPos, UnlockOnMapEntry;
            public ushort UnlockId;

            // HeartGold and SoulSilver.
            public byte FlagIdx;
            public bool IsBlackoutSpawn, IsFlyPoint;
            public int HeaderIdUnlockWarp;
            public ushort GlobalXUnlock, GlobalYUnlock;
        }

        private static bool Hgss => RomInfo.gameFamily == RomInfo.GameFamilies.HGSS;

        public static List<Row> Read()
        {
            var rows = new List<Row>(RomInfo.FlyTableRows);
            using var r = new ARM9.Reader(RomInfo.FlyTableOffset);
            for (int i = 0; i < RomInfo.FlyTableRows; i++)
            {
                var row = new Row();
                if (Hgss)
                {
                    row.FlagIdx = r.ReadByte();
                    byte flags = r.ReadByte();
                    row.IsBlackoutSpawn = (flags & 0x01) != 0;
                    row.IsFlyPoint = (flags & 0x02) != 0;
                    row.HeaderIdGameOver = r.ReadUInt16();
                    row.LocalX = r.ReadByte();
                    row.LocalY = r.ReadByte();
                    row.HeaderIdFly = r.ReadUInt16();
                    row.GlobalX = r.ReadUInt16();
                    row.GlobalY = r.ReadUInt16();
                    row.HeaderIdUnlockWarp = r.ReadUInt16();
                    row.GlobalXUnlock = r.ReadUInt16();
                    row.GlobalYUnlock = r.ReadUInt16();
                }
                else
                {
                    row.HeaderIdGameOver = r.ReadUInt16();
                    row.LocalX = r.ReadUInt16();
                    row.LocalY = r.ReadUInt16();
                    row.HeaderIdFly = r.ReadUInt16();
                    row.GlobalX = r.ReadUInt16();
                    row.GlobalY = r.ReadUInt16();
                    row.IsTeleportPos = r.ReadByte() != 0;
                    row.UnlockOnMapEntry = r.ReadByte() != 0;
                    row.UnlockId = r.ReadUInt16();
                }
                rows.Add(row);
            }
            return rows;
        }

        public static void Write(IReadOnlyList<Row> rows)
        {
            using var w = new ARM9.Writer(RomInfo.FlyTableOffset);
            foreach (var row in rows)
            {
                if (Hgss)
                {
                    w.Write(row.FlagIdx);
                    w.Write((byte)((row.IsBlackoutSpawn ? 0x01 : 0) | (row.IsFlyPoint ? 0x02 : 0)));
                    w.Write((ushort)row.HeaderIdGameOver);
                    w.Write((byte)row.LocalX);
                    w.Write((byte)row.LocalY);
                    w.Write((ushort)row.HeaderIdFly);
                    w.Write(row.GlobalX);
                    w.Write(row.GlobalY);
                    w.Write((ushort)row.HeaderIdUnlockWarp);
                    w.Write(row.GlobalXUnlock);
                    w.Write(row.GlobalYUnlock);
                }
                else
                {
                    w.Write((ushort)row.HeaderIdGameOver);
                    w.Write(row.LocalX);
                    w.Write(row.LocalY);
                    w.Write((ushort)row.HeaderIdFly);
                    w.Write(row.GlobalX);
                    w.Write(row.GlobalY);
                    w.Write(row.IsTeleportPos ? (byte)1 : (byte)0);
                    w.Write(row.UnlockOnMapEntry ? (byte)1 : (byte)0);
                    w.Write(row.UnlockId);
                }
            }
        }

        /// <summary>Raised after the table is written, so anything framed on a fly spot can follow.</summary>
        public static event System.EventHandler Saved;

        public static void RaiseSaved() => Saved?.Invoke(null, System.EventArgs.Empty);

        /// <summary>One town's fly spot, as a global tile on its header's matrix.</summary>
        public readonly record struct Spot(int HeaderId, int X, int Z);

        /// <summary>Every town's fly spot, the starting town first, one per town.</summary>
        public static List<Spot> Spots()
        {
            var spots = new List<Spot>();
            var rows = TryRead();
            if (rows == null) return spots;
            var seen = new HashSet<int>();
            foreach (var row in rows)
                if (seen.Add(row.HeaderIdFly))
                    spots.Add(new Spot(row.HeaderIdFly, row.GlobalX, row.GlobalY));
            return spots;
        }

        /// <summary>The table, or null when it cannot be read.</summary>
        public static List<Row> TryRead()
        {
            try { return Read(); }
            catch (System.Exception ex) { AppLogger.Warn("The fly table could not be read: " + ex.Message); return null; }
        }
    }
}
