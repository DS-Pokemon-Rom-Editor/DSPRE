using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Working out which tile a point on screen is over, by projecting the tiles the map actually has
    /// and taking the nearest. Dropping somebody onto the map needs this, and so does dragging the spot
    /// a walk starts from.
    /// </summary>
    public static class FieldTilePicker
    {
        /// <summary>
        /// The tile nearest a point on screen, or null when nothing is close enough.
        /// <paramref name="tileToWorld"/> says where a tile sits, and <paramref name="project"/> turns
        /// that into screen pixels, handing back null for anything behind the camera.
        /// </summary>
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
        /// question gets asked over and over: dragging something across the map asks once per twitch of
        /// the pointer, and a header that stitches the whole matrix has getting on for three hundred
        /// thousand tiles, which is too many to walk that often.
        ///
        /// The tiles are sorted into buckets by where they landed, so a look-up only reads the bucket
        /// under the pointer and the ring around it. It holds good only while the camera stays put,
        /// which is exactly the case it is built for.
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

        /// <summary>
        /// The tile itself when somebody could stand on it, otherwise the closest one nearby that they
        /// could. Null when everything within reach is closed off.
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
