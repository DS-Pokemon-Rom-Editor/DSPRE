using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Where you can and cannot walk across a set of maps, in whole-matrix tile coordinates so a step from
    /// one map into the next is answered the same way as a step inside one.
    /// </summary>
    public sealed class MapCollisionGrid
    {
        /// <summary>The bit the games test to decide a tile is closed off.</summary>
        public const byte BlockedBit = 0x80;

        /// <summary>The tile kind the games use for a counter you can talk across.</summary>
        public const byte CounterType = 0x80;

        private readonly Dictionary<(int cellX, int cellY), byte[,]> _cells
            = new Dictionary<(int, int), byte[,]>();
        private readonly Dictionary<(int cellX, int cellY), byte[,]> _types
            = new Dictionary<(int, int), byte[,]>();

        /// <summary>Adds one map's 32x32 collision bytes at its place in the matrix.</summary>
        public void Add(int cellX, int cellY, byte[,] collisions)
        {
            if (collisions != null) _cells[(cellX, cellY)] = collisions;
        }

        /// <summary>Every tile the grid holds, in whole-matrix tiles. </summary>
        public IEnumerable<(int x, int z)> Tiles
        {
            get
            {
                int size = MapFile.mapSize;
                foreach (var kv in _cells)
                {
                    int baseX = kv.Key.cellX * size, baseZ = kv.Key.cellY * size;
                    int rows = kv.Value.GetLength(0), cols = kv.Value.GetLength(1);
                    for (int z = 0; z < rows; z++)
                        for (int x = 0; x < cols; x++)
                            yield return (baseX + x, baseZ + z);
                }
            }
        }

        /// <summary>Adds one map's tile kinds, which say what a tile is rather than whether you can cross it.</summary>
        public void AddTypes(int cellX, int cellY, byte[,] types)
        {
            if (types != null) _types[(cellX, cellY)] = types;
        }

        /// <summary>
        /// A counter is the shop-desk tile: talking reaches one tile further so you can speak to whoever is
        /// standing behind it (SXY_HeroFrontObjGet).
        /// </summary>
        public bool IsCounter(int tileX, int tileZ) => TypeAt(tileX, tileZ) == CounterType;

        /// <summary>The tile kind, or 0 when that map is not loaded.</summary>
        public byte TypeAt(int tileX, int tileZ)
        {
            int size = MapFile.mapSize;
            int cellX = FloorDiv(tileX, size), cellZ = FloorDiv(tileZ, size);
            if (!_types.TryGetValue((cellX, cellZ), out var grid)) return 0;

            int x = tileX - cellX * size, z = tileZ - cellZ * size;
            if (x < 0 || z < 0 || z >= grid.GetLength(0) || x >= grid.GetLength(1)) return 0;
            return grid[z, x];
        }

        public bool IsEmpty => _cells.Count == 0;

        /// <summary>Whether a tile is closed off, in tiles across the whole matrix. </summary>
        public bool IsBlocked(int tileX, int tileZ)
        {
            int size = MapFile.mapSize;
            int cellX = FloorDiv(tileX, size), cellZ = FloorDiv(tileZ, size);
            if (!_cells.TryGetValue((cellX, cellZ), out var grid)) return true;

            int x = tileX - cellX * size, z = tileZ - cellZ * size;
            if (x < 0 || z < 0 || z >= grid.GetLength(0) || x >= grid.GetLength(1)) return true;
            return (grid[z, x] & BlockedBit) != 0;
        }

        private static int FloorDiv(int a, int b) => a >= 0 ? a / b : (a - b + 1) / b;
    }
}
