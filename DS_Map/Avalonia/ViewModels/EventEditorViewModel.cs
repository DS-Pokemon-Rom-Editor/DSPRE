using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the WinForms <c>EventEditor</c> — core scope. Edits an event
    /// file's four event lists (Spawnables, Overworlds, Warps, Triggers): select an
    /// event to edit its position (map + matrix) and its type-specific fields, add/remove
    /// events, and save / import / export. The 3D map overlay with event markers and
    /// click-to-place are deferred (the renderer foundation is in place for later).
    /// </summary>
    public class EventEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private EventFile _file;

        // ── 3D map view ────────────────────────────────────────────────────────────────
        private Dictionary<int, (ushort matrixId, byte areaId)> _eventToHeader; // event file → its header's matrix + area
        private GameMatrix _matrix;
        private byte _areaDataId;

        /// <summary>Raised after the 3D map is (re)loaded so the view can push the new model.</summary>
        public event EventHandler MapLoaded;
        /// <summary>Raised when the event markers change so the view can push the new marker mesh.</summary>
        public event EventHandler MarkersChanged;

        public NsbmdRenderModel Model3D { get; private set; }
        public float[] MarkerMesh { get; private set; }
        public int MarkerVertexCount { get; private set; }

        public List<NsbmdGlControl.SpriteInstance> Sprites { get; private set; }
        /// <summary>Raised when overworld sprites change so the view can push them to the GL control.</summary>
        public event EventHandler SpritesChanged;

        private string _mapInfo = "";
        public string MapInfo { get => _mapInfo; set => Set(ref _mapInfo, value); }

        public ObservableCollection<string> EventNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Spawnables { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Overworlds { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Warps { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Triggers { get; } = new ObservableCollection<string>();

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── Current selection ───────────────────────────────────────────────────────
        private Event _current;          // base for shared position props
        private Spawnable _spawn;
        private Overworld _ow;
        private Warp _warp;
        private Trigger _trig;

        private int _selSpawn = -1, _selOw = -1, _selWarp = -1, _selTrig = -1;
        public int SelectedSpawnableIndex { get => _selSpawn; set { if (Set(ref _selSpawn, value)) LoadSpawnable(value); } }
        public int SelectedOverworldIndex { get => _selOw; set { if (Set(ref _selOw, value)) LoadOverworld(value); } }
        public int SelectedWarpIndex { get => _selWarp; set { if (Set(ref _selWarp, value)) LoadWarp(value); } }
        public int SelectedTriggerIndex { get => _selTrig; set { if (Set(ref _selTrig, value)) LoadTrigger(value); } }

        public bool HasSpawn => _spawn != null;
        public bool HasOw => _ow != null;
        public bool HasWarp => _warp != null;
        public bool HasTrig => _trig != null;

        // ── Shared position (map + matrix) ──────────────────────────────────────────
        private decimal _xMap, _yMap, _zPos, _xMat, _yMat;
        public decimal XMap { get => _xMap; set { if (Set(ref _xMap, value) && !_suppress && _current != null) { _current.xMapPosition = (short)value; Dirty(); RefreshMarkers(); } } }
        public decimal YMap { get => _yMap; set { if (Set(ref _yMap, value) && !_suppress && _current != null) { _current.yMapPosition = (short)value; Dirty(); RefreshMarkers(); } } }
        public decimal ZPos { get => _zPos; set { if (Set(ref _zPos, value) && !_suppress && _current != null) { _current.zPosition = (int)value; Dirty(); } } }
        public decimal XMatrix { get => _xMat; set { if (Set(ref _xMat, value) && !_suppress && _current != null) { _current.xMatrixPosition = (ushort)value; Dirty(); RefreshMarkers(); } } }
        public decimal YMatrix { get => _yMat; set { if (Set(ref _yMat, value) && !_suppress && _current != null) { _current.yMatrixPosition = (ushort)value; Dirty(); RefreshMarkers(); } } }

        // ── Spawnable fields ────────────────────────────────────────────────────────
        private decimal _spScript, _spType, _spDir;
        public decimal SpScript { get => _spScript; set { if (Set(ref _spScript, value) && !_suppress && _spawn != null) { _spawn.scriptNumber = (ushort)value; Dirty(); } } }
        public decimal SpType { get => _spType; set { if (Set(ref _spType, value) && !_suppress && _spawn != null) { _spawn.type = (ushort)value; Dirty(); } } }
        public decimal SpDir { get => _spDir; set { if (Set(ref _spDir, value) && !_suppress && _spawn != null) { _spawn.dir = (ushort)value; Dirty(); } } }

        // ── Overworld fields ────────────────────────────────────────────────────────
        private decimal _owId, _owSprite, _owMove, _owType, _owFlag, _owScript, _owOrient, _owSight, _owXr, _owYr;
        public decimal OwId { get => _owId; set { if (Set(ref _owId, value) && !_suppress && _ow != null) { _ow.owID = (ushort)value; Dirty(); } } }
        public decimal OwSprite { get => _owSprite; set { if (Set(ref _owSprite, value) && !_suppress && _ow != null) { _ow.overlayTableEntry = (ushort)value; Dirty(); } } }
        public decimal OwMovement { get => _owMove; set { if (Set(ref _owMove, value) && !_suppress && _ow != null) { _ow.movement = (ushort)value; Dirty(); } } }
        public decimal OwType { get => _owType; set { if (Set(ref _owType, value) && !_suppress && _ow != null) { _ow.type = (ushort)value; Dirty(); } } }
        public decimal OwFlag { get => _owFlag; set { if (Set(ref _owFlag, value) && !_suppress && _ow != null) { _ow.flag = (ushort)value; Dirty(); } } }
        public decimal OwScript { get => _owScript; set { if (Set(ref _owScript, value) && !_suppress && _ow != null) { _ow.scriptNumber = (ushort)value; Dirty(); } } }
        public decimal OwOrientation { get => _owOrient; set { if (Set(ref _owOrient, value) && !_suppress && _ow != null) { _ow.orientation = (ushort)value; Dirty(); } } }
        public decimal OwSight { get => _owSight; set { if (Set(ref _owSight, value) && !_suppress && _ow != null) { _ow.sightRange = (ushort)value; Dirty(); } } }
        public decimal OwXRange { get => _owXr; set { if (Set(ref _owXr, value) && !_suppress && _ow != null) { _ow.xRange = (ushort)value; Dirty(); } } }
        public decimal OwYRange { get => _owYr; set { if (Set(ref _owYr, value) && !_suppress && _ow != null) { _ow.yRange = (ushort)value; Dirty(); } } }

        // ── Warp fields ───────────────────────────────────────────────────────────────
        private decimal _warpHeader, _warpAnchor, _warpHeight;
        public decimal WarpHeader { get => _warpHeader; set { if (Set(ref _warpHeader, value) && !_suppress && _warp != null) { _warp.header = (ushort)value; Dirty(); } } }
        public decimal WarpAnchor { get => _warpAnchor; set { if (Set(ref _warpAnchor, value) && !_suppress && _warp != null) { _warp.anchor = (ushort)value; Dirty(); } } }
        public decimal WarpHeight { get => _warpHeight; set { if (Set(ref _warpHeight, value) && !_suppress && _warp != null) { _warp.height = (uint)value; Dirty(); } } }

        // ── Trigger fields ──────────────────────────────────────────────────────────
        private decimal _trScript, _trW, _trH, _trVarVal, _trVar;
        public decimal TrScript { get => _trScript; set { if (Set(ref _trScript, value) && !_suppress && _trig != null) { _trig.scriptNumber = (ushort)value; Dirty(); } } }
        public decimal TrWidth { get => _trW; set { if (Set(ref _trW, value) && !_suppress && _trig != null) { _trig.widthX = (ushort)value; Dirty(); } } }
        public decimal TrHeight { get => _trH; set { if (Set(ref _trH, value) && !_suppress && _trig != null) { _trig.heightY = (ushort)value; Dirty(); } } }
        public decimal TrVarValue { get => _trVarVal; set { if (Set(ref _trVarVal, value) && !_suppress && _trig != null) { _trig.expectedVarValue = (ushort)value; Dirty(); } } }
        public decimal TrVar { get => _trVar; set { if (Set(ref _trVar, value) && !_suppress && _trig != null) { _trig.variableWatched = (ushort)value; Dirty(); } } }

        // ── Dirty tracking ───────────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Event file {_selectedIndex}";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); if (_selectedIndex >= 0) LoadFile(_selectedIndex); }
        private void Dirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private int _selectedIndex = -1;
        public int SelectedEventIndex
        {
            get => _selectedIndex;
            set { if (Set(ref _selectedIndex, value) && !_suppress && value >= 0) LoadFile(value); }
        }

        public EventEditorViewModel() { if (Design.IsDesignMode) EventNames.Add("Event 0"); }
        public EventEditorViewModel(bool _) { }

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> {
                    DirNames.eventFiles, DirNames.maps, DirNames.exteriorBuildingModels,
                    DirNames.buildingTextures, DirNames.mapTextures, DirNames.matrices,
                    DirNames.areaData, DirNames.dynamicHeaders, DirNames.OWSprites });
                _eventToHeader = BuildEventHeaderLookup();
                int count = Filesystem.GetEventFileCount();
                for (int i = 0; i < count; i++) EventNames.Add("Event File " + i);
                StatusText = $"{count} event files.";
                if (count > 0) SelectedEventIndex = 0;
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowError($"Failed to set up Event Editor:\n{ex.Message}", "Event Editor");
            }
        }

        private void LoadFile(int index)
        {
            try
            {
                _file = new EventFile(index);
                RefreshLists();
                ResolveMatrixForFile(index);
                SetClean();
                StatusText = $"Loaded event file {index}.";
                OnPropertyChanged(nameof(UnsavedChangesDescription));
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Failed to load event file {index}:\n{ex.Message}", "Event Editor"); }
        }

        private void RefreshLists()
        {
            _suppress = true;
            Spawnables.Clear(); Overworlds.Clear(); Warps.Clear(); Triggers.Clear();
            if (_file != null)
            {
                for (int i = 0; i < _file.spawnables.Count; i++) Spawnables.Add($"Spawnable {i:D2}");
                for (int i = 0; i < _file.overworlds.Count; i++) Overworlds.Add($"Overworld {i:D2}");
                for (int i = 0; i < _file.warps.Count; i++) Warps.Add($"Warp {i:D2}");
                for (int i = 0; i < _file.triggers.Count; i++) Triggers.Add($"Trigger {i:D2}");
            }
            _suppress = false;
        }

        private void LoadPosition(Event e)
        {
            _current = e;
            if (e == null) return;
            XMap = e.xMapPosition; YMap = e.yMapPosition; ZPos = e.zPosition;
            XMatrix = e.xMatrixPosition; YMatrix = e.yMatrixPosition;
        }

        private void LoadSpawnable(int i)
        {
            _spawn = (_file != null && i >= 0 && i < _file.spawnables.Count) ? _file.spawnables[i] : null;
            OnPropertyChanged(nameof(HasSpawn));
            if (_spawn == null) return;
            _suppress = true;
            LoadPosition(_spawn);
            SpScript = _spawn.scriptNumber; SpType = _spawn.type; SpDir = _spawn.dir;
            _suppress = false;
            RefreshMarkers();
        }

        private void LoadOverworld(int i)
        {
            _ow = (_file != null && i >= 0 && i < _file.overworlds.Count) ? _file.overworlds[i] : null;
            OnPropertyChanged(nameof(HasOw));
            if (_ow == null) return;
            _suppress = true;
            LoadPosition(_ow);
            OwId = _ow.owID; OwSprite = _ow.overlayTableEntry; OwMovement = _ow.movement; OwType = _ow.type;
            OwFlag = _ow.flag; OwScript = _ow.scriptNumber; OwOrientation = _ow.orientation; OwSight = _ow.sightRange;
            OwXRange = _ow.xRange; OwYRange = _ow.yRange;
            _suppress = false;
            RefreshMarkers();
        }

        private void LoadWarp(int i)
        {
            _warp = (_file != null && i >= 0 && i < _file.warps.Count) ? _file.warps[i] : null;
            OnPropertyChanged(nameof(HasWarp));
            if (_warp == null) return;
            _suppress = true;
            LoadPosition(_warp);
            WarpHeader = _warp.header; WarpAnchor = _warp.anchor; WarpHeight = _warp.height;
            _suppress = false;
            RefreshMarkers();
        }

        private void LoadTrigger(int i)
        {
            _trig = (_file != null && i >= 0 && i < _file.triggers.Count) ? _file.triggers[i] : null;
            OnPropertyChanged(nameof(HasTrig));
            if (_trig == null) return;
            _suppress = true;
            LoadPosition(_trig);
            TrScript = _trig.scriptNumber; TrWidth = _trig.widthX; TrHeight = _trig.heightY;
            TrVarValue = _trig.expectedVarValue; TrVar = _trig.variableWatched;
            _suppress = false;
            RefreshMarkers();
        }

        // ── Add / remove ────────────────────────────────────────────────────────────
        public void AddSpawnable() { if (_file == null) return; _file.spawnables.Add(new Spawnable(0, 0)); RefreshLists(); Dirty(); SelectedSpawnableIndex = _file.spawnables.Count - 1; }
        public void RemoveSpawnable() { if (_file == null || _selSpawn < 0 || _selSpawn >= _file.spawnables.Count) return; _file.spawnables.RemoveAt(_selSpawn); RefreshLists(); Dirty(); SelectedSpawnableIndex = -1; RefreshMarkers(); }
        public void AddOverworld() { if (_file == null) return; _file.overworlds.Add(new Overworld(0, 0, 0)); RefreshLists(); Dirty(); SelectedOverworldIndex = _file.overworlds.Count - 1; }
        public void RemoveOverworld() { if (_file == null || _selOw < 0 || _selOw >= _file.overworlds.Count) return; _file.overworlds.RemoveAt(_selOw); RefreshLists(); Dirty(); SelectedOverworldIndex = -1; RefreshMarkers(); }
        public void AddWarp() { if (_file == null) return; _file.warps.Add(new Warp(0, 0)); RefreshLists(); Dirty(); SelectedWarpIndex = _file.warps.Count - 1; }
        public void RemoveWarp() { if (_file == null || _selWarp < 0 || _selWarp >= _file.warps.Count) return; _file.warps.RemoveAt(_selWarp); RefreshLists(); Dirty(); SelectedWarpIndex = -1; RefreshMarkers(); }
        public void AddTrigger() { if (_file == null) return; _file.triggers.Add(new Trigger(0, 0)); RefreshLists(); Dirty(); SelectedTriggerIndex = _file.triggers.Count - 1; }
        public void RemoveTrigger() { if (_file == null || _selTrig < 0 || _selTrig >= _file.triggers.Count) return; _file.triggers.RemoveAt(_selTrig); RefreshLists(); Dirty(); SelectedTriggerIndex = -1; RefreshMarkers(); }

        // ── 3D map view + event markers ─────────────────────────────────────────────────

        /// <summary>
        /// Builds the reverse map: event-file index → (matrix, area data) via the header that
        /// references it (<see cref="MapHeader.eventFileID"/>). This is the real ROM linkage
        /// the WinForms editor uses to pick the correct map + texture packs for an event file.
        /// </summary>
        private static Dictionary<int, (ushort, byte)> BuildEventHeaderLookup()
        {
            var lookup = new Dictionary<int, (ushort, byte)>();
            try
            {
                int headerCount = GetHeaderCount();
                for (ushort h = 0; h < headerCount; h++)
                {
                    try
                    {
                        var header = MapHeader.GetMapHeader(h);
                        if (header == null) continue;
                        if (!lookup.ContainsKey(header.eventFileID))
                            lookup[header.eventFileID] = (header.matrixID, header.areaDataID);
                    }
                    catch { /* skip bad header */ }
                }
            }
            catch (Exception ex) { AppLogger.Error("Event→header lookup failed: " + ex.Message); }
            return lookup;
        }

        /// <summary>Resolves the matrix + area for the loaded event file, then renders the whole matrix.</summary>
        private void ResolveMatrixForFile(int eventIndex)
        {
            _matrix = null; _areaDataId = 0; _matrixId = -1;
            try
            {
                if (_eventToHeader != null && _eventToHeader.TryGetValue(eventIndex, out var hdr))
                {
                    _matrixId = hdr.Item1;
                    _matrix = new GameMatrix(hdr.Item1);
                    _areaDataId = hdr.Item2;
                }
            }
            catch (Exception ex) { AppLogger.Error("Matrix resolve failed: " + ex.Message); }
            DisplayMap();
        }

        private int _matrixId = -1;

        /// <summary>
        /// Renders all maps of the event's matrix stitched together, so every map the event
        /// file can reach is visible at once (mirrors how an event file spans a whole matrix).
        /// Each cell's tileset is resolved through its header section / the file's area data.
        /// </summary>
        private void DisplayMap()
        {
            Model3D = null;
            try
            {
                if (_matrix == null)
                {
                    MapInfo = "No header references this event file — can't pick a matrix to render.";
                    MapLoaded?.Invoke(this, EventArgs.Empty); RefreshMarkers(); return;
                }

                Model3D = MatrixSceneBuilder.Build(_matrix, _areaDataId, gameFamily, areaForMap: null);
                MapInfo = Model3D != null
                    ? $"Matrix {_matrixId}  ·  {_matrix.width}×{_matrix.height}  ·  area {_areaDataId}"
                    : $"Matrix {_matrixId} has no renderable maps.";
            }
            catch (Exception ex)
            {
                MapInfo = "Map render failed: " + ex.Message;
                AppLogger.Error("Event map render failed: " + ex.Message);
            }
            MapLoaded?.Invoke(this, EventArgs.Empty);
            RefreshMarkers();
        }

        // Per-type marker colours (RGB 0..1).
        private static (float r, float g, float b) MarkerColor(int type) => type switch
        {
            0 => (0.25f, 0.95f, 0.35f),   // overworld  → green
            1 => (1.00f, 0.62f, 0.10f),   // warp       → orange
            2 => (0.95f, 0.25f, 0.90f),   // trigger    → magenta
            _ => (0.20f, 0.85f, 0.95f),   // spawnable  → cyan
        };

        // How far one z-position step raises a marker, in tile units (events that share the
        // ground sit at z = 0). Tunable — the in-game height step is roughly one tile.
        private const float ZStepInTiles = 1f / 8f;
        private const int MapTiles = 32;

        /// <summary>
        /// Rebuilds the event overlay: warps/triggers/spawnables as flat colour quads and
        /// overworlds as upright textured billboards (their real sprite). Everything is placed
        /// by matrix cell + tile + z so it sits where it will be in-game.
        /// </summary>
        public void RefreshMarkers()
        {
            MarkerMesh = null; MarkerVertexCount = 0;
            var sprites = new List<NsbmdGlControl.SpriteInstance>();
            var m = Model3D;
            if (_file != null && m != null && m.CellStrideX != 0)
            {
                float ground = m.HasMapBounds ? m.MapMinY : m.RawMinY;
                float tileX = m.CellStrideX / MapTiles;
                float tileZ = m.CellStrideZ / MapTiles;
                float surfEps = tileX * 0.05f;

                (float x, float y, float z) Foot(Event e)
                {
                    float rawX = m.CellBaseX + (e.xMatrixPosition + (e.xMapPosition + 0.5f) / MapTiles) * m.CellStrideX;
                    float rawZ = m.CellBaseZ + (e.yMatrixPosition + (e.yMapPosition + 0.5f) / MapTiles) * m.CellStrideZ;
                    float rawY = ground + surfEps + e.zPosition * tileX * ZStepInTiles;
                    return m.ToNormalized(rawX, rawY, rawZ);
                }

                var v = new List<float>(256);
                void Quad(Event e, (float r, float g, float b) col)
                {
                    bool sel = ReferenceEquals(e, _current);
                    var c = sel ? (1f, 1f, 1f) : col;
                    float half = (sel ? 0.46f : 0.40f);
                    float rawX = m.CellBaseX + (e.xMatrixPosition + (e.xMapPosition + 0.5f) / MapTiles) * m.CellStrideX;
                    float rawZ = m.CellBaseZ + (e.yMatrixPosition + (e.yMapPosition + 0.5f) / MapTiles) * m.CellStrideZ;
                    float rawY = ground + surfEps + e.zPosition * tileX * ZStepInTiles;
                    AddMarker(v, m, rawX, rawY, rawZ, half * tileX, half * tileZ, c);
                }

                foreach (var e in _file.warps) Quad(e, MarkerColor(1));
                foreach (var e in _file.triggers) Quad(e, MarkerColor(2));
                foreach (var e in _file.spawnables) Quad(e, MarkerColor(3));

                // Overworlds → real sprite billboards (foot anchored on the surface). Selected
                // overworlds also get a white ground ring so the selection is obvious.
                float spriteH = tileX * m.Scale * 1.6f;
                foreach (var ow in _file.overworlds)
                {
                    bool sel = ReferenceEquals(ow, _current);
                    if (sel) Quad(ow, (1f, 1f, 1f));

                    var pix = OverworldSprites.Get(ow.overlayTableEntry, ow.orientation);
                    var foot = Foot(ow);
                    if (pix != null && pix.Width > 0 && pix.Height > 0)
                    {
                        float halfH = spriteH * 0.5f;
                        float halfW = halfH * (pix.Width / (float)pix.Height);
                        sprites.Add(new NsbmdGlControl.SpriteInstance
                        {
                            Cx = foot.x, Cy = foot.y + halfH, Cz = foot.z,
                            HalfW = halfW, HalfH = halfH,
                            Rgba = pix.Rgba, Width = pix.Width, Height = pix.Height,
                        });
                    }
                    else Quad(ow, MarkerColor(0));   // fall back to a green quad if no sprite
                }

                MarkerMesh = v.ToArray();
                MarkerVertexCount = v.Count / 8;
            }
            Sprites = sprites;
            MarkersChanged?.Invoke(this, EventArgs.Empty);
            SpritesChanged?.Invoke(this, EventArgs.Empty);
        }

        private static void AddMarker(List<float> v, NsbmdRenderModel m, float cx, float cy, float cz,
            float halfX, float halfZ, (float r, float g, float b) col)
        {
            var a = m.ToNormalized(cx - halfX, cy, cz - halfZ);
            var b = m.ToNormalized(cx + halfX, cy, cz - halfZ);
            var c = m.ToNormalized(cx + halfX, cy, cz + halfZ);
            var d = m.ToNormalized(cx - halfX, cy, cz + halfZ);
            void Vtx((float x, float y, float z) p) { v.Add(p.x); v.Add(p.y); v.Add(p.z); v.Add(0); v.Add(0); v.Add(col.r); v.Add(col.g); v.Add(col.b); }
            Vtx(a); Vtx(b); Vtx(c);
            Vtx(a); Vtx(c); Vtx(d);
        }

        // ── Save / import / export ─────────────────────────────────────────────────────
        public void Save()
        {
            if (_file == null || _selectedIndex < 0) return;
            _file.SaveToFileDefaultDir(_selectedIndex, showSuccessMessage: false);
            SetClean();
            StatusText = $"Saved event file {_selectedIndex}.";
        }

        public async Task ImportAsync()
        {
            if (_selectedIndex < 0) return;
            var filter = new FilePickerFileType("Event file") { Patterns = new[] { "*.ev", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFile(_owner, "Import event file", new[] { filter });
            if (path == null) return;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read)) _file = new EventFile(fs);
                RefreshLists(); Dirty(); RefreshMarkers();
                StatusText = "Imported event file (unsaved).";
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }

        public async Task ExportAsync()
        {
            if (_file == null) return;
            var filter = new FilePickerFileType("Event file") { Patterns = new[] { "*.ev" } };
            string path = await DialogHelper.SaveFile(_owner, "Export event file", new[] { filter }, $"event_{_selectedIndex:D4}.ev");
            if (path == null) return;
            try { File.WriteAllBytes(path, _file.ToByteArray()); StatusText = "Exported."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }
    }
}
