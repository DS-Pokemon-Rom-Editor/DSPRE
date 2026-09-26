using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DSPRE.Models
{
    /// <summary>PDSMS collision defaults (.pdsts.meta) and auto collision.</summary>
    public static class TileCollisions
    {
        public const int TypeLayer = 0, CollisionLayer = 1;

        public static int ReadMeta(string tilesetPath, MapTileset set)
        {
            string path = tilesetPath + ".meta";
            if (set == null || !File.Exists(path)) return 0;

            int found = 0;
            foreach (string line in File.ReadAllLines(path))
            {
                var f = line.Split('|');
                if (f.Length < 6 || f[0] != "tile") continue;
                if (!int.TryParse(f[1], out int listed)) continue;
                int index = set.TileOfListed != null ? (listed < set.TileOfListed.Length ? set.TileOfListed[listed] : -1) : listed;
                if (index < 0 || index >= set.Tiles.Count) continue;
                var tile = set.Tiles[index];

                int field = 5;
                if (f.Length >= 12)
                {
                    int.TryParse(f[7], out int fw); int.TryParse(f[8], out int fd);
                    if (fw > 0 && fd > 0)
                    {
                        tile.CollisionWide = fw; tile.CollisionDeep = fd;
                        int.TryParse(f[9], out tile.CollisionAnchorX);
                        int.TryParse(f[10], out tile.CollisionAnchorY);
                    }
                    field = 11;
                }
                else if (f.Length >= 8) field = 7;

                if (string.IsNullOrEmpty(f[field])) continue;
                foreach (string chunk in f[field].Split(';'))
                {
                    if (chunk.Contains(':'))
                    {
                        foreach (string pair in chunk.Split(','))
                        {
                            var parts = pair.Split(':');
                            if (parts.Length != 2 || !int.TryParse(parts[0], out int layer)) continue;
                            if (!int.TryParse(parts[1], NumberStyles.HexNumber, null, out int value)) continue;
                            var grid = tile.CollisionGrid(layer);
                            for (int x = 0; x < grid.GetLength(0); x++)
                                for (int y = 0; y < grid.GetLength(1); y++) grid[x, y] = value & 0xff;
                        }
                    }
                    else
                    {
                        var parts = chunk.Split(',');
                        if (parts.Length != 4) continue;
                        if (!int.TryParse(parts[0], out int layer) || !int.TryParse(parts[1], out int x)
                            || !int.TryParse(parts[2], out int y)
                            || !int.TryParse(parts[3], NumberStyles.HexNumber, null, out int value)) continue;
                        var grid = tile.CollisionGrid(layer);
                        if (x >= 0 && y >= 0 && x < grid.GetLength(0) && y < grid.GetLength(1)) grid[x, y] = value & 0xff;
                    }
                }
                if (tile.CollisionDefaults.Count > 0) found++;
            }
            return found;
        }

        public const string MetaHeader = "# Pokemon DS Map Studio tile metadata v7";

        /// <summary>The .pdsts position of each tile, keyed by the tile itself so later reordering does not matter.</summary>
        public static Dictionary<MapTileset.Tile, int> ListedPlaces(MapTileset set)
        {
            var places = new Dictionary<MapTileset.Tile, int>(ReferenceEqualityComparer.Instance);
            if (set == null) return places;
            if (set.TileOfListed == null)
                for (int i = 0; i < set.Tiles.Count; i++) places[set.Tiles[i]] = i;
            else
                for (int listed = 0; listed < set.TileOfListed.Length; listed++)
                {
                    int i = set.TileOfListed[listed];
                    if (i >= 0 && i < set.Tiles.Count && !places.ContainsKey(set.Tiles[i])) places[set.Tiles[i]] = listed;
                }
            return places;
        }

        /// <summary>Writes the footprint and collision of the given tiles into the PDSMS sidecar, keeping every other line and field.</summary>
        /// <returns>Tiles written, or -1 when the file could not be written.</returns>
        public static int WriteMeta(string tilesetPath, IEnumerable<(MapTileset.Tile tile, int listed)> tiles, out string whynot)
        {
            whynot = null;
            string path = tilesetPath + ".meta";
            var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
            int header = lines.FindIndex(l => l.StartsWith("# Pokemon DS Map Studio tile metadata v", StringComparison.Ordinal));
            if (header >= 0) lines[header] = MetaHeader; else lines.Insert(0, MetaHeader);

            var lineOf = new Dictionary<int, int>();
            for (int i = 0; i < lines.Count; i++)
            {
                var f = lines[i].Split('|');
                if (f.Length >= 6 && f[0] == "tile" && int.TryParse(f[1], out int listed)) lineOf[listed] = i;
            }

            int written = 0;
            foreach (var (tile, listed) in tiles)
            {
                if (tile == null || listed < 0) continue;
                string cells = CellsText(tile);
                bool custom = tile.CollisionWide > 0 && tile.CollisionDeep > 0;
                bool had = lineOf.TryGetValue(listed, out int at);
                if (!had && cells.Length == 0 && !custom) continue;

                string[] f = had
                    ? lines[at].Split('|')
                    : new[] { "tile", listed.ToString(CultureInfo.InvariantCulture), "", "", "-1" };
                string displayW = f.Length >= 8 ? f[5] : "0", displayH = f.Length >= 8 ? f[6] : "0";
                string line = string.Join("|", f[0], f[1], f[2], f[3], f[4], displayW, displayH,
                    custom ? tile.CollisionWide : 0, custom ? tile.CollisionDeep : 0,
                    custom ? tile.CollisionAnchorX : 0, custom ? tile.CollisionAnchorY : -1, cells);
                if (had) lines[at] = line;
                else { lines.Add(line); lineOf[listed] = lines.Count - 1; }
                written++;
            }

            try
            {
                string temp = path + ".tmp";
                File.WriteAllLines(temp, lines);
                File.Move(temp, path, true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                whynot = e.Message;
                return -1;
            }
            return written;
        }

        // Same order as PDSMS: layer, then column, then row; row 0 is the tile's top.
        private static string CellsText(MapTileset.Tile tile)
        {
            var parts = new List<string>();
            foreach (var (layer, grid) in tile.CollisionDefaults.OrderBy(kv => kv.Key))
                for (int x = 0; x < grid.GetLength(0); x++)
                    for (int y = 0; y < grid.GetLength(1); y++)
                        if (grid[x, y] >= 0) parts.Add($"{layer},{x},{y},{grid[x, y] & 0xff:X2}");
            return string.Join(";", parts);
        }

        /// <summary>Gives each square of each tile the permissions it has on at least two thirds of the places the tile is put.</summary>
        public static int Learn(TileGrid grid, MapTileset set, byte[,] types, byte[,] collisions)
        {
            if (grid == null || set == null || types == null || collisions == null) return 0;
            int n = TileGrid.Across;
            var seen = new Dictionary<(int tile, int layer, int ix, int iy), List<int>>();
            foreach (var (ax, az, square) in grid.Placed())
            {
                if (square.Tile < 0 || square.Tile >= set.Tiles.Count || !square.WhereItWasPut) continue;
                var tile = set.Tiles[square.Tile];
                for (int ix = 0; ix < tile.FootprintWide; ix++)
                    for (int iy = 0; iy < tile.FootprintDeep; iy++)
                    {
                        var (mx, mz) = OnMap(tile, square, ax, az, ix, iy);
                        if (mx < 0 || mz < 0 || mx >= n || mz >= n) continue;
                        foreach (var (layer, from) in new[] { (TypeLayer, types), (CollisionLayer, collisions) })
                        {
                            if (!seen.TryGetValue((square.Tile, layer, ix, iy), out var values)) seen[(square.Tile, layer, ix, iy)] = values = new List<int>();
                            values.Add(from[mz, mx]);
                        }
                    }
            }

            int learned = 0;
            foreach (var byTile in seen.GroupBy(kv => kv.Key.tile))
            {
                var tile = set.Tiles[byTile.Key];
                bool any = false;
                foreach (var byLayer in byTile.GroupBy(kv => kv.Key.layer))
                {
                    var cells = new int[tile.FootprintWide, tile.FootprintDeep];
                    for (int x = 0; x < cells.GetLength(0); x++) for (int y = 0; y < cells.GetLength(1); y++) cells[x, y] = -1;
                    bool anyCell = false;
                    foreach (var kv in byLayer)
                    {
                        var most = kv.Value.GroupBy(v => v).OrderByDescending(g => g.Count()).First();
                        if (most.Count() * 3 < kv.Value.Count * 2) continue;
                        cells[kv.Key.ix, kv.Key.iy] = most.Key;
                        anyCell = true;
                    }
                    if (!anyCell) continue;
                    tile.CollisionDefaults[byLayer.Key] = cells;
                    any = true;
                }
                if (any) learned++;
            }
            return learned;
        }

        // The map square under one square of a tile's footprint, as placed.
        private static (int x, int z) OnMap(MapTileset.Tile tile, TileGrid.Square square, int ax, int az, int ix, int iy)
        {
            float cx = ix - tile.FootprintAnchorX + 0.5f;
            float cz = tile.Deep - 1 + (iy - tile.FootprintAnchorY) + 0.5f;
            var (tx, tz) = Turn(cx, cz, square.Turn, tile.Wide, tile.Deep);
            return (ax + (int)Math.Floor(tx), az - square.PastNorth + (int)Math.Floor(tz));
        }

        /// <param name="only">Squares to stamp; null stamps every square a tile covers.</param>
        public static int Apply(TileGrid grid, MapTileset set, byte[,] types, byte[,] collisions,
                                Func<int, int, bool> only = null)
        {
            if (grid == null || set == null) return 0;
            int n = TileGrid.Across;
            var value = new int[2, n, n];
            for (int l = 0; l < 2; l++) for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) value[l, z, x] = -1;

            for (int layer = 0; layer < TileGrid.Layers; layer++)
                foreach (var (ax, az, square) in grid.Placed().Where(p => p.square.Layer == layer))
                {
                    if (square.Tile < 0 || square.Tile >= set.Tiles.Count) continue;
                    var tile = set.Tiles[square.Tile];
                    foreach (var (which, cells) in tile.CollisionDefaults)
                    {
                        if (which < 0 || which > 1) continue;
                        for (int ix = 0; ix < cells.GetLength(0); ix++)
                            for (int iy = 0; iy < cells.GetLength(1); iy++)
                            {
                                int v = cells[ix, iy];
                                if (v < 0) continue;

                                var (mx, mz) = OnMap(tile, square, ax, az, ix, iy);
                                if (mx < 0 || mz < 0 || mx >= n || mz >= n) continue;
                                value[which, mz, mx] = v;
                            }
                    }
                }

            int changed = 0;
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    if (only != null && !only(x, z)) continue;
                    if (value[0, z, x] >= 0 && types[z, x] != value[0, z, x]) { types[z, x] = (byte)value[0, z, x]; changed++; }
                    if (value[1, z, x] >= 0 && collisions[z, x] != value[1, z, x]) { collisions[z, x] = (byte)value[1, z, x]; changed++; }
                }
            return changed;
        }

        private static (float x, float z) Turn(float x, float z, int quarters, int wide, int deep) => (quarters & 3) switch
        {
            1 => (deep - z, x),
            2 => (wide - x, deep - z),
            3 => (z, wide - x),
            _ => (x, z),
        };
    }
}
