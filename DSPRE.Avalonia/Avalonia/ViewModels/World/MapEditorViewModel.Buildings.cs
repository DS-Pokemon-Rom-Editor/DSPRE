using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.Models;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class MapEditorViewModel
    {
        // In header view only the map that owns the selected building is marked dirty.
        private void MarkSelectedBuildingDirty()
        {
            if (IsHeaderView && _selectedBuildingIndex >= 0 && _selectedBuildingIndex < _headerBuildingIndex.Count)
            {
                (int ci, int _) = _headerBuildingIndex[_selectedBuildingIndex];
                if (ci >= 0 && ci < _headerCells.Count) _headerCells[ci].Dirty = true;
            }
            MarkDirty();
        }

        private int _selectedBuildingIndex = -1;
        public int SelectedBuildingIndex
        {
            get => _selectedBuildingIndex;
            set
            {
                if (!Set(ref _selectedBuildingIndex, value)) return;
                if (IsHeaderView && value >= 0 && value < _headerBuildingIndex.Count) SelectedHeaderMapIndex = _headerBuildingIndex[value].CellIndex;
                LoadBuildingDetail();
            }
        }

        public ObservableCollection<string> HeaderMapNames { get; } = new ObservableCollection<string>();

        private int _selectedHeaderMap = -1;
        // The header map that model editing acts on. It follows the last pick or paint.
        public int SelectedHeaderMapIndex
        {
            get => _selectedHeaderMap;
            set
            {
                if (value < -1 || value >= _headerCells.Count || !Set(ref _selectedHeaderMap, value)) return;
                OnPropertyChanged(nameof(CanEditModel));
                OnPropertyChanged(nameof(FocusedMapIndex));
            }
        }

        public bool CanEditModel => IsSingleMap ? _map != null
            : IsHeaderView && _selectedHeaderMap >= 0 && _selectedHeaderMap < _headerCells.Count;

        public int FocusedMapIndex => IsSingleMap ? _selectedMapIndex
            : IsHeaderView && _selectedHeaderMap >= 0 && _selectedHeaderMap < _headerCells.Count ? _headerCells[_selectedHeaderMap].MapIndex : -1;

        public bool PrepareModelEdit()
        {
            _modelAreaId = AreaIdForImport;
            if (IsSingleMap) return _map != null;
            if (!CanEditModel) return false;
            // Single-map fields follow the header map until EndModelEdit, for import checks and warp moves.
            HeaderMapCell cell = _headerCells[_selectedHeaderMap];
            _modelAreaId = cell.AreaId;
            _borrowed = (_map, _selectedMapIndex);
            _map = cell.Map;
            _selectedMapIndex = cell.MapIndex;
            OpenModelEditor(cell.MapIndex, cell.AreaId);
            return true;
        }

        private (MapFile map, int index)? _borrowed;
        private int _modelAreaId;

        public int AreaIdForImport => _area == null ? -1 : _areaId;

        // Import review, or null. Call while the imported map is open, as events check _map.
        public MapImportReviewViewModel ReviewImport((byte[,] collisions, byte[,] types) permissionsBefore, bool fromModelWindow)
        {
            if (_map == null) return null;
            int areaId = fromModelWindow ? _modelAreaId : AreaIdForImport;
            // A map no header uses has no area, so check the preview's pack.
            int pack = MapTilesetIndex;
            if (areaId >= 0)
            {
                try { pack = new AreaData((byte)areaId).MapTileset; }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException) { }   // without the area data the current tileset is kept
            }
            List<MapImportCheck.BuildingTrouble> buildings;
            try { buildings = MapImportCheck.Buildings(_map, areaId, GameFamily); }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException)
            {
                AppLogger.Error("Import review buildings: " + ex.Message);
                buildings = new List<MapImportCheck.BuildingTrouble>();
            }
            MapImportReviewViewModel review = new MapImportReviewViewModel(this, $"Map {_selectedMapIndex}", _map, areaId, pack,
                                                                           buildings, EventsOnUnwalkableSquares(permissionsBefore));
            return review.HasAny ? review : null;
        }

        public void EndModelEdit()
        {
            if (_borrowed is not (MapFile map, int index)) return;
            _borrowed = null;
            _map = map;
            _selectedMapIndex = index;
        }

        private void OpenModelEditor(int index, byte areaId)
        {
            WatchGeometry();
            MapModel.Open(_map, areaId, GameFamily, $"Map {index}");
            MapModel.Tiles.AreasOfMap = () => AreasUsingMap(index, out _);
            MapModel.Tiles.WarpsFollow = (was, dx, dz) => MoveWarpsWith(index, was, dx, dz);
            MapModel.Tiles.SelectedBuilding = -1;
            MapModel.HeadersOfMap = () => HeadersUsingMap(index).Select(h => h.header).Distinct().OrderBy(h => h).ToList();
        }

        public bool HasBuildingSelected => ResolveSelectedBuilding() != null;

        private decimal _bModelId, _bx, _by, _bz, _bRotX, _bRotY, _bRotZ, _bRawX, _bRawY, _bRawZ;
        public decimal BModelId { get => _bModelId; set { if (Set(ref _bModelId, value) && !_suppress) ApplyBuilding(reloadModel: true); } }
        public decimal BX { get => _bx; set { if (Set(ref _bx, value) && !_suppress) ApplyBuilding(); } }
        public decimal BY { get => _by; set { if (Set(ref _by, value) && !_suppress) ApplyBuilding(); } }
        public decimal BZ { get => _bz; set { if (Set(ref _bz, value) && !_suppress) ApplyBuilding(); } }
        // Each rotation axis is written only by its own boxes, so other edits keep the stored rotation.
        public decimal BRotX { get => _bRotX; set { if (Set(ref _bRotX, value) && !_suppress) SetRotation(0, DegToU16(value)); } }
        public decimal BRotY { get => _bRotY; set { if (Set(ref _bRotY, value) && !_suppress) SetRotation(1, DegToU16(value)); } }
        public decimal BRotZ { get => _bRotZ; set { if (Set(ref _bRotZ, value) && !_suppress) SetRotation(2, DegToU16(value)); } }
        public decimal BRawX { get => _bRawX; set { if (Set(ref _bRawX, value) && !_suppress) SetRotation(0, (ushort)value); } }
        public decimal BRawY { get => _bRawY; set { if (Set(ref _bRawY, value) && !_suppress) SetRotation(1, (ushort)value); } }
        public decimal BRawZ { get => _bRawZ; set { if (Set(ref _bRawZ, value) && !_suppress) SetRotation(2, (ushort)value); } }

        private static decimal Degrees(ushort raw) => Math.Round((decimal)raw * 360m / 65536m, 2);

        private void ShowRotation(Building b)
        {
            BRotX = Degrees(b.XRotation); BRotY = Degrees(b.YRotation); BRotZ = Degrees(b.ZRotation);
            BRawX = b.XRotation; BRawY = b.YRotation; BRawZ = b.ZRotation;
        }

        private void SetRotation(int axis, ushort raw)
        {
            (MapFile map, int cellX, int cellY, Building building)? resolved = ResolveSelectedBuilding();
            if (resolved == null) return;
            Building b = resolved.Value.building;
            if (axis == 0) b.XRotation = raw; else if (axis == 1) b.YRotation = raw; else b.ZRotation = raw;
            _suppress = true;
            ShowRotation(b);
            _suppress = false;
            ApplyBuilding();
        }

        // Scale is stored as sixteenths of the model's size, so each step is one stored unit.
        private const decimal ScaleUnit = 16m;
        private decimal _bScaleX = 1, _bScaleY = 1, _bScaleZ = 1;
        public decimal BScaleX { get => _bScaleX; set { if (Set(ref _bScaleX, value) && !_suppress) ApplyBuilding(); } }
        public decimal BScaleY { get => _bScaleY; set { if (Set(ref _bScaleY, value) && !_suppress) ApplyBuilding(); } }
        public decimal BScaleZ { get => _bScaleZ; set { if (Set(ref _bScaleZ, value) && !_suppress) ApplyBuilding(); } }
        private static decimal ScaleOf(uint stored) => stored / ScaleUnit;
        private static uint StoredScale(decimal scale) => (uint)Math.Clamp(Math.Round(scale * ScaleUnit), 1m, ushort.MaxValue);

        // The game reads rotation only with the Building rotation patch, so the controls wait for it.
        private bool _buildingRotationEnabled = true;
        public bool BuildingRotationEnabled { get => _buildingRotationEnabled; private set => Set(ref _buildingRotationEnabled, value); }

        public void RefreshBuildingRotationPatchState()
        {
            try
            {
                BuildingRotationEnabled = RomPatchState.FlagBuildingRotationPatchApplied || PatchToolboxLogic.CheckFilesBuildingRotationPatchApplied();
            }
            catch (Exception ex)
            {
                AppLogger.WarnOnce("MapEditorViewModel.RefreshBuildingRotationPatchState", ex);
                BuildingRotationEnabled = false;
            }
        }

        private void OnRomPatchStateChanged(object sender, EventArgs e) => RefreshBuildingRotationPatchState();

        // Only a standalone popup closing calls this. The Maps workspace instance never does.
        public void Detach()
        {
            AppEvents.RomPatchStateChanged -= OnRomPatchStateChanged;
            AppEvents.MapSaved -= OnMapSavedElsewhere;
            AppEvents.AreaDataSaved -= OnAreaSavedElsewhere;
            AppEvents.EventFileSaved -= OnEventFileSavedElsewhere;
        }

        private void OnMapSavedElsewhere(object sender, int mapIndex)
        {
            if (ReferenceEquals(sender, this)) return;
            bool shown = IsSingleMap ? _selectedMapIndex == mapIndex : IsHeaderView && _headerCells.Any(c => c.MapIndex == mapIndex);
            if (!shown) return;
            if (HasUnsavedChanges) { StatusText = $"Map {mapIndex} was saved elsewhere: save or discard here to see it."; return; }
            if (IsSingleMap) LoadMap(mapIndex);
            else BuildHeaderPreview();
            StatusText = "";
        }

        private void LoadBuildingDetail()
        {
            OnPropertyChanged(nameof(HasBuildingSelected));
            (MapFile map, int cellX, int cellY, Building building)? resolved = ResolveSelectedBuilding();
            if (resolved == null) { GizmoTargetChanged?.Invoke(this, EventArgs.Empty); return; }
            Building b = resolved.Value.building;
            _suppress = true;
            // Positions show the full fractional tile, so the boxes can fine-tune after a snap-drag.
            BModelId = b.ModelID; BX = Coord(b.XPosition, b.XFraction); BY = Coord(b.YPosition, b.YFraction); BZ = Coord(b.ZPosition, b.ZFraction);
            ShowRotation(b);
            BScaleX = ScaleOf(b.Width); BScaleY = ScaleOf(b.Height); BScaleZ = ScaleOf(b.Length);
            _suppress = false;
            GizmoTargetChanged?.Invoke(this, EventArgs.Empty);
        }

        private static decimal Coord(short pos, ushort frac) => pos + (decimal)frac / 65536m;
        private static (short pos, ushort frac) SplitCoord(decimal v)
        {
            decimal fl = Math.Floor(v);
            int pos = Math.Max(short.MinValue, Math.Min(short.MaxValue, (int)fl));
            int frac = (int)Math.Round((double)((v - fl) * 65536m));
            if (frac >= 65536) { frac = 0; pos = Math.Min(short.MaxValue, pos + 1); }
            if (frac < 0) frac = 0;
            return ((short)pos, (ushort)frac);
        }

        private static ushort DegToU16(decimal deg) => (ushort)((int)Math.Round((deg % 360m + 360m) % 360m * 65536m / 360m) & 0xFFFF);

        private void ApplyBuilding(bool reloadModel = false)
        {
            (MapFile map, int cellX, int cellY, Building building)? resolved = ResolveSelectedBuilding();
            if (resolved == null) return;
            Building b = resolved.Value.building;
            b.ModelID = (uint)_bModelId;
            (short px, ushort fx) = SplitCoord(_bx); (short py, ushort fy) = SplitCoord(_by); (short pz, ushort fz) = SplitCoord(_bz);
            b.XPosition = px; b.XFraction = fx; b.YPosition = py; b.YFraction = fy; b.ZPosition = pz; b.ZFraction = fz;
            // A stored 0 shows as the smallest box value; only an edit to a scale box writes it back.
            if (StoredScale(_bScaleX) != Math.Max(1u, b.Width)) b.Width = StoredScale(_bScaleX);
            if (StoredScale(_bScaleY) != Math.Max(1u, b.Height)) b.Height = StoredScale(_bScaleY);
            if (StoredScale(_bScaleZ) != Math.Max(1u, b.Length)) b.Length = StoredScale(_bScaleZ);
            if (reloadModel) b.NSBMDFile = null;   // force reload of the (new) model in BuildPreview
            MaybeTransferBuildingAcrossCells();
            MarkSelectedBuildingDirty();
            // Buildings list not refreshed: replacing the selected item drops the selection.
            RebuildPreview();
            GizmoTargetChanged?.Invoke(this, EventArgs.Empty);
        }

        // Positions are tiles from the map's centre, so a map spans -16 to 16.
        public static int CellsCrossed(short position, ushort fraction)
        {
            int half = MapFile.MapSize / 2;
            double at = position + fraction / 65536.0;
            if (at > half) return (int)Math.Ceiling((at - half) / MapFile.MapSize);
            if (at < -half) return -(int)Math.Ceiling((-half - at) / MapFile.MapSize);
            return 0;
        }

        // A map reads only its own buildings, so one moved past the edge goes to the neighbouring map.
        private void MaybeTransferBuildingAcrossCells()
        {
            if (!IsHeaderView || _selectedBuildingIndex < 0 || _selectedBuildingIndex >= _headerBuildingIndex.Count) return;
            (int ci, int bi) = _headerBuildingIndex[_selectedBuildingIndex];
            if (ci < 0 || ci >= _headerCells.Count) return;
            HeaderMapCell srcCell = _headerCells[ci];
            if (srcCell.Map?.Buildings == null || bi < 0 || bi >= srcCell.Map.Buildings.Count) return;
            Building b = srcCell.Map.Buildings[bi];

            int dCellX = CellsCrossed(b.XPosition, b.XFraction);
            int dCellZ = CellsCrossed(b.ZPosition, b.ZFraction);
            if (dCellX == 0 && dCellZ == 0) return;   // still within its own map's tile bounds

            // Tries the X, then Z neighbour before the diagonal: most buildings overhang one edge.
            int? Find(int x, int y) { int idx = _headerCells.FindIndex(c => c.CellX == x && c.CellY == y); return idx >= 0 && idx != ci && _headerCells[idx].Map?.Buildings != null ? idx : (int?)null; }

            int targetIndex; int usedDCellX, usedDCellZ;
            if (dCellX != 0 && Find(srcCell.CellX + dCellX, srcCell.CellY) is int xi) { targetIndex = xi; usedDCellX = dCellX; usedDCellZ = 0; }
            else if (dCellZ != 0 && Find(srcCell.CellX, srcCell.CellY + dCellZ) is int zi) { targetIndex = zi; usedDCellX = 0; usedDCellZ = dCellZ; }
            else if (dCellX != 0 && dCellZ != 0 && Find(srcCell.CellX + dCellX, srcCell.CellY + dCellZ) is int di) { targetIndex = di; usedDCellX = dCellX; usedDCellZ = dCellZ; }
            else return;   // no map exists in a direction that actually matches how this building moved

            HeaderMapCell targetCell = _headerCells[targetIndex];
            srcCell.Map.Buildings.RemoveAt(bi);
            b.XPosition = (short)(b.XPosition - usedDCellX * MapFile.MapSize);
            b.ZPosition = (short)(b.ZPosition - usedDCellZ * MapFile.MapSize);
            targetCell.Map.Buildings.Add(b);
            srcCell.Dirty = true; targetCell.Dirty = true;

            RefreshBuildings();
            SelectedBuildingIndex = _headerBuildingIndex.FindLastIndex(t => t.CellIndex == targetIndex);
            StatusText = $"Building moved from map {srcCell.MapIndex} to map {targetCell.MapIndex}.";
        }

        private int TargetHeaderCellIndex()
        {
            if (_selectedBuildingIndex >= 0 && _selectedBuildingIndex < _headerBuildingIndex.Count)
                return _headerBuildingIndex[_selectedBuildingIndex].CellIndex;
            return 0;
        }

        public void AddBuilding()
        {
            if (IsHeaderView)
            {
                if (_headerCells.Count == 0) return;
                HeaderMapCell cell = _headerCells[TargetHeaderCellIndex()];
                if (cell.Map.Buildings == null) return;
                cell.Map.Buildings.Add(new Building());
                cell.Dirty = true;
                RefreshBuildings();
                MarkDirty();
                SelectedBuildingIndex = _headerBuildingIndex.FindLastIndex(t => t.CellIndex == _headerCells.IndexOf(cell));
                RebuildPreview();
                return;
            }
            if (_map?.Buildings == null) return;
            _map.Buildings.Add(new Building());
            RefreshBuildings();
            MarkDirty();
            SelectedBuildingIndex = _map.Buildings.Count - 1;
            RebuildPreview();
        }

        public void RemoveBuilding()
        {
            if (!HasBuildingSelected) return;
            if (IsHeaderView)
            {
                (int ci, int bi) = _headerBuildingIndex[_selectedBuildingIndex];
                HeaderMapCell cell = _headerCells[ci];
                cell.Map.Buildings.RemoveAt(bi);
                cell.Dirty = true;
                RefreshBuildings();
                MarkDirty();
                SelectedBuildingIndex = Buildings.Count > 0 ? Math.Min(_selectedBuildingIndex, Buildings.Count - 1) : -1;
                RebuildPreview();
                return;
            }
            _map.Buildings.RemoveAt(_selectedBuildingIndex);
            RefreshBuildings();
            MarkDirty();
            SelectedBuildingIndex = _map.Buildings.Count > 0 ? Math.Min(_selectedBuildingIndex, _map.Buildings.Count - 1) : -1;
            RebuildPreview();
        }

        private bool _editMode3D;
        public bool EditMode3D
        {
            get => _editMode3D;
            set { if (Set(ref _editMode3D, value)) { OnPropertyChanged(nameof(EditMode3D)); EditModeChanged?.Invoke(this, EventArgs.Empty); } }
        }
        public event EventHandler EditModeChanged;
        public event EventHandler GizmoTargetChanged;

        public bool TryBuildingAnchorNorm(int index, out float nx, out float ny, out float nz)
        {
            nx = ny = nz = 0f;
            (MapFile map, int cellX, int cellY, Building building)? resolved = ResolveBuildingAt(index);
            if (Model3D == null || resolved == null) return false;
            (MapFile _, int cellX, int cellY, Building b) = resolved.Value;
            // The gizmo anchor needs the cell's origin offset too, or it lands north-west of the building.
            float offX = 0f, offZ = 0f;
            if (Model3D.TryCellPlacement(cellX, cellY, out NsbmdRenderModel.CellPlacement cp))
            {
                offX = cp.OriginX + NsbmdGeometry.MapStride * 0.5f;
                offZ = cp.OriginZ + NsbmdGeometry.MapStride * 0.5f;
            }
            float rx = 0.25f * (b.XPosition + b.XFraction / 65536f) + offX;
            float ry = 0.25f * (b.YPosition + b.YFraction / 65536f);
            float rz = 0.25f * (b.ZPosition + b.ZFraction / 65536f) + offZ;
            (float a, float c, float d) = Model3D.ToNormalized(rx, ry, rz);
            nx = a; ny = c; nz = d;
            return true;
        }

        public bool TrySelectedBuildingAnchorNorm(out float nx, out float ny, out float nz)
            => TryBuildingAnchorNorm(_selectedBuildingIndex, out nx, out ny, out nz);

        public int BuildingAlong(float ox, float oy, float oz, float dx, float dy, float dz)
        {
            if (Model3D == null) return -1;
            Building nearest = null;
            float best = float.MaxValue;
            foreach (NsbmdRenderModel.BuildingMaterials placed in Model3D.Buildings)
            {
                if (placed.Source == null) continue;
                foreach (NsbmdMeshPart part in Model3D.Parts)
                {
                    if (part.MaterialIndex < placed.FirstKey || part.MaterialIndex >= placed.FirstKey + placed.Count || part.Vertices == null) continue;
                    float[] v = part.Vertices;
                    for (int t = 0; t + 2 < part.VertexCount; t += 3)
                    {
                        int a = t * 8, b = a + 8, c = a + 16;
                        if (RayHitsTriangle(ox, oy, oz, dx, dy, dz, v[a], v[a + 1], v[a + 2], v[b], v[b + 1], v[b + 2], v[c], v[c + 1], v[c + 2], out float d) && d < best)
                        { best = d; nearest = placed.Source; }
                    }
                }
            }
            if (nearest == null) return -1;
            for (int i = 0; i < BuildingCount; i++)
                if (ResolveBuildingAt(i) is (MapFile _, int _, int _, Building b) && b == nearest) return i;
            return -1;
        }

        // Moller-Trumbore, both faces.
        private static bool RayHitsTriangle(float ox, float oy, float oz, float dx, float dy, float dz,
            float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz, out float t)
        {
            t = 0f;
            float e1x = bx - ax, e1y = by - ay, e1z = bz - az, e2x = cx - ax, e2y = cy - ay, e2z = cz - az;
            float px = dy * e2z - dz * e2y, py = dz * e2x - dx * e2z, pz = dx * e2y - dy * e2x;
            float det = e1x * px + e1y * py + e1z * pz;
            if (Math.Abs(det) < 1e-9f) return false;
            float inv = 1f / det;
            float sx = ox - ax, sy = oy - ay, sz = oz - az;
            float u = (sx * px + sy * py + sz * pz) * inv;
            if (u < 0f || u > 1f) return false;
            float qx = sy * e1z - sz * e1y, qy = sz * e1x - sx * e1z, qz = sx * e1y - sy * e1x;
            float w = (dx * qx + dy * qy + dz * qz) * inv;
            if (w < 0f || u + w > 1f) return false;
            t = (e2x * qx + e2y * qy + e2z * qz) * inv;
            return t > 0f;
        }

        public IReadOnlyList<NsbmdGlControl.HighlightBatch> SelectedBuildingHighlight()
        {
            if (Model3D == null || ResolveSelectedBuilding() is not (MapFile _, int _, int _, Building building)) return null;
            NsbmdRenderModel.BuildingMaterials placed = Model3D.Buildings.FirstOrDefault(b => b.Source == building);
            if (placed == null) return null;
            List<NsbmdGlControl.HighlightBatch> batches = new List<NsbmdGlControl.HighlightBatch>();
            foreach (NsbmdMeshPart part in Model3D.Parts)
                if (part.MaterialIndex >= placed.FirstKey && part.MaterialIndex < placed.FirstKey + placed.Count && part.Vertices != null)
                    batches.Add(new NsbmdGlControl.HighlightBatch { MaterialKey = part.MaterialIndex, Mesh = part.Vertices.Length == part.VertexCount * 8 ? part.Vertices : part.Vertices.Take(part.VertexCount * 8).ToArray(), R = 2.2f, G = 1.9f, B = 0.4f });
            return batches;
        }

        public (string ModelDir, string TexturePack)? BuildingModelSource()
        {
            if (ResolveSelectedBuilding() == null) return null;
            byte? area = null;
            int pack = -1;
            if (IsHeaderView && _selectedBuildingIndex >= 0 && _selectedBuildingIndex < _headerBuildingIndex.Count)
            {
                int ci = _headerBuildingIndex[_selectedBuildingIndex].CellIndex;
                if (ci >= 0 && ci < _headerCells.Count) area = _headerCells[ci].AreaId;
            }
            else
            {
                area = AreaForMap(_selectedMapIndex);
                pack = _buildingTilesetIndex - 1;
            }
            DirNames models = DirNames.ExteriorBuildingModels;
            if (area is byte aid)
                try
                {
                    AreaData data = new AreaData(aid);
                    models = data.BuildingModels;
                    if (IsHeaderView) pack = data.BuildingsTileset;
                }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException) { AppLogger.Warn($"Area {aid} unreadable: {ex.Message}"); }
            string texture = pack >= 0 ? ArchiveMembers.PathOf(DirNames.BuildingTextures, pack) : null;
            return (GameDirs[models].unpackedDir, texture);
        }

        public static float[] OutlineOf(IReadOnlyList<NsbmdGlControl.HighlightBatch> batches)
        {
            if (batches == null || batches.Count == 0) return null;
            float x0 = float.MaxValue, y0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue, z1 = float.MinValue;
            foreach (NsbmdGlControl.HighlightBatch b in batches)
                for (int i = 0; i + 2 < b.Mesh.Length; i += 8)
                {
                    x0 = Math.Min(x0, b.Mesh[i]); x1 = Math.Max(x1, b.Mesh[i]);
                    y0 = Math.Min(y0, b.Mesh[i + 1]); y1 = Math.Max(y1, b.Mesh[i + 1]);
                    z0 = Math.Min(z0, b.Mesh[i + 2]); z1 = Math.Max(z1, b.Mesh[i + 2]);
                }
            if (x0 > x1) return null;
            float w = Math.Max(Math.Max(x1 - x0, y1 - y0), z1 - z0) * 0.012f;
            List<float> v = new List<float>(12 * 12 * 8);
            void Vtx(float x, float y, float z) { v.Add(x); v.Add(y); v.Add(z); v.Add(0); v.Add(0); v.Add(1f); v.Add(0.84f); v.Add(0.24f); }
            void Edge(float ax, float ay, float az, float bx, float by, float bz)
            {
                bool alongX = ax != bx, alongY = ay != by;
                (float, float, float)[] sides = alongX ? new[] { (0f, w, 0f), (0f, 0f, w) }
                    : alongY ? new[] { (w, 0f, 0f), (0f, 0f, w) } : new[] { (w, 0f, 0f), (0f, w, 0f) };
                foreach ((float sx, float sy, float sz) in sides)
                {
                    Vtx(ax - sx, ay - sy, az - sz); Vtx(bx - sx, by - sy, bz - sz); Vtx(bx + sx, by + sy, bz + sz);
                    Vtx(ax - sx, ay - sy, az - sz); Vtx(bx + sx, by + sy, bz + sz); Vtx(ax + sx, ay + sy, az + sz);
                }
            }
            foreach (float y in new[] { y0, y1 })
            {
                Edge(x0, y, z0, x1, y, z0); Edge(x1, y, z0, x1, y, z1); Edge(x1, y, z1, x0, y, z1); Edge(x0, y, z1, x0, y, z0);
            }
            foreach ((float x, float z) in new[] { (x0, z0), (x1, z0), (x1, z1), (x0, z1) }) Edge(x, y0, z, x, y1, z);
            return v.ToArray();
        }

        public float ModelScale => Model3D?.Scale ?? 1f;

        public void NudgeSelectedBuildingRaw(int axis, float rawDelta)
        {
            (MapFile map, int cellX, int cellY, Building building)? resolved = ResolveSelectedBuilding();
            if (resolved == null || rawDelta == 0f) return;
            Building b = resolved.Value.building;
            double tileDelta = rawDelta / 0.25;   // 1 position unit = 0.25 raw units
            switch (axis)
            {
                case 0: { (short p, ushort f) = AddTiles(b.XPosition, b.XFraction, tileDelta); b.XPosition = p; b.XFraction = f; } break;
                case 1: { (short p, ushort f) = AddTiles(b.YPosition, b.YFraction, tileDelta); b.YPosition = p; b.YFraction = f; } break;
                case 2: { (short p, ushort f) = AddTiles(b.ZPosition, b.ZFraction, tileDelta); b.ZPosition = p; b.ZFraction = f; } break;
            }
            if (_snapToTile) SnapAxis(b, axis);
            AfterBuildingMoved(b);
        }

        private bool _snapToTile;
        public bool SnapToTile { get => _snapToTile; set => Set(ref _snapToTile, value); }

        public void NudgeSelectedBuildingTiles(int dx, int dz)
        {
            (MapFile map, int cellX, int cellY, Building building)? resolved = ResolveSelectedBuilding();
            if (resolved == null) return;
            Building b = resolved.Value.building;
            if (dx != 0) { b.XPosition = (short)(b.XPosition + dx); b.XFraction = 0; }
            if (dz != 0) { b.ZPosition = (short)(b.ZPosition + dz); b.ZFraction = 0; }
            AfterBuildingMoved(b);
        }

        private static void SnapAxis(Building b, int axis)
        {
            switch (axis)
            {
                case 0: b.XPosition = (short)Math.Round(b.XPosition + b.XFraction / 65536.0); b.XFraction = 0; break;
                case 1: b.YPosition = (short)Math.Round(b.YPosition + b.YFraction / 65536.0); b.YFraction = 0; break;
                case 2: b.ZPosition = (short)Math.Round(b.ZPosition + b.ZFraction / 65536.0); b.ZFraction = 0; break;
            }
        }
         
        private void AfterBuildingMoved(Building b)
        {
            _suppress = true;
            BX = Coord(b.XPosition, b.XFraction); BY = Coord(b.YPosition, b.YFraction); BZ = Coord(b.ZPosition, b.ZFraction);
            _suppress = false;
            MaybeTransferBuildingAcrossCells();
            MarkSelectedBuildingDirty();
            RebuildPreview();
            GizmoTargetChanged?.Invoke(this, EventArgs.Empty);
        }

        private static (short pos, ushort frac) AddTiles(short pos, ushort frac, double tileDelta)
        {
            double cur = pos + frac / 65536.0 + tileDelta;
            double fl = Math.Floor(cur);
            int ip = (int)fl;
            int f = (int)Math.Round((cur - fl) * 65536.0);
            if (f >= 65536) { f -= 65536; ip++; }
            if (f < 0) { f += 65536; ip--; }
            ip = Math.Max(short.MinValue, Math.Min(short.MaxValue, ip));
            return ((short)ip, (ushort)f);
        }
    }
}
