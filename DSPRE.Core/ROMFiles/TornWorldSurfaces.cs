using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>Lays out a floor's walkable surfaces side by side.</summary>
    public static class TornWorldSurfaces
    {
        public sealed class Surface
        {
            public int FloorIndex;

            public int PatchX, PatchY;

            public byte[,] Collisions = new byte[MapFile.mapSize, MapFile.mapSize];

            public byte[,] Types = new byte[MapFile.mapSize, MapFile.mapSize];

            public TornWorldFile.PlatformKind Kind = TornWorldFile.PlatformKind.Floor;

            public int PlatformIndex = -1;

            public bool IsGround => PlatformIndex < 0;

            public TornWorldFile.Bounds Bounds;

            public int GroundX, GroundZ, GroundY;

            /// <summary>Tiles the land data's terrain rises above the floor's altitude, per ground tile.</summary>
            public int[,] GroundLift;

            public bool CanWalk(int col, int row)
                => col >= 0 && row >= 0 && col < MapFile.mapSize && row < MapFile.mapSize
                   && (Collisions[row, col] & MapCollisionGrid.BlockedBit) == 0;

            public (int x, int y, int z) WorldAt(int col, int row)
            {
                if (IsGround) return (GroundX + col, GroundY, GroundZ + row);

                TornWorldFile.Bounds b = Bounds;
                switch (Kind)
                {
                    case TornWorldFile.PlatformKind.WestWall:
                        return (b.StartX, b.StartY + b.SizeY - col, b.StartZ + row);
                    case TornWorldFile.PlatformKind.EastWall:
                        return (b.StartX, b.StartY + col, b.StartZ + row);
                    case TornWorldFile.PlatformKind.Ceiling:
                        return (b.StartX + b.SizeX - col, b.StartY, b.StartZ + row);
                    default:
                        return (b.StartX + col, b.StartY, b.StartZ + row);
                }
            }

            /// <summary>
            /// Where the game puts the player on this tile, which is what jump points, prop triggers and camera
            /// regions are compared against: on the ground that is the terrain's height, not the floor's altitude.
            /// </summary>
            public (int x, int y, int z) EventAt(int col, int row)
            {
                (int x, int y, int z) at = WorldAt(col, row);
                return IsGround ? (at.x, at.y + Lift(col, row), at.z) : at;
            }

            private int Lift(int col, int row)
                => GroundLift != null && row >= 0 && col >= 0 && row < GroundLift.GetLength(0) && col < GroundLift.GetLength(1)
                    ? GroundLift[row, col] : 0;

            public bool TryTileFor(int worldX, int worldY, int worldZ, out int col, out int row)
            {
                col = row = 0;

                if (IsGround)
                {
                    col = worldX - GroundX; row = worldZ - GroundZ;
                    if (worldY != GroundY && worldY != GroundY + Lift(col, row)) return false;
                }
                else
                {
                    TornWorldFile.Bounds b = Bounds;
                    if (!b.Contains(worldX, worldY, worldZ)) return false;
                    row = worldZ - b.StartZ;
                    switch (Kind)
                    {
                        case TornWorldFile.PlatformKind.WestWall: col = b.StartY + b.SizeY - worldY; break;
                        case TornWorldFile.PlatformKind.EastWall: col = worldY - b.StartY; break;
                        case TornWorldFile.PlatformKind.Ceiling: col = b.StartX + b.SizeX - worldX; break;
                        default: col = worldX - b.StartX; break;
                    }
                }

                return col >= 0 && row >= 0 && col < MapFile.mapSize && row < MapFile.mapSize;
            }

            public (int x, int z) WalkTile(int col, int row)
                => (PatchX * MapFile.mapSize + col, PatchY * MapFile.mapSize + row);
        }

        public static List<Surface> ForFloor(TornWorldMapTable.Floor floor, TornWorldFile data,
            Func<int, int, MapFile> mapAt, Func<int, ushort[]> gridFor,
            int cellsAcross, int cellsDown, int floorIndex, int gridBand, int firstPlatformRow = 8)
        {
            List<Surface> surfaces = new List<Surface>();
            if (floor == null) return surfaces;

            for (int down = 0; down < cellsDown; down++)
                for (int across = 0; across < cellsAcross; across++)
                {
                    MapFile map = mapAt?.Invoke(across, down);
                    if (map?.collisions == null) continue;

                    surfaces.Add(new Surface
                    {
                        FloorIndex = floorIndex,
                        PatchX = floorIndex * gridBand + across,
                        PatchY = down,
                        Collisions = (byte[,])map.collisions.Clone(),
                        Types = map.types != null ? (byte[,])map.types.Clone() : new byte[MapFile.mapSize, MapFile.mapSize],
                        GroundX = floor.OffsetX + across * MapFile.mapSize,
                        GroundZ = floor.OffsetZ + down * MapFile.mapSize,
                        GroundY = floor.OffsetAltitude,
                        GroundLift = TerrainLift(map),
                    });
                }

            if (data?.Platforms == null) return surfaces;

            for (int i = 0; i < data.Platforms.Count; i++)
            {
                TornWorldFile.FloatingPlatform platform = data.Platforms[i];
                ushort[] grid = gridFor?.Invoke(platform.AttributeId);
                if (grid == null) continue;

                Surface surface = new Surface
                {
                    FloorIndex = floorIndex,
                    PatchX = floorIndex * gridBand,
                    PatchY = firstPlatformRow + i,
                    Kind = platform.Kind,
                    PlatformIndex = i,
                    Bounds = platform.Bounds,
                };

                Fill(surface, platform, grid);
                surfaces.Add(surface);
            }

            return surfaces;
        }

        /// <summary>How much of a platform's grid its patch shows: columns run down the grid, rows across it.</summary>
        public static (int columns, int rows) PaintedExtent(TornWorldFile.FloatingPlatform platform)
        {
            bool upright = platform.Kind == TornWorldFile.PlatformKind.WestWall
                        || platform.Kind == TornWorldFile.PlatformKind.EastWall;
            int reachDown = (upright ? platform.Bounds.SizeY : platform.Bounds.SizeX) + 1;
            int reachAcross = platform.Bounds.SizeZ + 1;

            int columns = Math.Max(0, Math.Min(Math.Min(MapFile.mapSize, platform.TilesVertical), reachDown));
            int rows = Math.Max(0, Math.Min(Math.Min(MapFile.mapSize, platform.TilesHorizontal), reachAcross));
            return (columns, rows);
        }

        /// <summary>The grid is column major over the vertical count (LoadFloatingPlatformTerrainAttributes).</summary>
        public static int GridIndex(TornWorldFile.FloatingPlatform platform, int column, int row)
            => column + row * platform.TilesVertical;

        public static void Fill(Surface surface, TornWorldFile.FloatingPlatform platform, ushort[] grid)
        {
            for (int row = 0; row < MapFile.mapSize; row++)
                for (int col = 0; col < MapFile.mapSize; col++)
                {
                    surface.Collisions[row, col] = MapCollisionGrid.BlockedBit;
                    surface.Types[row, col] = 0;
                }

            (int columns, int rows) = PaintedExtent(platform);
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < columns; col++)
                {
                    int at = GridIndex(platform, col, row);
                    if (at < 0 || at >= grid.Length) continue;

                    ushort tile = grid[at];
                    surface.Collisions[row, col] = (byte)((tile & 0x8000) != 0 ? MapCollisionGrid.BlockedBit : 0);
                    surface.Types[row, col] = (byte)(tile & 0xFF);
                }
        }

        /// <summary>Writes painted cells back into the grid, only where the patch shows the grid.</summary>
        public static void WriteBack(TornWorldFile.FloatingPlatform platform, ushort[] grid,
            byte[,] collisions, byte[,] types)
        {
            (int columns, int rows) = PaintedExtent(platform);
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < columns; col++)
                {
                    int at = GridIndex(platform, col, row);
                    if (at < 0 || at >= grid.Length) continue;
                    grid[at] = (ushort)((collisions[row, col] != 0 ? 0x8000 : 0) | types[row, col]);
                }
        }

        /// <summary>A grid of another size, keeping every cell that still fits; new cells are blocked.</summary>
        public static ushort[] ResizeGrid(ushort[] grid, int oldVertical, int oldHorizontal, int newVertical, int newHorizontal)
        {
            ushort[] resized = new ushort[Math.Max(0, newVertical) * Math.Max(0, newHorizontal)];
            for (int i = 0; i < resized.Length; i++) resized[i] = 0x8000;
            if (grid == null) return resized;

            for (int h = 0; h < Math.Min(oldHorizontal, newHorizontal); h++)
                for (int v = 0; v < Math.Min(oldVertical, newVertical); v++)
                {
                    int from = v + h * oldVertical;
                    if (from < grid.Length) resized[v + h * newVertical] = grid[from];
                }
            return resized;
        }

        /// <summary>A tw_arc_attr member as little endian u16 cells.</summary>
        public static ushort[] GridFromBytes(byte[] raw)
        {
            if (raw == null) return null;
            ushort[] grid = new ushort[raw.Length / 2];
            for (int i = 0; i < grid.Length; i++) grid[i] = (ushort)(raw[i * 2] | (raw[i * 2 + 1] << 8));
            return grid;
        }

        public static byte[] GridToBytes(ushort[] grid)
        {
            byte[] raw = new byte[grid.Length * 2];
            for (int i = 0; i < grid.Length; i++)
            {
                raw[i * 2] = (byte)(grid[i] & 0xFF);
                raw[i * 2 + 1] = (byte)(grid[i] >> 8);
            }
            return raw;
        }

        public static int CountWalkable(IEnumerable<Surface> surfaces)
        {
            int total = 0;
            if (surfaces == null) return 0;
            foreach (Surface surface in surfaces)
                for (int row = 0; row < MapFile.mapSize; row++)
                    for (int col = 0; col < MapFile.mapSize; col++)
                        if (surface.CanWalk(col, row)) total++;
            return total;
        }

        // The terrain file gives heights in 64-unit steps and a tile is 16 units, so a step is four tiles. A tile
        // with no terrain under it leaves the player at the height they had, so it takes its nearest neighbour's.
        private static int[,] TerrainLift(MapFile map)
        {
            int n = MapFile.mapSize;
            int[,] lift = new int[n, n];
            if (map?.bdhc == null || !BdhcFile.TryParse(map.bdhc, out BdhcFile terrain)) return lift;

            bool[,] found = new bool[n, n];
            for (int row = 0; row < n; row++)
                for (int col = 0; col < n; col++)
                    if (terrain.TryGetHeight((col + 0.5f) * 0.25f, (row + 0.5f) * 0.25f, 0f, out float y))
                    {
                        lift[row, col] = (int)Math.Round(y * 4f);
                        found[row, col] = true;
                    }

            int[,] filled = (int[,])lift.Clone();
            for (int row = 0; row < n; row++)
                for (int col = 0; col < n; col++)
                {
                    if (found[row, col]) continue;
                    for (int r = 1; r < n; r++)
                    {
                        int best = int.MinValue;
                        for (int dr = -r; dr <= r && best == int.MinValue; dr++)
                            for (int dc = -r; dc <= r; dc++)
                            {
                                if (Math.Max(Math.Abs(dr), Math.Abs(dc)) != r) continue;
                                int rr = row + dr, cc = col + dc;
                                if (rr < 0 || cc < 0 || rr >= n || cc >= n || !found[rr, cc]) continue;
                                best = lift[rr, cc];
                                break;
                            }
                        if (best == int.MinValue) continue;
                        filled[row, col] = best;
                        break;
                    }
                }
            return filled;
        }
    }
}
