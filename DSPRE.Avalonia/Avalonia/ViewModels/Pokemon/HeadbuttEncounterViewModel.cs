using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
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
        // Out-of-range values show clamped so the boxes never coerce, and are only replaced when the user picks another value.
        private const int MaxShownLevel = 100;
        public int SpeciesIndex { get => _e.pokemonID < Species.Count ? _e.pokemonID : -1; set { if (value < 0 || value == SpeciesIndex) return; _e.pokemonID = (ushort)value; On(nameof(SpeciesIndex)); _changed(); } }
        public decimal MinLevel { get => Math.Min((int)_e.minLevel, MaxShownLevel); set { if (value == MinLevel) return; _e.minLevel = (byte)value; On(nameof(MinLevel)); _changed(); } }
        public decimal MaxLevel { get => Math.Min((int)_e.maxLevel, MaxShownLevel); set { if (value == MaxLevel) return; _e.maxLevel = (byte)value; On(nameof(MaxLevel)); _changed(); } }
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
        // Matrix-cell + in-map-tile breakdown (globalX = matrixX*32 + mapX): the same coordinates the
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
    /// Avalonia port of the WinForms <c>HeadbuttEncounterEditor</c>, data scope (HGSS). Edits a
    /// headbutt encounter file: the 12 normal + 6 special wild-encounter slots, and the normal /
    /// special tree groups (each a set of trees positioned by a global x/y). The on-map 3D tree
    /// placement from WinForms is deferred; coordinates are editable numerically.
    /// </summary>
    public class HeadbuttEncounterViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
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
        public int SelectedFileIndex
        {
            get => _selFile;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == _selFile) return;
                if (_dirty && !_suppress && value >= 0 && _selFile >= 0)
                {
                    // Snap the list back to the file still loaded until the user has answered.
                    int requested = value;
                    RecordSwitchGuard.SnapBack(() => _selFile, v => _selFile = v, () => OnPropertyChanged(nameof(SelectedFileIndex)));
                    _ = SwitchFileAsync(requested);
                    return;
                }
                if (Set(ref _selFile, value) && !_suppress && value >= 0) LoadFile(value);
            }
        }

        private async Task SwitchFileAsync(int requested)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, _owner, "headbutt file")) return;
            SetClean();
            if (Set(ref _selFile, requested, nameof(SelectedFileIndex))) LoadFile(requested);
        }

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
        private void Dirty()
        {
            _undo?.Record();
            bool dirty = _undo?.IsDirty ?? true;
            if (_dirty == dirty) return;
            _dirty = dirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // ── Undo / redo: the loaded file, as it is stored ──
        private DSPRE.Avalonia.ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private void ResetUndo()
        {
            if (_file == null) return;
            _undo = new DSPRE.Avalonia.ByteStateUndo(() => _file.ToByteArray(), ApplyState, RaiseUndo);
            RaiseUndo();
        }

        private void ApplyState(byte[] state)
        {
            int group = _selGroup;
            bool special = _specialGroupActive;
            _file = new HeadbuttEncounterFile((ushort)System.Math.Max(0, _selFile), state);
            BuildEncounterRows();
            RefreshGroups();
            _selGroup = group;
            _specialGroupActive = special;
            ShowGroupTrees();
            bool dirty = _undo.IsDirty;
            if (_dirty != dirty) { _dirty = dirty; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        }

        private void BuildEncounterRows()
        {
            NormalEncounters.Clear();
            for (int i = 0; i < _file.normalEncounters.Count; i++)
                NormalEncounters.Add(new HeadbuttEncRow($"Normal {i + 1}", _file.normalEncounters[i], Species, Dirty));
            SpecialEncounters.Clear();
            for (int i = 0; i < _file.specialEncounters.Count; i++)
                SpecialEncounters.Add(new HeadbuttEncRow($"Special {i + 1}", _file.specialEncounters[i], Species, Dirty));
        }
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
                BuildEncounterRows();
                RefreshGroups();
                SetClean();
                ResetUndo();
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
            // Only show USED tree slots; empty slots are the 65535/65535 sentinel and just look like
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
            RefreshTreeMarkers();   // the map is already built for the whole file, only re-mark this group
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
                if (include.Count == 0 && _headerId == MapHeader.Everywhere)
                {
                    MapInfo = "Header 0 is the game's catch-all header, not a place, so there is no map to show.";
                    MapLoaded?.Invoke(this, EventArgs.Empty);
                    RefreshTreeMarkers();
                    return;
                }
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
        /// the matrix has no headers section) plus every cell a tree occupies, the WinForms map set.</summary>
        private HashSet<(int x, int y)> HeaderCells()
        {
            var set = new HashSet<(int x, int y)>();
            if (_matrix == null) return set;
            // The catch-all header owns no place of its own, only the cells its trees stand on.
            if (_headerId != MapHeader.Everywhere)
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
            // Only the tile being edited gets a square; the trees themselves are tinted below.
            if (m != null && m.CellStrideX != 0 && _selTree >= 0 && _selTree < SelectedGroupTrees.Count)
            {
                float tile = (m.CellStrideX / MapTiles + m.CellStrideZ / MapTiles) * 0.5f;
                var t = SelectedGroupTrees[_selTree].Tree;
                if (!t.IsUnused && TreeRaw(m, t, out float rx, out float rz))
                {
                    var v = new List<float>(48);
                    AddMarkerQuad(v, m, rx, m.SurfaceY(rx, rz) + tile * 0.06f, rz, 0.5f * tile, (1f, 1f, 1f));
                    MarkerMesh = v.ToArray();
                    MarkerVertexCount = v.Count / 8;
                }
            }
            BuildTreeHighlight(m);
            MarkersChanged?.Invoke(this, EventArgs.Empty);
            GizmoTargetChanged?.Invoke(this, EventArgs.Empty);
        }

        // ── Whole-tree tint and the hover card ────────────────────────────────────────────
        // A tree group is one tree: its six slots are the tiles the player can headbutt it from.

        public IReadOnlyList<NsbmdGlControl.HighlightBatch> Highlight { get; private set; }

        private sealed class TreeBox
        {
            public bool Special;
            public int Group;
            public float MinX, MaxX, MinZ, MaxZ, Ground, Top;   // normalized render space; grows to the model found
        }
        private readonly List<TreeBox> _treeBoxes = new List<TreeBox>();

        private void BuildTreeBoxes(NsbmdRenderModel m)
        {
            _treeBoxes.Clear();
            if (m == null || _file == null) return;
            void Add(IList<HeadbuttTreeGroup> groups, bool special)
            {
                for (int g = 0; g < groups.Count; g++)
                {
                    TreeBox box = null;
                    foreach (var t in groups[g].trees)
                    {
                        if (t.IsUnused || !m.TryCellPlacement(t.matrixX, t.matrixY, out var p)) continue;
                        float tw = p.Width / MapTiles, th = p.Height / MapTiles;
                        float x0 = p.OriginX + t.mapX * tw, z0 = p.OriginZ + t.mapY * th;
                        float ground = m.SurfaceY(x0 + tw / 2, z0 + th / 2);
                        var (ax, ay, az) = m.ToNormalized(x0, ground, z0);
                        var (bx, _, bz) = m.ToNormalized(x0 + tw, ground, z0 + th);
                        if (box == null) box = new TreeBox { Special = special, Group = g, MinX = ax, MaxX = bx, MinZ = az, MaxZ = bz, Ground = ay, Top = ay };
                        else
                        {
                            box.MinX = Math.Min(box.MinX, ax); box.MaxX = Math.Max(box.MaxX, bx);
                            box.MinZ = Math.Min(box.MinZ, az); box.MaxZ = Math.Max(box.MaxZ, bz);
                            box.Ground = Math.Min(box.Ground, ay);
                        }
                    }
                    if (box != null) _treeBoxes.Add(box);
                }
            }
            Add(_file.normalTreeGroups, false);
            Add(_file.specialTreeGroups, true);
        }

        // Raised scene triangles grouped into connected pieces, built once per scene.
        private NsbmdRenderModel _piecesOf;
        private int[] _triPart, _triStart, _triPiece;
        private float[] _triCx, _triCz;
        private float[] _pieceMinX, _pieceMaxX, _pieceMinZ, _pieceMaxZ;

        private void BuildPieces(NsbmdRenderModel m)
        {
            if (ReferenceEquals(_piecesOf, m)) return;
            _piecesOf = m;
            float tileN = (m.CellStrideX / MapTiles) * m.Scale;
            float lift = tileN * 0.2f;
            var part = new List<int>(); var start = new List<int>(); var cxs = new List<float>(); var czs = new List<float>();
            var keys = new List<(long, long, long)>();
            for (int pi = 0; pi < m.Parts.Count; pi++)
            {
                var a = m.Parts[pi].Vertices;
                int n = Math.Min(m.Parts[pi].VertexCount, a.Length / 8) / 3 * 3;
                for (int v = 0; v < n; v += 3)
                {
                    int i = v * 8;
                    float cx = (a[i] + a[i + 8] + a[i + 16]) / 3f, cz = (a[i + 2] + a[i + 10] + a[i + 18]) / 3f;
                    float top = Math.Max(a[i + 1], Math.Max(a[i + 9], a[i + 17]));
                    // Ground under and around trees stays out, so pieces never join through it.
                    float groundN = (m.SurfaceY(cx / m.Scale + m.Cx, cz / m.Scale + m.Cz) - m.Cy) * m.Scale;
                    if (top < groundN + lift) continue;
                    part.Add(pi); start.Add(i); cxs.Add(cx); czs.Add(cz);
                }
            }
            int count = part.Count;
            var parent = new int[count];
            for (int t = 0; t < count; t++) parent[t] = t;
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            var owner = new Dictionary<(long, long, long), int>();
            for (int t = 0; t < count; t++)
            {
                var a = m.Parts[part[t]].Vertices;
                for (int k = 0; k < 3; k++)
                {
                    int o = start[t] + k * 8;
                    var key = ((long)Math.Round(a[o] * 4096), (long)Math.Round(a[o + 1] * 4096), (long)Math.Round(a[o + 2] * 4096));
                    if (owner.TryGetValue(key, out int other)) { int ra = Find(t), rb = Find(other); if (ra != rb) parent[ra] = rb; }
                    else owner[key] = t;
                }
            }
            _triPart = part.ToArray(); _triStart = start.ToArray(); _triCx = cxs.ToArray(); _triCz = czs.ToArray();
            _triPiece = new int[count];
            var ids = new Dictionary<int, int>();
            for (int t = 0; t < count; t++)
            {
                int r = Find(t);
                if (!ids.TryGetValue(r, out int id)) ids[r] = id = ids.Count;
                _triPiece[t] = id;
            }
            _pieceMinX = new float[ids.Count]; _pieceMaxX = new float[ids.Count]; _pieceMinZ = new float[ids.Count]; _pieceMaxZ = new float[ids.Count];
            for (int q = 0; q < ids.Count; q++) { _pieceMinX[q] = _pieceMinZ[q] = float.MaxValue; _pieceMaxX[q] = _pieceMaxZ[q] = float.MinValue; }
            for (int t = 0; t < count; t++)
            {
                var a = m.Parts[part[t]].Vertices;
                int q = _triPiece[t];
                for (int k = 0; k < 3; k++)
                {
                    int o = start[t] + k * 8;
                    _pieceMinX[q] = Math.Min(_pieceMinX[q], a[o]); _pieceMaxX[q] = Math.Max(_pieceMaxX[q], a[o]);
                    _pieceMinZ[q] = Math.Min(_pieceMinZ[q], a[o + 2]); _pieceMaxZ[q] = Math.Max(_pieceMaxZ[q], a[o + 2]);
                }
            }
        }

        /// <summary>Tints every tree's own model: the selected group strongly, the rest faintly.</summary>
        private void BuildTreeHighlight(NsbmdRenderModel m)
        {
            Highlight = null;
            BuildTreeBoxes(m);
            if (m == null || _treeBoxes.Count == 0) return;
            BuildPieces(m);

            float tileN = (m.CellStrideX / MapTiles) * m.Scale;
            float maxSpan = tileN * 4f;   // bigger pieces are cliffs, fences or rows of buildings; thinner ones are edges
            var pieceOwner = new Dictionary<int, TreeBox>();
            // The selected group claims first, so a piece shared by two groups shows as the one being edited.
            // A tree's listed tiles can be only its bottom row, so a tree that finds nothing looks a little wider.
            foreach (var box in _treeBoxes.OrderByDescending(b => b.Special == _specialGroupActive && b.Group == _selGroup))
                foreach (float margin in new[] { 0f, tileN * 0.5f, tileN })
                {
                    bool found = false;
                    for (int t = 0; t < _triPiece.Length; t++)
                    {
                        if (_triCx[t] < box.MinX - margin || _triCx[t] > box.MaxX + margin
                            || _triCz[t] < box.MinZ - margin || _triCz[t] > box.MaxZ + margin) continue;
                        int q = _triPiece[t];
                        if (_pieceMaxX[q] - _pieceMinX[q] > maxSpan || _pieceMaxZ[q] - _pieceMinZ[q] > maxSpan
                            || _pieceMaxX[q] - _pieceMinX[q] < tileN * 0.5f || _pieceMaxZ[q] - _pieceMinZ[q] < tileN * 0.5f) continue;
                        if (pieceOwner.TryGetValue(q, out var had)) { found |= had == box; continue; }
                        pieceOwner[q] = box;
                        found = true;
                    }
                    if (found) break;
                }

            var batches = new Dictionary<(int mat, bool special, bool picked), List<float>>();
            for (int t = 0; t < _triPiece.Length; t++)
            {
                if (!pieceOwner.TryGetValue(_triPiece[t], out var box)) continue;
                var p = m.Parts[_triPart[t]];
                bool picked = box.Special == _specialGroupActive && box.Group == _selGroup;
                var key = (p.MaterialIndex, box.Special, picked);
                if (!batches.TryGetValue(key, out var v)) batches[key] = v = new List<float>();
                var a = p.Vertices;
                for (int k = 0; k < 24; k++) v.Add(a[_triStart[t] + k]);
                for (int k = 0; k < 3; k++)
                {
                    int o = _triStart[t] + k * 8;
                    box.Top = Math.Max(box.Top, a[o + 1]);
                    box.MinX = Math.Min(box.MinX, a[o]); box.MaxX = Math.Max(box.MaxX, a[o]);
                    box.MinZ = Math.Min(box.MinZ, a[o + 2]); box.MaxZ = Math.Max(box.MaxZ, a[o + 2]);
                }
            }
            var list = new List<NsbmdGlControl.HighlightBatch>();
            foreach (var kv in batches)
            {
                var (mat, special, picked) = kv.Key;
                var col = special
                    ? (picked ? (2.2f, 1.7f, 0.2f) : (1.5f, 1.2f, 0.4f))
                    : (picked ? (0.5f, 2.0f, 0.5f) : (0.6f, 1.45f, 0.6f));
                list.Add(new NsbmdGlControl.HighlightBatch { MaterialKey = mat, Mesh = kv.Value.ToArray(), R = col.Item1, G = col.Item2, B = col.Item3 });
            }
            Highlight = list;
        }

        /// <summary>The tree a ray (normalized render space) hits first, or null.</summary>
        public (bool Special, int Group)? TreeAlong(float ox, float oy, float oz, float dx, float dy, float dz)
        {
            var m = Model3D;
            if (m == null) return null;
            float tileN = (m.CellStrideX / MapTiles) * m.Scale;
            TreeBox best = null;
            float bestT = float.MaxValue;
            foreach (var b in _treeBoxes)
            {
                float top = Math.Max(b.Top, b.Ground + tileN * 1.5f);
                if (RayHitsBox(ox, oy, oz, dx, dy, dz, b.MinX, b.Ground, b.MinZ, b.MaxX, top, b.MaxZ, out float t) && t < bestT)
                { bestT = t; best = b; }
            }
            return best == null ? null : (best.Special, best.Group);
        }

        private static bool RayHitsBox(float ox, float oy, float oz, float dx, float dy, float dz,
            float x0, float y0, float z0, float x1, float y1, float z1, out float tNear)
        {
            tNear = float.MinValue;
            float tFar = float.MaxValue;
            bool Slab(float o, float d, float lo, float hi, ref float n, ref float f)
            {
                if (Math.Abs(d) < 1e-9f) return o >= lo && o <= hi;
                float a = (lo - o) / d, b = (hi - o) / d;
                if (a > b) (a, b) = (b, a);
                n = Math.Max(n, a); f = Math.Min(f, b);
                return n <= f;
            }
            return Slab(ox, dx, x0, x1, ref tNear, ref tFar) && Slab(oy, dy, y0, y1, ref tNear, ref tFar)
                && Slab(oz, dz, z0, z1, ref tNear, ref tFar) && tFar >= 0;
        }

        public sealed class HoverSlot
        {
            public Bitmap Icon { get; init; }
            public string Name { get; init; }
            public string Levels { get; init; }
            public string Chance { get; init; }
        }

        public sealed class HoverTable
        {
            public string Header { get; init; }
            public List<HoverSlot> Slots { get; init; }
        }

        public sealed class HoverCard
        {
            public string Title { get; init; }
            public string Rule { get; init; }
            public List<HoverTable> Tables { get; init; }
        }

        private readonly Dictionary<int, Bitmap> _iconCache = new Dictionary<int, Bitmap>();

        private Bitmap IconOf(int species)
        {
            if (species <= 0) return null;
            if (_iconCache.TryGetValue(species, out var icon)) return icon;
            try { icon = ImageConverter.ToAvaloniaBitmap(DSUtils.GetPokePicRaw(species, 32, 32)); }
            catch { icon = null; }
            return _iconCache[species] = icon;
        }

        private HoverTable TableOf(string header, IList<HeadbuttEncounter> slots, int first)
        {
            // Slots holding the same Pokémon at the same levels add up, as the player sees them.
            var order = new List<(ushort id, byte lo, byte hi)>();
            var chance = new Dictionary<(ushort, byte, byte), int>();
            for (int s = 0; s < HeadbuttRules.SlotsPerTable && first + s < slots.Count; s++)
            {
                var e = slots[first + s];
                if (e.pokemonID == 0) continue;
                var key = (e.pokemonID, e.minLevel, e.maxLevel);
                if (!chance.ContainsKey(key)) { chance[key] = 0; order.Add(key); }
                chance[key] += (EncounterSlotOdds.CurrentPercents("Headbutt") ?? HeadbuttRules.SlotChance)[s];
            }
            var rows = new List<HoverSlot>();
            foreach (var (id, lo, hi) in order)
                rows.Add(new HoverSlot
                {
                    Icon = IconOf(id),
                    Name = id < Species.Count ? Species[id] : $"#{id}",
                    Levels = lo == hi ? $"Lv {lo}" : $"Lv {lo}-{hi}",
                    Chance = $"{chance[(id, lo, hi)]}%",
                });
            return new HoverTable { Header = header, Slots = rows };
        }

        /// <summary>What headbutting a tree can find, for the view's hover card.</summary>
        public HoverCard CardFor(bool special, int group)
        {
            if (_file == null) return null;
            if (special)
            {
                if (group < 0 || group >= _file.specialTreeGroups.Count) return null;
                return new HoverCard
                {
                    Title = $"Special tree {group}",
                    Rule = "Same for every trainer ID.",
                    Tables = new List<HoverTable> { TableOf("Secret", _file.specialEncounters, 0) },
                };
            }
            int count = _file.normalTreeGroups.Count;
            if (group < 0 || group >= count) return null;
            var byTable = new Dictionary<HeadbuttRules.Table, List<int>>();
            for (int digit = 0; digit <= 9; digit++)
            {
                var table = HeadbuttRules.NormalTreeTable(group, count, digit);
                if (!byTable.TryGetValue(table, out var digits)) byTable[table] = digits = new List<int>();
                digits.Add(digit);
            }
            string Ends(HeadbuttRules.Table t) => byTable.TryGetValue(t, out var d) ? string.Join(", ", d) : null;
            var parts = new List<string>();
            if (Ends(HeadbuttRules.Table.Common) is string c) parts.Add($"Common: ID key {c}");
            if (Ends(HeadbuttRules.Table.Rare) is string r) parts.Add($"Rare: ID key {r}");
            if (Ends(HeadbuttRules.Table.None) is string n) parts.Add($"Nothing: ID key {n}");
            var tables = new List<HoverTable>();
            if (byTable.ContainsKey(HeadbuttRules.Table.Common)) tables.Add(TableOf("Common", _file.normalEncounters, 0));
            if (byTable.ContainsKey(HeadbuttRules.Table.Rare)) tables.Add(TableOf("Rare", _file.normalEncounters, HeadbuttRules.SlotsPerTable));
            return new HoverCard { Title = $"Normal tree {group}", Rule = string.Join("\n", parts), Tables = tables };
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
        /// neighbouring matrix cell at the edges. Y (axis 1) is a no-op: trees have no height.</summary>
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
            if (HgEngineProject.IsActive) { _ = SaveHgEngineAsync(); return; }
            try
            {
                if (_file.SaveToFile(_selFile)) MarkSaved(_selFile);
                else StatusText = "Save failed (see log).";
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Save failed:\n{ex.Message}", "Headbutt Editor"); }
        }

        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            if (HgEngineProject.IsActive) await SaveHgEngineAsync();
            else Save();
            return !HasUnsavedChanges;
        }

        private async Task SaveHgEngineAsync()
        {
            if (_file == null || _selFile < 0) return;
            int fileId = _selFile;
            var file = _file;
            var (saved, error) = await HgEngineSave.RunAsync(() => HgEngineHeadbutt.TrySave(fileId, file, out string err) ? null : err);
            if (saved)
            {
                if (fileId == _selFile) MarkSaved(fileId);
                return;
            }
            if (error == null) return;
            StatusText = "Save failed.";
            await DialogHelper.ShowError($"Headbutt file {fileId} was not saved.\n{error}", "Headbutt Editor");
        }

        private void MarkSaved(int fileId)
        {
            _undo?.MarkSaved();
            SetClean();
            StatusText = $"Saved headbutt file {fileId}.";
            SaveNotice.Saved(UnsavedChangesDescription);
        }
    }
}
