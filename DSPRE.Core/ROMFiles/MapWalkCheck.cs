using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>Where a player can walk on one map, now and before an import.</summary>
    public sealed class MapWalkCheck
    {
        private const int Enough = 40;
        private static readonly (int dx, int dy)[] Beside = { (0, 1), (0, -1), (1, 0), (-1, 0) };

        private readonly byte[,] _collisions, _types, _wasCollisions, _wasTypes;
        private readonly HashSet<byte> _water;

        public MapWalkCheck(byte[,] collisions, byte[,] types, (byte[,] collisions, byte[,] types) was, HashSet<byte> waterTypes)
        {
            _collisions = collisions;
            _types = types;
            _wasCollisions = was.collisions;
            _wasTypes = was.types;
            _water = waterTypes;
        }

        public bool KnowsBefore => _wasCollisions != null;

        private static bool Inside(int x, int y) => x >= 0 && y >= 0 && x < MapFile.MapSize && y < MapFile.MapSize;

        private bool Open(byte[,] collisions, byte[,] types, int x, int y) =>
            Inside(x, y) && (collisions[y, x] & 0x80) == 0 && !_water.Contains(types[y, x]);

        public bool IsOpen(int x, int y) => Open(_collisions, _types, x, y);

        public bool WasOpen(int x, int y) => Open(_wasCollisions, _wasTypes, x, y);

        public string WarpTrouble(int x, int y, HashSet<(int x, int y)> warps, Func<byte, string> behaviourName)
        {
            if (_wasTypes != null && _wasTypes[y, x] != 0 && _types[y, x] != _wasTypes[y, x])
                return $"its {behaviourName(_wasTypes[y, x])} is now {behaviourName(_types[y, x])}";
            if (DeadEnd(x, y, warps) is string stuck) return stuck;
            bool wasOpen = _wasCollisions == null
                || Beside.Any(d => Inside(x + d.dx, y + d.dy) && (_wasCollisions[y + d.dy, x + d.dx] & 0x80) == 0);
            if (wasOpen && !Beside.Any(d => IsOpen(x + d.dx, y + d.dy))) return "nothing beside it can be walked onto";
            return null;
        }

        public string DeadEnd(int x, int y, HashSet<(int x, int y)> warps)
        {
            // Some retail warps are only reached by Surf or a cutscene, so only a new dead end counts.
            string now = DeadEnd(x, y, warps, _collisions, _types);
            if (now == null || _wasCollisions == null) return now;
            return DeadEnd(x, y, warps, _wasCollisions, _wasTypes) == null ? now : null;
        }

        private string DeadEnd(int x, int y, HashSet<(int x, int y)> warps, byte[,] collisions, byte[,] types)
        {
            int n = MapFile.MapSize;
            HashSet<(int, int)> seen = new HashSet<(int, int)> { (x, y) };
            Queue<(int x, int y)> queue = new Queue<(int x, int y)>();
            foreach ((int dx, int dy) in Beside)
                if (Open(collisions, types, x + dx, y + dy) && seen.Add((x + dx, y + dy))) queue.Enqueue((x + dx, y + dy));
            int reached = 0;
            while (queue.Count > 0)
            {
                (int a, int b) = queue.Dequeue();
                reached++;
                if (reached >= Enough || a == 0 || b == 0 || a == n - 1 || b == n - 1 || warps.Contains((a, b))) return null;
                foreach ((int dx, int dy) in Beside)
                {
                    int c = a + dx, d = b + dy;
                    if (warps.Contains((c, d)) && (c, d) != (x, y)) return null;
                    if (Open(collisions, types, c, d) && seen.Add((c, d))) queue.Enqueue((c, d));
                }
            }
            return reached == 0 ? "nothing beside it can be walked onto"
                 : $"only {Plural.Of(reached, "square")} around it can be walked before water or walls block the way";
        }

        public string WalkedInFrom(List<(int x, int y)> edge, string side, HashSet<(int x, int y)> warps)
        {
            string now = WalkedInFrom(edge, side, warps, _collisions, _types);
            if (now == null || _wasCollisions == null) return now;
            return WalkedInFrom(edge, side, warps, _wasCollisions, _wasTypes) == null ? now : null;
        }

        private string WalkedInFrom(List<(int x, int y)> edge, string side, HashSet<(int x, int y)> warps, byte[,] collisions, byte[,] types)
        {
            int n = MapFile.MapSize;
            bool OtherEdge(int a, int b) => side switch
            {
                "North" => b == n - 1 || a == 0 || a == n - 1,
                "South" => b == 0 || a == 0 || a == n - 1,
                "West" => a == n - 1 || b == 0 || b == n - 1,
                _ => a == 0 || b == 0 || b == n - 1,
            };
            HashSet<(int, int)> seen = new HashSet<(int, int)>(edge);
            Queue<(int x, int y)> queue = new Queue<(int x, int y)>(edge);
            int reached = 0;
            while (queue.Count > 0)
            {
                (int a, int b) = queue.Dequeue();
                reached++;
                if (reached >= Enough + edge.Count || warps.Contains((a, b)) || (OtherEdge(a, b) && !edge.Contains((a, b)))) return null;
                foreach ((int dx, int dy) in Beside)
                {
                    int c = a + dx, d = b + dy;
                    if (warps.Contains((c, d))) return null;
                    if (Open(collisions, types, c, d) && seen.Add((c, d))) queue.Enqueue((c, d));
                }
            }
            int inside = reached - edge.Count;
            return inside == 0 ? "water or walls block the way right past the edge"
                 : $"only {Plural.Of(inside, "square")} can be walked before water or walls block the way";
        }

        /// <summary>Edge squares leading next door now or before; closed holds those now shut.</summary>
        public (List<(int x, int y)> closed, List<(int x, int y)> crossing) EdgeSquares(string side, byte[,] besideCollisions, byte[,] besideTypes)
        {
            int n = MapFile.MapSize;
            List<(int x, int y)> closed = new List<(int x, int y)>();
            List<(int x, int y)> crossing = new List<(int x, int y)>();
            for (int i = 0; i < n; i++)
            {
                (int ax, int ay, int bx, int by) = side switch
                {
                    "North" => (i, 0, i, n - 1),
                    "South" => (i, n - 1, i, 0),
                    "West" => (0, i, n - 1, i),
                    _ => (n - 1, i, 0, i),
                };
                if (!Open(besideCollisions, besideTypes, bx, by)) continue;
                if (IsOpen(ax, ay)) crossing.Add((ax, ay));
                else if (WasOpen(ax, ay)) { closed.Add((ax, ay)); crossing.Add((ax, ay)); }
            }
            return (closed, crossing);
        }
    }
}
