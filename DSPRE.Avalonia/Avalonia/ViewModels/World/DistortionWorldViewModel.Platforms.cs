using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using LibNDSFormats.NSBMD;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using System.IO;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class DistortionWorldViewModel
    {
        private void RebuildOverlay()
        {
            List<float> mesh = new List<float>();
            if (Model3D != null && _showGravity)
                foreach ((int floorIndex, FloorRow floor) in ShownFloors())
                {
                    TornWorldSurfaces.Surface chosen = _selectedPlatformIndex >= 0 && _selectedPlatformIndex < SurfaceRows.Count
                        ? SurfaceRows[_selectedPlatformIndex].Surface : null;
                    bool selected = floor == CurrentFloor;

                    for (int i = 0; i < floor.Data.Platforms.Count; i++)
                        AppendPlatform(mesh, floor.Data.Platforms[i], floor.Floor, floorIndex,
                                       selected && chosen != null && !chosen.IsGround && chosen.PlatformIndex == i);
                }

            OverlayMesh = mesh.ToArray();
            OverlayVertexCount = OverlayMesh.Length / 8;
            Raise(nameof(OverlayMesh));
        }

        private const float TileInset = 0.16f;
        private const float Lift = 0.35f;

        private static readonly (float r, float g, float b)[] KindColours =
        {
            (0.30f, 0.60f, 0.38f), (0.32f, 0.49f, 0.77f), (0.59f, 0.42f, 0.77f), (0.76f, 0.55f, 0.27f),
        };

        private void AppendPlatform(List<float> mesh, TornWorldFile.FloatingPlatform platform,
            TornWorldMapTable.Floor floor, int floorIndex, bool selected)
        {
            ushort[] grid = Grid(platform.AttributeId);
            if (grid == null) return;

            int kind = (int)platform.Kind;
            (float r, float g, float b) = kind >= 0 && kind < KindColours.Length ? KindColours[kind] : (0.6f, 0.6f, 0.6f);
            if (selected) { r = Math.Min(1f, r * 1.6f); g = Math.Min(1f, g * 1.6f); b = Math.Min(1f, b * 1.6f); }

            TornWorldFile.Bounds bounds = platform.Bounds;
            for (int z = 0; z <= bounds.SizeZ; z++)
                for (int x = 0; x <= bounds.SizeX; x++)
                    for (int y = 0; y <= bounds.SizeY; y++)
                    {
                        int tileX = bounds.StartX + x, tileY = bounds.StartY + y, tileZ = bounds.StartZ + z;
                        ushort tile = TornWorldFile.AttributeAt(platform, grid, tileX, tileY, tileZ) ?? 0x8000;
                        if ((tile & 0x8000) != 0) continue;
                        if (!TryTile(floorIndex, floor, tileX, tileZ, out float originX, out float originZ,
                                     out int col, out int row, out _)) continue;

                        float sizeX = NsbmdGeometry.TileSize, sizeZ = NsbmdGeometry.TileSize;
                        float x0 = originX + col * sizeX, z0 = originZ + row * sizeZ;
                        float height = HeightOf(tileY) + Lift + (float)_overlayHeight;

                        float insetX = sizeX * TileInset, insetZ = sizeZ * TileInset;
                        bool special = (tile & 0xFF) != 0;
                        AddQuad(mesh, x0 + insetX, x0 + sizeX - insetX, z0 + insetZ, z0 + sizeZ - insetZ, height,
                            special ? 0.89f : r, special ? 0.76f : g, special ? 0.33f : b);
                    }
        }

        private void AddQuad(List<float> v, float x0, float x1, float z0, float z1, float y, float r, float g, float b)
        {
            void Vertex(float x, float z)
            {
                (float nx, float ny, float nz) = Model3D.ToNormalized(x, y, z);
                v.Add(nx); v.Add(ny); v.Add(nz); v.Add(0); v.Add(0); v.Add(r); v.Add(g); v.Add(b);
            }
            Vertex(x0, z0); Vertex(x1, z0); Vertex(x1, z1);
            Vertex(x0, z0); Vertex(x1, z1); Vertex(x0, z1);
        }
        private ushort[] Grid(int attributeId)
        {
            if (_gridCache.TryGetValue(attributeId, out ushort[] cached)) return cached;
            if (!_attributes.Available) return null;

            try
            {
                byte[] raw = _attributes.Get(attributeId);
                if (raw == null) return null;

                ushort[] grid = TornWorldSurfaces.GridFromBytes(raw);
                _gridCache[attributeId] = grid;
                return grid;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"DistortionWorld.Grid({attributeId}): " + ex.Message);
                return null;
            }
        }

        private void ShowPlatform()
        {
            for (int row = 0; row < PaintedGridSize; row++)
                for (int col = 0; col < PaintedGridSize; col++)
                {
                    CollisionCells[row, col] = MapCollisionGrid.BlockedBit;
                    BehaviourCells[row, col] = 0;
                }

            TornWorldSurfaces.Surface chosen = _selectedPlatformIndex >= 0 && _selectedPlatformIndex < SurfaceRows.Count
                ? SurfaceRows[_selectedPlatformIndex].Surface : null;

            if (chosen == null)
            {
                UsedRows = UsedColumns = PaintedGridSize;
                PlatformNote = "";
                GridNote = "";
                PlatformShown?.Invoke();
                return;
            }

            for (int row = 0; row < PaintedGridSize; row++)
                for (int col = 0; col < PaintedGridSize; col++)
                {
                    CollisionCells[row, col] = chosen.Collisions[row, col];
                    BehaviourCells[row, col] = chosen.Types[row, col];
                }

            if (chosen.IsGround)
            {
                UsedRows = UsedColumns = PaintedGridSize;
                PlatformNote = $"Map  ·  x {chosen.GroundX} to {chosen.GroundX + 31}, "
                             + $"z {chosen.GroundZ} to {chosen.GroundZ + 31}";
            }
            else
            {
                bool upright = chosen.Kind == TornWorldFile.PlatformKind.WestWall
                            || chosen.Kind == TornWorldFile.PlatformKind.EastWall;
                TornWorldFile.FloatingPlatform platform = chosen.PlatformIndex < Platforms.Count ? Platforms[chosen.PlatformIndex] : null;
                (int columns, int rows) = platform != null ? TornWorldSurfaces.PaintedExtent(platform) : (0, 0);
                UsedColumns = columns;
                UsedRows = rows;
                PlatformNote = $"{columns} across ({(upright ? "height" : "x")}) x {rows} down (z)";
            }

            GridNote = chosen.IsGround
                ? "Map permissions"
                : Explain(chosen.Kind);

            RebuildOverlay();
            PlatformShown?.Invoke();
            SceneShown?.Invoke();
        }

        private static string Explain(TornWorldFile.PlatformKind kind)
        {
            switch (kind)
            {
                case TornWorldFile.PlatformKind.WestWall:
                    return "West wall: columns run down Y, rows along Z.";
                case TornWorldFile.PlatformKind.EastWall:
                    return "East wall: columns run up Y, rows along Z.";
                case TornWorldFile.PlatformKind.Ceiling:
                    return "Ceiling: columns run along -X, rows along Z.";
                default:
                    return "Floor: columns run along X, rows along Z.";
            }
        }

        public void ApplyPaintedGrid()
        {
            TornWorldSurfaces.Surface chosen = _selectedPlatformIndex >= 0 && _selectedPlatformIndex < SurfaceRows.Count
                ? SurfaceRows[_selectedPlatformIndex].Surface : null;
            if (chosen == null) return;

            for (int row = 0; row < PaintedGridSize; row++)
                for (int col = 0; col < PaintedGridSize; col++)
                {
                    chosen.Collisions[row, col] = CollisionCells[row, col];
                    chosen.Types[row, col] = BehaviourCells[row, col];
                }

            if (chosen.IsGround)
            {
                PaintedGroundWarning = "Map permissions are saved from the Map editor.";
            }
            else
            {
                PaintedGroundWarning = null;

                TornWorldFile.FloatingPlatform platform = Platforms.Count > chosen.PlatformIndex ? Platforms[chosen.PlatformIndex] : null;
                if (platform != null && _shownGrids.TryGetValue(platform.AttributeId, out ushort[] grid))
                {
                    TornWorldSurfaces.WriteBack(platform, grid, CollisionCells, BehaviourCells);
                    // Cells past the grid stay blocked, as the game treats them.
                    TornWorldSurfaces.Fill(chosen, platform, grid);

                    _editedGrids.Add(platform.AttributeId);
                    Raise(nameof(HasUnsavedChanges));
                }
            }

            Raise(nameof(PaintedGroundWarning));
            SayWhatIsUnsaved();
            BuildCollisionFromSurfaces();
            RebuildOverlay();
            SceneShown?.Invoke();
            Recorded();
        }

        public string PaintedGroundWarning { get; private set; }

        private void BuildCollisionFromSurfaces()
        {
            MapCollisionGrid walkable = new MapCollisionGrid();
            foreach (TornWorldSurfaces.Surface surface in _surfaces.Values)
            {
                walkable.Add(surface.PatchX, surface.PatchY, surface.Collisions);
                walkable.AddTypes(surface.PatchX, surface.PatchY, surface.Types);
            }
            Collision = walkable;
        }

        private readonly Dictionary<TornWorldFile.FloatingPlatform, (int attribute, int vertical, int horizontal)> _gridShapes
            = new Dictionary<TornWorldFile.FloatingPlatform, (int, int, int)>();

        private const int MostGridTiles = 256;

        private void RememberGridShapes()
        {
            _gridShapes.Clear();
            foreach (TornWorldFile.FloatingPlatform platform in Platforms)
                _gridShapes[platform] = (platform.AttributeId, platform.TilesVertical, platform.TilesHorizontal);
        }

        /// <summary>False when a value was refused and put back, so the rows need redrawing.</summary>
        public bool MarkEdited()
        {
            if (_selectedFloorIndex < 0 || _selectedFloorIndex >= Floors.Count) return true;

            string refused = null;
            bool gridsChanged = false;
            foreach (TornWorldFile.FloatingPlatform platform in Platforms)
            {
                if (!_gridShapes.TryGetValue(platform, out (int attribute, int vertical, int horizontal) was))
                {
                    _gridShapes[platform] = (platform.AttributeId, platform.TilesVertical, platform.TilesHorizontal);
                    continue;
                }

                if (platform.AttributeId != was.attribute)
                {
                    int grids = _attributes.Available ? _attributes.Count : 0;
                    if (platform.AttributeId < 0 || platform.AttributeId >= grids)
                    {
                        refused = $"There is no collision grid {platform.AttributeId}.";
                        platform.AttributeId = was.attribute;
                    }
                    else gridsChanged = true;
                }

                if (platform.TilesVertical != was.vertical || platform.TilesHorizontal != was.horizontal)
                {
                    if (platform.TilesVertical < 1 || platform.TilesHorizontal < 1
                        || platform.TilesVertical > MostGridTiles || platform.TilesHorizontal > MostGridTiles)
                    {
                        refused = $"Tile counts go from 1 to {MostGridTiles}.";
                        platform.TilesVertical = was.vertical;
                        platform.TilesHorizontal = was.horizontal;
                    }
                    else
                    {
                        ushort[] grid = Grid(platform.AttributeId);
                        if (grid != null)
                        {
                            _gridCache[platform.AttributeId] = TornWorldSurfaces.ResizeGrid(grid,
                                was.vertical, was.horizontal, platform.TilesVertical, platform.TilesHorizontal);
                            _editedGrids.Add(platform.AttributeId);
                        }
                        gridsChanged = true;
                    }
                }

                _gridShapes[platform] = (platform.AttributeId, platform.TilesVertical, platform.TilesHorizontal);
            }

            // One cell ends each edit, so a refused edit changed nothing and leaves the floor as it was.
            if (refused != null)
            {
                if (gridsChanged) RefreshSurfaces();
                StatusText = refused;
                return false;
            }

            _editedMembers.Add(Floors[_selectedFloorIndex].Floor.DataMember);
            Raise(nameof(HasUnsavedChanges));
            SayWhatIsUnsaved();
            if (gridsChanged) RefreshSurfaces();
            Recorded();
            return true;
        }

        private void RefreshSurfaces()
        {
            _shownGrids.Clear();
            foreach (TornWorldFile.FloatingPlatform platform in Platforms)
            {
                ushort[] grid = Grid(platform.AttributeId);
                if (grid != null) _shownGrids[platform.AttributeId] = grid;
            }

            int keep = _selectedPlatformIndex;
            CommitShownFloor();
            BuildScene(ShownFloors().Select(f => f.floor).ToList());
            FloorShown?.Invoke();
            _selectedPlatformIndex = keep >= 0 && keep < SurfaceRows.Count ? keep : (SurfaceRows.Count > 0 ? 0 : -1);
            Raise(nameof(SelectedPlatformIndex));
            ShowPlatform();
        }

        private void SayWhatIsUnsaved()
        {
            int floors = _editedMembers.Count, grids = _editedGrids.Count;
            if (floors + grids == 0) { StatusText = $"{Floors.Count} floors."; return; }
            StatusText = grids == 0 ? $"{Plural.Of(floors, "floor")} changed, not saved."
                   : floors == 0 ? $"{Plural.Of(grids, "grid")} changed, not saved."
                   : $"{Plural.Of(floors, "floor")} and {Plural.Of(grids, "grid")} changed, not saved.";
        }

        public bool HasUnsavedChanges =>
            _editedMembers.Count > 0 || _editedGrids.Count > 0 || _shapeChanged.Count > 0;
    }
}
