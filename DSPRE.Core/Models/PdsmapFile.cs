using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DSPRE.Models
{
    /// <summary>Reads PDSMS .pdsmap projects.</summary>
    public static class PdsmapFile
    {
        public const int Across = 32, Layers = 9;

        public sealed class Map
        {
            public int X, Y;
            public int Area, ExportGroup;

            public int[][,] Tiles = new int[Layers][,];
            public int[][,] Heights = new int[Layers][,];
        }

        public sealed class Project
        {
            public int Game = -1;

            public string TilesetPath;

            public List<Map> Maps = new List<Map>();
        }

        public static Project Read(string path, out string whynot)
        {
            whynot = null;
            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch (Exception ex) { whynot = "Could not read file: " + ex.Message; return null; }

            var project = new Project();
            string folder = Path.GetDirectoryName(path) ?? ".";
            Map map = null;
            int tileLayer = 0, heightLayer = 0;

            for (int at = 0; at < lines.Length; at++)
            {
                string line = lines[at].Trim();
                string Next() => ++at < lines.Length ? lines[at].Trim() : "";

                if (line.StartsWith("gameindex", StringComparison.Ordinal))
                    int.TryParse(Next(), out project.Game);
                else if (line.StartsWith("tileset", StringComparison.Ordinal))
                    project.TilesetPath = Path.Combine(folder, Next());
                else if (line.StartsWith("mapstart", StringComparison.Ordinal))
                {
                    var bits = Next().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    map = new Map();
                    if (bits.Length >= 2) { int.TryParse(bits[0], out map.X); int.TryParse(bits[1], out map.Y); }
                    tileLayer = heightLayer = 0;
                }
                else if (line.StartsWith("areaindex", StringComparison.Ordinal))
                { if (map != null) int.TryParse(Next(), out map.Area); else Next(); }
                else if (line.StartsWith("exportgroup", StringComparison.Ordinal))
                { if (map != null) int.TryParse(Next(), out map.ExportGroup); else Next(); }
                else if (line.StartsWith("tilegrid", StringComparison.Ordinal) || line.StartsWith("heightgrid", StringComparison.Ordinal))
                {
                    bool tiles = line[0] == 't';
                    var grid = new int[Across, Across];
                    for (int row = 0; row < Across; row++)
                    {
                        var numbers = Next().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        if (numbers.Length < Across)
                        { whynot = $"Layer row {row} has {numbers.Length} values, expected {Across}."; return null; }
                        for (int col = 0; col < Across; col++)
                            if (!int.TryParse(numbers[col], NumberStyles.Integer, CultureInfo.InvariantCulture, out grid[col, row]))
                            { whynot = $"Not a number in layer: {numbers[col]}"; return null; }
                    }
                    if (map == null) continue;
                    // Rows are stored south to north.
                    if (tiles && tileLayer < Layers) map.Tiles[tileLayer++] = grid;
                    else if (!tiles && heightLayer < Layers) map.Heights[heightLayer++] = grid;
                }
                else if (line.StartsWith("mapend", StringComparison.Ordinal))
                {
                    if (map != null) project.Maps.Add(map);
                    map = null;
                }
            }

            if (project.Maps.Count == 0) { whynot = "No maps in file."; return null; }
            return project;
        }

        public static TileGrid ToGrid(Map map, MapTileset set, out int dropped)
        {
            dropped = 0;
            var grid = new TileGrid();
            if (map == null) return grid;

            for (int layer = 0; layer < Layers && layer < TileGrid.Layers; layer++)
            {
                var heights = map.Heights[layer];
                var tiles = map.Tiles[layer];

                if (heights != null)
                    for (int col = 0; col < Across; col++)
                        for (int row = 0; row < Across; row++)
                            grid.SetHeight(col, Across - 1 - row, heights[col, row] * TileGrid.Step, layer);

                if (tiles == null) continue;
                for (int col = 0; col < Across; col++)
                    for (int row = 0; row < Across; row++)
                    {
                        int listed = tiles[col, row];
                        if (listed < 0) continue;

                        int tile = set?.TileOfListed != null
                            ? (listed < set.TileOfListed.Length ? set.TileOfListed[listed] : -1)
                            : listed;
                        if (set == null || tile < 0 || tile >= set.Tiles.Count) { dropped++; continue; }

                        var t = set.Tiles[tile];
                        // PDSMS anchors at the south-west square, we anchor at the north-west one.
                        int z = Across - 1 - row - (t.Deep - 1);
                        if (!grid.Stamp(col, z, tile, t.Wide, t.Deep, layer)) dropped++;
                    }
            }
            return grid;
        }
    }
}
