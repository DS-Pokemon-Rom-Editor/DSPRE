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

            public bool CanWalk(int col, int row)
                => col >= 0 && row >= 0 && col < MapFile.mapSize && row < MapFile.mapSize
                   && (Collisions[row, col] & MapCollisionGrid.BlockedBit) == 0;

            public (int x, int y, int z) WorldAt(int col, int row)
            {
                if (IsGround) return (GroundX + col, GroundY, GroundZ + row);

                var b = Bounds;
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

            public bool TryTileFor(int worldX, int worldY, int worldZ, out int col, out int row)
            {
                col = row = 0;

                if (IsGround)
                {
                    if (worldY != GroundY) return false;
                    col = worldX - GroundX; row = worldZ - GroundZ;
                }
                else
                {
                    var b = Bounds;
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
            var surfaces = new List<Surface>();
            if (floor == null) return surfaces;

            for (int down = 0; down < cellsDown; down++)
                for (int across = 0; across < cellsAcross; across++)
                {
                    var map = mapAt?.Invoke(across, down);
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
                    });
                }

            if (data?.Platforms == null) return surfaces;

            for (int i = 0; i < data.Platforms.Count; i++)
            {
                var platform = data.Platforms[i];
                var grid = gridFor?.Invoke(platform.AttributeId);
                if (grid == null) continue;

                var surface = new Surface
                {
                    FloorIndex = floorIndex,
                    PatchX = floorIndex * gridBand,
                    PatchY = firstPlatformRow + i,
                    Kind = platform.Kind,
                    PlatformIndex = i,
                    Bounds = platform.Bounds,
                };

                bool upright = platform.Kind == TornWorldFile.PlatformKind.WestWall
                            || platform.Kind == TornWorldFile.PlatformKind.EastWall;
                int reachDown = (upright ? platform.Bounds.SizeY : platform.Bounds.SizeX) + 1;
                int reachAcross = platform.Bounds.SizeZ + 1;

                int down = Math.Min(Math.Min(MapFile.mapSize, platform.TilesVertical), reachDown);
                int across = Math.Min(Math.Min(MapFile.mapSize, platform.TilesHorizontal), reachAcross);

                for (int row = 0; row < MapFile.mapSize; row++)
                    for (int col = 0; col < MapFile.mapSize; col++)
                        surface.Collisions[row, col] = MapCollisionGrid.BlockedBit;

                for (int row = 0; row < across; row++)
                    for (int col = 0; col < down; col++)
                    {
                        int at = col + row * platform.TilesVertical;
                        if (at >= grid.Length) continue;

                        ushort tile = grid[at];
                        surface.Collisions[row, col] = (byte)((tile & 0x8000) != 0 ? MapCollisionGrid.BlockedBit : 0);
                        surface.Types[row, col] = (byte)(tile & 0xFF);
                    }

                surfaces.Add(surface);
            }

            return surfaces;
        }

        public static int CountWalkable(IEnumerable<Surface> surfaces)
        {
            int total = 0;
            if (surfaces == null) return 0;
            foreach (var surface in surfaces)
                for (int row = 0; row < MapFile.mapSize; row++)
                    for (int col = 0; col < MapFile.mapSize; col++)
                        if (surface.CanWalk(col, row)) total++;
            return total;
        }
    }
}
