using System;
using System.Collections.Generic;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The places the game puts the player without a warp: where they wake after blacking out, where Fly lands, and
    /// (HGSS) where a town's warp unlocks. Read from the same arm9 table the Fly / warp editor edits.
    /// </summary>
    public static class SpawnPoints
    {
        public readonly record struct Point(string Kind, int Header, int X, int Z, bool Global);

        public static List<Point> Read()
        {
            List<Point> points = new List<Point>();
            foreach (FlyTable.Row row in FlyTable.Read())
            {
                // HGSS rows say which of their points the game uses; the warp unlock point is always there.
                bool hgss = GameFamily == GameFamilies.HGSS;
                if (!hgss || row.IsBlackoutSpawn) points.Add(new Point("Blackout point", row.HeaderIdGameOver, row.LocalX, row.LocalY, false));
                if (!hgss || row.IsFlyPoint) points.Add(new Point("Fly point", row.HeaderIdFly, row.GlobalX, row.GlobalY, true));
                if (hgss) points.Add(new Point("Warp unlock point", row.HeaderIdUnlockWarp, row.GlobalXUnlock, row.GlobalYUnlock, true));
            }
            return points;
        }
    }
}
