using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Working out which tile a point on screen is over, by projecting the tiles the map actually has and
    /// taking the nearest.
    /// </summary>
    public static class FieldTilePicker
    {
        /// <summary>The tile nearest a point on screen, or null when nothing is close enough. </summary>
        public static (int x, int z)? NearestTile(MapCollisionGrid map,
                                                  Func<float, float, (float x, float y, float z)> tileToWorld,
                                                  Func<float, float, float, (float sx, float sy)?> project,
                                                  double px, double py,
                                                  double withinPixels = 80)
        {
            if (map == null || map.IsEmpty || tileToWorld == null || project == null) return null;

            (int x, int z)? best = null;
            double bestD = withinPixels * withinPixels;
            foreach (var (x, z) in map.Tiles)
            {
                var foot = tileToWorld(x, z);
                var at = project(foot.x, foot.y, foot.z);
                if (at == null) continue;
                double dx = px - at.Value.sx, dy = py - at.Value.sy;
                double d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = (x, z); }
            }
            return best;
        }


        /// <summary>
        /// A picker that has already worked out where every tile lands on screen, for when the same
        /// question gets asked over and over: dragging something across the map asks once per twitch of the
        /// pointer, and a header that stitches the whole matrix has getting on for three hundred thousand
        /// tiles, which is too many to walk that often.
        /// </summary>
        public sealed class Prepared
        {
            private readonly Dictionary<(int, int), List<(int x, int z, float sx, float sy)>> _buckets
                = new Dictionary<(int, int), List<(int, int, float, float)>>();
            private readonly double _bucket;

            /// <summary>How many tiles it knows about, so a caller can say what it measured.</summary>
            public int TileCount { get; private set; }

            private Prepared(double bucketPixels) { _bucket = bucketPixels <= 0 ? 48 : bucketPixels; }

            public static Prepared Build(MapCollisionGrid map,
                                         Func<float, float, (float x, float y, float z)> tileToWorld,
                                         Func<float, float, float, (float sx, float sy)?> project,
                                         double bucketPixels = 48)
            {
                var p = new Prepared(bucketPixels);
                if (map == null || map.IsEmpty || tileToWorld == null || project == null) return p;

                foreach (var (x, z) in map.Tiles)
                {
                    var foot = tileToWorld(x, z);
                    var at = project(foot.x, foot.y, foot.z);
                    if (at == null) continue;                      // behind the camera
                    var key = ((int)Math.Floor(at.Value.sx / p._bucket), (int)Math.Floor(at.Value.sy / p._bucket));
                    if (!p._buckets.TryGetValue(key, out var list))
                        p._buckets[key] = list = new List<(int, int, float, float)>();
                    list.Add((x, z, at.Value.sx, at.Value.sy));
                    p.TileCount++;
                }
                return p;
            }

            /// <summary>The tile nearest a point, or null when nothing is close enough.</summary>
            public (int x, int z)? Nearest(double px, double py, double withinPixels = 80)
            {
                if (_buckets.Count == 0) return null;

                (int x, int z)? best = null;
                double bestD = withinPixels * withinPixels;

                // How far out to look, so a bucket that only just reaches is still read.
                int reach = (int)Math.Ceiling(withinPixels / _bucket);
                int cx = (int)Math.Floor(px / _bucket), cy = (int)Math.Floor(py / _bucket);

                for (int dy = -reach; dy <= reach; dy++)
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        if (!_buckets.TryGetValue((cx + dx, cy + dy), out var list)) continue;
                        foreach (var (x, z, sx, sy) in list)
                        {
                            double ddx = px - sx, ddy = py - sy;
                            double d = ddx * ddx + ddy * ddy;
                            if (d < bestD) { bestD = d; best = (x, z); }
                        }
                    }
                return best;
            }
        }

        /// <summary>One matrix cell's footprint in raw world space, for saying which tile a point is over.</summary>
        public readonly struct CellQuad
        {
            public int CellX { get; }
            public int CellY { get; }
            public float OriginX { get; }
            public float OriginZ { get; }
            public float Width { get; }
            public float Height { get; }

            public CellQuad(int cellX, int cellY, float originX, float originZ, float width, float height)
            {
                CellX = cellX; CellY = cellY;
                OriginX = originX; OriginZ = originZ;
                Width = width; Height = height;
            }
        }

        /// <summary>
        /// The tile under a point on screen, across however many maps are stitched together. Only the
        /// few cells whose centres land nearest are walked tile by tile, since a header can stitch
        /// hundreds of maps and every tile of every one of them is too many to reproject per move.
        /// </summary>
        /// <param name="project">Raw world x/z to (whether it is on screen at all, screenX, screenY).</param>
        /// <param name="withinTiles">How far off a tile centre the point may be and still count, in tiles
        /// as they appear on screen. A fixed pixel figure claims tiles for a pointer well off the map:
        /// zoomed out to a thumbnail, a tile is about three pixels wide.</param>
        public static bool TileAtScreen(IReadOnlyList<CellQuad> cells,
                                        Func<float, float, (bool ok, float sx, float sy)> project,
                                        double px, double py,
                                        out int cellX, out int cellY, out int col, out int row,
                                        int tilesPerCell = 32, int cellsToSearch = 4, double withinTiles = 1.5)
        {
            cellX = cellY = col = row = -1;
            if (cells == null || cells.Count == 0 || project == null || tilesPerCell <= 0) return false;

            var ranked = new List<(double d, CellQuad cell)>(cells.Count);
            foreach (CellQuad cell in cells)
            {
                var (ok, sx, sy) = project(cell.OriginX + cell.Width / 2f, cell.OriginZ + cell.Height / 2f);
                if (!ok) continue;
                ranked.Add(((sx - px) * (sx - px) + (sy - py) * (sy - py), cell));
            }
            if (ranked.Count == 0) return false;
            ranked.Sort((a, b) => a.d.CompareTo(b.d));

            double best = double.MaxValue, bestPitch = 0;
            int take = Math.Min(cellsToSearch, ranked.Count);
            for (int i = 0; i < take; i++)
            {
                CellQuad cell = ranked[i].cell;
                float tw = cell.Width / tilesPerCell, th = cell.Height / tilesPerCell;

                // What "close enough" is measured against: one of this cell's tiles as drawn right now.
                double pitch = 0;
                var (okA, ax, ay) = project(cell.OriginX + 0.5f * tw, cell.OriginZ + 0.5f * th);
                var (okB, bx, by) = project(cell.OriginX + 1.5f * tw, cell.OriginZ + 0.5f * th);
                if (okA && okB) pitch = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                for (int r = 0; r < tilesPerCell; r++)
                    for (int c = 0; c < tilesPerCell; c++)
                    {
                        var (ok, sx, sy) = project(cell.OriginX + (c + 0.5f) * tw, cell.OriginZ + (r + 0.5f) * th);
                        if (!ok) continue;
                        double d = (sx - px) * (sx - px) + (sy - py) * (sy - py);
                        if (d >= best) continue;
                        best = d; bestPitch = pitch; cellX = cell.CellX; cellY = cell.CellY; col = c; row = r;
                    }
            }

            double reach = bestPitch > 0 ? bestPitch * withinTiles : 1.0;
            if (col < 0 || best > reach * reach) { cellX = cellY = col = row = -1; return false; }
            return true;
        }

        /// <summary>
        /// The tile itself when somebody could stand on it, otherwise the closest one nearby that they
        /// could.
        /// </summary>
        public static (int x, int z)? NearestFree(MapCollisionGrid map, int x, int z,
                                                  Func<int, int, bool> alsoTaken = null, int reach = 3)
        {
            bool Free(int tx, int tz) =>
                (map == null || map.IsEmpty || !map.IsBlocked(tx, tz))
                && (alsoTaken == null || !alsoTaken(tx, tz));

            if (Free(x, z)) return (x, z);

            for (int r = 1; r <= reach; r++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                        if (Free(x + dx, z + dz)) return (x + dx, z + dz);
                    }
            return null;
        }
    }
}
