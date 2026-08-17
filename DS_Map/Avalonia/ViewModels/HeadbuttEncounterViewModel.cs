using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>One headbutt-tree wild encounter slot: a species + level range.</summary>
    public sealed class HeadbuttEncRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void On(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        public string Name { get; }
        public ObservableCollection<string> Species { get; }
        private readonly HeadbuttEncounter _e;
        private readonly Action _changed;
        public HeadbuttEncRow(string name, HeadbuttEncounter e, ObservableCollection<string> species, Action changed)
        { Name = name; _e = e; Species = species; _changed = changed; }
        public int SpeciesIndex { get => _e.pokemonID; set { if (_e.pokemonID == value) return; _e.pokemonID = (ushort)value; On(nameof(SpeciesIndex)); _changed(); } }
        public decimal MinLevel { get => _e.minLevel; set { if (_e.minLevel == value) return; _e.minLevel = (byte)value; On(nameof(MinLevel)); _changed(); } }
        public decimal MaxLevel { get => _e.maxLevel; set { if (_e.maxLevel == value) return; _e.maxLevel = (byte)value; On(nameof(MaxLevel)); _changed(); } }
    }

    /// <summary>One tree's global (x,y) position within a tree group.</summary>
    public sealed class HeadbuttTreeRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void On(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        public string Name { get; }
        private readonly HeadbuttTree _t;
        private readonly Action _changed;
        public HeadbuttTreeRow(string name, HeadbuttTree t, Action changed) { Name = name; _t = t; _changed = changed; }
        internal HeadbuttTree Tree => _t;
        public decimal GlobalX { get => _t.globalX; set { if (_t.globalX == value) return; _t.globalX = (ushort)value; OnAll(); } }
        public decimal GlobalY { get => _t.globalY; set { if (_t.globalY == value) return; _t.globalY = (ushort)value; OnAll(); } }
        // Matrix-cell + in-map-tile breakdown (globalX = matrixX*32 + mapX) — the same coordinates the
        // 3D map view and the rest of the editor use, so placement is human-readable.
        public decimal MatrixX { get => _t.matrixX; set { if (_t.matrixX == value) return; _t.matrixX = (ushort)value; OnAll(); } }
        public decimal MatrixY { get => _t.matrixY; set { if (_t.matrixY == value) return; _t.matrixY = (ushort)value; OnAll(); } }
        public decimal MapX { get => _t.mapX; set { if (_t.mapX == value) return; _t.mapX = (ushort)value; OnAll(); } }
        public decimal MapY { get => _t.mapY; set { if (_t.mapY == value) return; _t.mapY = (ushort)value; OnAll(); } }
        public void RaiseAll() => OnAll();
        private void OnAll()
        {
            On(nameof(GlobalX)); On(nameof(GlobalY)); On(nameof(MatrixX)); On(nameof(MatrixY)); On(nameof(MapX)); On(nameof(MapY));
            _changed();
        }
    }

    /// <summary>
    /// Avalonia port of the WinForms <c>HeadbuttEncounterEditor</c> — data scope (HGSS). Edits a
    /// headbutt encounter file: the 12 normal + 6 special wild-encounter slots, and the normal /
    /// special tree groups (each a set of trees positioned by a global x/y). The on-map 3D tree
    /// placement from WinForms is deferred; coordinates are editable numerically.
    /// </summary>
    public class HeadbuttEncounterViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private HeadbuttEncounterFile _file;

        public ObservableCollection<string> FileNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Species { get; } = new ObservableCollection<string>();
        public ObservableCollection<HeadbuttEncRow> NormalEncounters { get; } = new ObservableCollection<HeadbuttEncRow>();
        public ObservableCollection<HeadbuttEncRow> SpecialEncounters { get; } = new ObservableCollection<HeadbuttEncRow>();
        public ObservableCollection<string> NormalTreeGroups { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SpecialTreeGroups { get; } = new ObservableCollection<string>();
        public ObservableCollection<HeadbuttTreeRow> SelectedGroupTrees { get; } = new ObservableCollection<HeadbuttTreeRow>();

        private bool _available;
        public bool IsAvailable { get => _available; private set => Set(ref _available, value); }

        private int _selFile = -1;
        public int SelectedFileIndex { get => _selFile; set { if (Set(ref _selFile, value) && !_suppress && value >= 0) LoadFile(value); } }

        private bool _specialGroupActive;
        private int _selGroup = -1;
        public int SelectedNormalGroupIndex { get => _specialGroupActive ? -1 : _selGroup; set { if (value >= 0) { _specialGroupActive = false; _selGroup = value; ShowGroupTrees(); } } }
        public int SelectedSpecialGroupIndex { get => _specialGroupActive ? _selGroup : -1; set { if (value >= 0) { _specialGroupActive = true; _selGroup = value; ShowGroupTrees(); } } }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Headbutt file {_selFile}";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); if (_selFile >= 0) LoadFile(_selFile); }
        private void Dirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void OnTreeChanged() { Dirty(); RefreshTreeMarkers(); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public HeadbuttEncounterViewModel() { }
        public HeadbuttEncounterViewModel(bool _) { }

        /// <summary>Headbutt file to open once loaded (set before SetupAsync; e.g. from a "Go to Headbutt #N" jump).</summary>
        public int InitialIndex { get; set; }

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                if (gameFamily != GameFamilies.HGSS)
                {
                    StatusText = "Headbutt encounters are HeartGold/SoulSilver only.";
                    return;
                }
                IsAvailable = true;
                DSUtils.TryUnpackNarcs(new List<DirNames> {
                    DirNames.headbutt, DirNames.maps, DirNames.matrices, DirNames.areaData,
                    DirNames.dynamicHeaders, DirNames.exteriorBuildingModels, DirNames.interiorBuildingModels,
                    DirNames.buildingTextures, DirNames.mapTextures });
                foreach (var n in GetPokemonNames()) Species.Add(n);
                int count = Filesystem.GetHeadbuttCount();
                for (int i = 0; i < count; i++) FileNames.Add("Headbutt File " + i);
                StatusText = $"{count} headbutt files.";
                if (count > 0) SelectedFileIndex = System.Math.Clamp(InitialIndex, 0, count - 1);
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowError($"Failed to set up Headbutt Editor:\n{ex.Message}", "Headbutt Editor");
            }
        }

        private void LoadFile(int index)
        {
            try
            {
                // Headbutt isn't one of DSPRE's owned domains for the packed NARC, so the vanilla read
                // would show a stale packed-ROM snapshot rather than the checkout's real data/Headbutt.c.
                if (HgEngineProject.IsActive)
                {
                    if (!HgEngineHeadbutt.TryLoad(index, out _file, out string err))
                    {
                        _file = new HeadbuttEncounterFile();
                        AppLogger.Error($"hg-engine headbutt read failed (file {index}): {err}");
                    }
                }
                else
                {
                    _file = new HeadbuttEncounterFile((ushort)index);
                }
                NormalEncounters.Clear();
                for (int i = 0; i < _file.normalEncounters.Count; i++)
                    NormalEncounters.Add(new HeadbuttEncRow($"Normal {i + 1}", _file.normalEncounters[i], Species, Dirty));
                SpecialEncounters.Clear();
                for (int i = 0; i < _file.specialEncounters.Count; i++)
                    SpecialEncounters.Add(new HeadbuttEncRow($"Special {i + 1}", _file.specialEncounters[i], Species, Dirty));
                RefreshGroups();
                SetClean();
                StatusText = $"Loaded headbutt file {index} ({_file.normalTreeGroups.Count} normal / {_file.specialTreeGroups.Count} special tree groups).";
                OnPropertyChanged(nameof(UnsavedChangesDescription));
                // Resolve + render the map ONCE per file, exactly like the event editor (full matrix when
                // small, else the bounding box of all trees). Group selection only re-marks, never rebuilds.
                ResolveMatrix();
                DisplayMap();
                if (_file.normalTreeGroups.Count > 0) SelectedNormalGroupIndex = 0;
                else if (_file.specialTreeGroups.Count > 0) SelectedSpecialGroupIndex = 0;
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Failed to load headbutt file {index}:\n{ex.Message}", "Headbutt Editor"); }
        }

        private void RefreshGroups()
        {
            NormalTreeGroups.Clear();
            for (int i = 0; i < _file.normalTreeGroups.Count; i++) NormalTreeGroups.Add($"Normal group {i} ({_file.normalTreeGroups[i].trees.Count} trees)");
            SpecialTreeGroups.Clear();
            for (int i = 0; i < _file.specialTreeGroups.Count; i++) SpecialTreeGroups.Add($"Special group {i} ({_file.specialTreeGroups[i].trees.Count} trees)");
            SelectedGroupTrees.Clear();
        }

        private void ShowGroupTrees()
        {
            SelectedGroupTrees.Clear();
            if (_file == null || _selGroup < 0) return;
            var groups = _specialGroupActive ? _file.specialTreeGroups : _file.normalTreeGroups;
            if (_selGroup >= groups.Count) return;
            var trees = groups[_selGroup].trees;
            // Only show USED tree slots — empty slots are the 65535/65535 sentinel and just look like
            // broken numbers. The slot index is kept in the name so it's traceable. Add/Remove tree
            // activates/clears a slot.
            for (int i = 0; i < trees.Count; i++)
                if (!trees[i].IsUnused)
                    SelectedGroupTrees.Add(new HeadbuttTreeRow($"Tree (slot {i + 1})", trees[i], OnTreeChanged));
            _selTree = -1;
            OnPropertyChanged(nameof(SelectedNormalGroupIndex));
            OnPropertyChanged(nameof(SelectedSpecialGroupIndex));
            OnPropertyChanged(nameof(SelectedTreeIndex));
            OnPropertyChanged(nameof(GroupTreeSummary));
            RefreshTreeMarkers();   // the map is already built for the whole file — only re-mark this group
        }

        public string GroupTreeSummary
        {
            get
            {
                if (_file == null || _selGroup < 0) return "";
                var groups = _specialGroupActive ? _file.specialTreeGroups : _file.normalTreeGroups;
                if (_selGroup >= groups.Count) return "";
                int used = 0, total = groups[_selGroup].trees.Count;
                foreach (var t in groups[_selGroup].trees) if (!t.IsUnused) used++;
                return $"{used} / {total} slots used";
            }
        }

        private HeadbuttTreeGroup CurrentGroup()
        {
            if (_file == null || _selGroup < 0) return null;
            var groups = _specialGroupActive ? _file.specialTreeGroups : _file.normalTreeGroups;
            return _selGroup < groups.Count ? groups[_selGroup] : null;
        }

        /// <summary>Activates the first empty (unused) slot in the current group, placing it on an existing
        /// tree's cell (or 0,0), so it shows up as a real, editable tree.</summary>
        public void AddTree()
        {
            var g = CurrentGroup();
            if (g == null) return;
            HeadbuttTree slot = null;
            foreach (var t in g.trees) if (t.IsUnused) { slot = t; break; }
            if (slot == null) { StatusText = "All tree slots in this group are in use."; return; }
            ushort gx = 0, gy = 0;
            foreach (var t in g.trees) if (!t.IsUnused) { gx = t.globalX; gy = t.globalY; break; }
            slot.globalX = gx; slot.globalY = gy;
            Dirty();
            ShowGroupTrees();
            SelectedTreeIndex = SelectedGroupTrees.Count - 1;
        }

        /// <summary>Clears the selected tree back to an empty (unused) slot.</summary>
        public void RemoveSelectedTree()
        {
            if (_selTree < 0 || _selTree >= SelectedGroupTrees.Count) return;
            var t = SelectedGroupTrees[_selTree].Tree;
            t.globalX = ushort.MaxValue; t.globalY = ushort.MaxValue;
            Dirty();
            ShowGroupTrees();
        }

        // ── 3D map view + tree markers (mirrors the event editor's matrix pipeline) ───────
        public NsbmdRenderModel Model3D { get; private set; }
        public float[] MarkerMesh { get; private set; }
        public int MarkerVertexCount { get; private set; }
        public event EventHandler MapLoaded;
        public event EventHandler MarkersChanged;
        public string MapInfo { get => _mapInfo; private set => Set(ref _mapInfo, value); }
        private string _mapInfo = "";

        private GameMatrix _matrix;
        private int _matrixId = -1;
        private byte _areaDataId;
        private const int MapTiles = 32;

        private int _headerId = -1;

        /// <summary>Resolves this headbutt file's header → matrix + area EXACTLY like the WinForms editor:
        /// the headbutt file index IS the header number (MapHeader.GetMapHeader(fileIndex)).</summary>
        private void ResolveMatrix()
        {
            _matrix = null; _matrixId = -1; _areaDataId = 0; _headerId = -1;
            try
            {
                var hdr = MapHeader.GetMapHeader((ushort)_selFile);
                if (hdr != null)
                {
                    _headerId = hdr.ID;
                    _matrixId = hdr.matrixID;
                    _areaDataId = hdr.areaDataID;
                    _matrix = new GameMatrix(hdr.matrixID);
                }
            }
            catch (Exception ex) { AppLogger.Error("Headbutt matrix resolve failed (file " + _selFile + "): " + ex.Message); }
        }

        /// <summary>Builds the 3D scene EXACTLY like the WinForms headbutt editor: render the matrix cells
        /// that belong to THIS header (where matrix.headers[y,x] == header.ID, or every non-empty cell if
        /// the matrix has no headers section), plus any cell a tree sits on. Built once per file.</summary>
        private void DisplayMap()
        {
            Model3D = null;
            try
            {
                if (_matrix == null) { MapInfo = "No header/matrix for this headbutt file."; MapLoaded?.Invoke(this, EventArgs.Empty); RefreshTreeMarkers(); return; }

                var include = HeaderCells();
                Model3D = MatrixSceneBuilder.Build(_matrix, _areaDataId, gameFamily, areaForMap: null, includeCells: include);
                MapInfo = Model3D != null
                    ? $"Header {_headerId} · matrix {_matrixId} · {include.Count} maps · area {_areaDataId}"
                    : $"Header {_headerId} · matrix {_matrixId}: no renderable maps.";
            }
            catch (Exception ex) { MapInfo = "Map render failed: " + ex.Message; AppLogger.Error("Headbutt map render failed: " + ex.Message); }
            MapLoaded?.Invoke(this, EventArgs.Empty);
            RefreshTreeMarkers();
        }

        /// <summary>The matrix cells this header owns (headers[y,x]==header.ID, or all non-empty cells when
        /// the matrix has no headers section) plus every cell a tree occupies — the WinForms map set.</summary>
        private HashSet<(int x, int y)> HeaderCells()
        {
            var set = new HashSet<(int x, int y)>();
            if (_matrix == null) return set;
            for (int y = 0; y < _matrix.height; y++)
                for (int x = 0; x < _matrix.width; x++)
                {
                    if (_matrix.maps[y, x] == GameMatrix.EMPTY) continue;
                    if (_matrix.hasHeadersSection && _matrix.headers[y, x] != _headerId) continue;
                    set.Add((x, y));
                }
            if (_file != null)
            {
                void NoteTrees(HeadbuttTreeGroup g)
                {
                    foreach (var t in g.trees)
                    {
                        if (t.IsUnused || t.matrixX >= _matrix.width || t.matrixY >= _matrix.height) continue;
                        if (_matrix.maps[t.matrixY, t.matrixX] != GameMatrix.EMPTY) set.Add((t.matrixX, t.matrixY));
                    }
                }
                foreach (var g in _file.normalTreeGroups) NoteTrees(g);
                foreach (var g in _file.specialTreeGroups) NoteTrees(g);
            }
            return set;
        }

        public void RefreshTreeMarkers()
        {
            MarkerMesh = null; MarkerVertexCount = 0;
            var m = Model3D;
            if (m != null && m.CellStrideX != 0 && SelectedGroupTrees.Count > 0)
            {
                float tile = (m.CellStrideX / MapTiles + m.CellStrideZ / MapTiles) * 0.5f;
                float eps = tile * 0.06f;
                var v = new List<float>(SelectedGroupTrees.Count * 48);
                for (int i = 0; i < SelectedGroupTrees.Count; i++)
                {
                    var t = SelectedGroupTrees[i].Tree;
                    if (t.IsUnused) continue;
                    if (!TreeRaw(m, t, out float rx, out float rz)) continue;
                    float y = m.SurfaceY(rx, rz) + eps;
                    bool sel = i == _selTree;
                    bool special = _specialGroupActive;
                    var c = sel ? (1f, 1f, 1f) : (special ? (1f, 0.78f, 0.15f) : (0.30f, 0.85f, 0.35f));
                    float half = (sel ? 0.5f : 0.42f) * tile;
                    AddMarkerQuad(v, m, rx, y, rz, half, c);
                }
                MarkerMesh = v.ToArray();
                MarkerVertexCount = v.Count / 8;
            }
            MarkersChanged?.Invoke(this, EventArgs.Empty);
            GizmoTargetChanged?.Invoke(this, EventArgs.Empty);
        }

        private static bool TreeRaw(NsbmdRenderModel m, HeadbuttTree t, out float rx, out float rz)
        {
            rx = rz = 0f;
            if (!m.TryCellPlacement(t.matrixX, t.matrixY, out var p)) return false;
            rx = p.OriginX + (t.mapX + 0.5f) / MapTiles * p.Width;
            rz = p.OriginZ + (t.mapY + 0.5f) / MapTiles * p.Height;
            return true;
        }

        private static void AddMarkerQuad(List<float> v, NsbmdRenderModel m, float cx, float cy, float cz, float half, (float r, float g, float b) col)
        {
            var a = m.ToNormalized(cx - half, cy, cz - half);
            var b = m.ToNormalized(cx + half, cy, cz - half);
            var c = m.ToNormalized(cx + half, cy, cz + half);
            var d = m.ToNormalized(cx - half, cy, cz + half);
            void P((float x, float y, float z) q) { v.Add(q.x); v.Add(q.y); v.Add(q.z); v.Add(0); v.Add(0); v.Add(col.r); v.Add(col.g); v.Add(col.b); }
            P(a); P(b); P(c); P(a); P(c); P(d);
        }

        // ── Tree selection + 3D move gizmo ────────────────────────────────────────────────
        private int _selTree = -1;
        public int SelectedTreeIndex { get => _selTree; set { if (Set(ref _selTree, value)) RefreshTreeMarkers(); } }

        private bool _editMode3D;
        public bool EditMode3D { get => _editMode3D; set { if (Set(ref _editMode3D, value)) { OnPropertyChanged(nameof(EditMode3D)); EditModeChanged?.Invoke(this, EventArgs.Empty); } } }
        public event EventHandler EditModeChanged;
        public event EventHandler GizmoTargetChanged;
        public float ModelScale => Model3D?.Scale ?? 1f;

        public bool TrySelectedTreeAnchorNorm(out float nx, out float ny, out float nz)
        {
            nx = ny = nz = 0f;
            var m = Model3D;
            if (m == null || _selTree < 0 || _selTree >= SelectedGroupTrees.Count) return false;
            var t = SelectedGroupTrees[_selTree].Tree;
            if (t.IsUnused || !TreeRaw(m, t, out float rx, out float rz)) return false;
            var (a, b, c) = m.ToNormalized(rx, m.SurfaceY(rx, rz), rz);
            nx = a; ny = b; nz = c;
            return true;
        }

        public IEnumerable<(int index, float nx, float ny, float nz)> TreeAnchorsNorm()
        {
            var m = Model3D;
            if (m == null) yield break;
            for (int i = 0; i < SelectedGroupTrees.Count; i++)
            {
                var t = SelectedGroupTrees[i].Tree;
                if (t.IsUnused || !TreeRaw(m, t, out float rx, out float rz)) continue;
                var (a, b, c) = m.ToNormalized(rx, m.SurfaceY(rx, rz), rz);
                yield return (i, a, b, c);
            }
        }

        private float _dragAccumX, _dragAccumZ;
        public void BeginGizmoDrag() { _dragAccumX = 0f; _dragAccumZ = 0f; }
        public bool HasSelectedTree => _selTree >= 0 && _selTree < SelectedGroupTrees.Count;

        /// <summary>Moves the selected tree by whole tiles along X / Z (for arrow keys), rolling over into
        /// the neighbouring matrix cell at the map edges.</summary>
        public void NudgeSelectedTreeTiles(int dx, int dz)
        {
            if (!HasSelectedTree) return;
            var t = SelectedGroupTrees[_selTree].Tree;
            if (t.IsUnused) return;
            if (dx != 0)
            {
                int tile = t.mapX + dx, mat = t.matrixX;
                while (tile < 0) { mat--; tile += MapTiles; }
                while (tile >= MapTiles) { mat++; tile -= MapTiles; }
                if (mat < 0) { mat = 0; tile = 0; }
                t.mapX = (ushort)tile; t.matrixX = (ushort)mat;
            }
            if (dz != 0)
            {
                int tile = t.mapY + dz, mat = t.matrixY;
                while (tile < 0) { mat--; tile += MapTiles; }
                while (tile >= MapTiles) { mat++; tile -= MapTiles; }
                if (mat < 0) { mat = 0; tile = 0; }
                t.mapY = (ushort)tile; t.matrixY = (ushort)mat;
            }
            SelectedGroupTrees[_selTree].RaiseAll();
        }

        /// <summary>Moves the selected tree along a ground axis (0=X,2=Z) by a raw delta, stepping its
        /// in-map tile in whole-tile increments (carrying the remainder) and rolling over into the
        /// neighbouring matrix cell at the edges. Y (axis 1) is a no-op — trees have no height.</summary>
        public void NudgeSelectedTreeRaw(int axis, float rawDelta)
        {
            var m = Model3D;
            if (m == null || axis == 1 || rawDelta == 0f || _selTree < 0 || _selTree >= SelectedGroupTrees.Count) return;
            var row = SelectedGroupTrees[_selTree];
            var t = row.Tree;
            if (t.IsUnused || !m.TryCellPlacement(t.matrixX, t.matrixY, out var p)) return;
            int step;
            if (axis == 0)
            {
                float per = p.Width / MapTiles; if (per <= 0) return;
                _dragAccumX += rawDelta / per; step = (int)_dragAccumX; if (step == 0) return; _dragAccumX -= step;
                int tile = t.mapX + step, mat = t.matrixX;
                while (tile < 0) { mat--; tile += MapTiles; }
                while (tile >= MapTiles) { mat++; tile -= MapTiles; }
                if (mat < 0) { mat = 0; tile = 0; }
                t.mapX = (ushort)tile; t.matrixX = (ushort)mat;
            }
            else
            {
                float per = p.Height / MapTiles; if (per <= 0) return;
                _dragAccumZ += rawDelta / per; step = (int)_dragAccumZ; if (step == 0) return; _dragAccumZ -= step;
                int tile = t.mapY + step, mat = t.matrixY;
                while (tile < 0) { mat--; tile += MapTiles; }
                while (tile >= MapTiles) { mat++; tile -= MapTiles; }
                if (mat < 0) { mat = 0; tile = 0; }
                t.mapY = (ushort)tile; t.matrixY = (ushort)mat;
            }
            row.RaiseAll();   // updates numeric fields + marks dirty + refreshes markers
        }

        public void Save()
        {
            if (_file == null || _selFile < 0) return;
            try
            {
                bool ok;
                if (HgEngineProject.IsActive)
                {
                    ok = HgEngineHeadbutt.TrySave(_selFile, _file, out string err);
                    if (!ok) AppLogger.Error($"hg-engine headbutt write failed (file {_selFile}): {err}");
                }
                else
                {
                    ok = _file.SaveToFile(_selFile);
                }
                if (ok) { SetClean(); StatusText = $"Saved headbutt file {_selFile}."; }
                else StatusText = "Save failed (see log).";
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Save failed:\n{ex.Message}", "Headbutt Editor"); }
        }
    }
}
