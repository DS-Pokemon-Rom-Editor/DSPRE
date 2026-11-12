using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;
using System.Text;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class EventEditorViewModel
    {
        // Model overworlds are built into the scene like buildings, a moment after edits stop.
        private string _builtModelObjects = "";
        private global::Avalonia.Threading.DispatcherTimer _modelRebuild;

        private string ModelObjectsNow()
        {
            if (_file == null || !_showOw) return "";
            StringBuilder key = new StringBuilder();
            foreach (Overworld ow in _file.Overworlds)
                if (Overworld3DObjects.IsModel(ow.OverlayTableEntry))
                    key.Append(ow.OverlayTableEntry).Append(',').Append(ow.XMatrixPosition).Append(',').Append(ow.YMatrixPosition).Append(',')
                       .Append(ow.XMapPosition).Append(',').Append(ow.YMapPosition).Append(',').Append(ow.ZPosition).Append(';');
            return key.ToString();
        }

        private bool ModelObjectsAsBuilt => _builtModelObjects == ModelObjectsNow();

        private void RebuildForModelObjectsSoon()
        {
            if (_modelRebuild == null)
            {
                _modelRebuild = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                _modelRebuild.Tick += (_, _) =>
                {
                    _modelRebuild.Stop();
                    if (ModelObjectsAsBuilt || _matrix == null) return;
                    KeepViewOnLoad = true;
                    try { DisplayMap(); }
                    finally { KeepViewOnLoad = false; }
                };
            }
            _modelRebuild.Stop();
            _modelRebuild.Start();
        }

        private IEnumerable<PlacedBuilding> ModelObjectsOn(int cellX, int cellY, MapFile map, float altitudeY)
        {
            List<PlacedBuilding> placed = new List<PlacedBuilding>();
            if (_file == null || !_showOw) return placed;
            BdhcFile terrain = null;
            if (map?.Bdhc != null && BdhcFile.TryParse(map.Bdhc, out BdhcFile parsed)) terrain = parsed;
            foreach (Overworld ow in _file.Overworlds)
            {
                if (ow.XMatrixPosition != cellX || ow.YMatrixPosition != cellY || !Overworld3DObjects.IsModel(ow.OverlayTableEntry)) continue;
                NSBMD nsbmd = Overworld3DObjects.Load(ow.OverlayTableEntry);
                if (nsbmd == null) continue;
                NSBMDModel model = nsbmd.Models[0];

                float unit = NsbmdGeometry.TileSize / 16f;   // one field unit
                (float offsetX, float offsetY, float offsetZ) = Overworld3DObjects.Offset(ow.OverlayTableEntry);
                float localX = (ow.XMapPosition + 0.5f) * NsbmdGeometry.TileSize + offsetX * unit;
                float localZ = (ow.YMapPosition + 0.5f) * NsbmdGeometry.TileSize + offsetZ * unit;
                float y = 0f;
                if (terrain != null && terrain.TryGetHeight(localX, localZ, HeightHint(ow) - altitudeY, out float ground)) y = ground;
                y += offsetY * unit;
                float x = localX - NsbmdGeometry.MapStride / 2f;
                float z = localZ - NsbmdGeometry.MapStride / 2f;

                float scale = (model.ModelScale == 0 ? 1f : model.ModelScale) / 64f;
                placed.Add(new PlacedBuilding
                {
                    Model = model,
                    Transform = Mat4.Multiply(Mat4.Scale(scale, scale, scale), Mat4.Translate(x / scale, y / scale, z / scale)),
                    ModelId = -1 - (int)ow.OverlayTableEntry,
                    TileX = cellX * MapFile.MapSize + ow.XMapPosition,
                    TileZ = cellY * MapFile.MapSize + ow.YMapPosition,
                });
            }
            return placed;
        }
        public float[] MarkerMesh { get; private set; }
        public int MarkerVertexCount { get; private set; }

        public List<NsbmdGlControl.SpriteInstance> Sprites { get; private set; }
        public event EventHandler SpritesChanged;

        private string _mapInfo = "";
        public string MapInfo { get => _mapInfo; set => Set(ref _mapInfo, value); }

        public ObservableCollection<string> EventNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Spawnables { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Overworlds { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Warps { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Triggers { get; } = new ObservableCollection<string>();


        private static Dictionary<int, (ushort, byte, ushort, ushort)> BuildEventHeaderLookup()
        {
            Dictionary<int, (ushort, byte, ushort, ushort)> lookup = new Dictionary<int, (ushort, byte, ushort, ushort)>();
            try
            {
                int headerCount = GetHeaderCount();
                for (ushort h = 0; h < headerCount; h++)
                {
                    try
                    {
                        MapHeader header = MapHeader.GetMapHeader(h);
                        if (header == null) continue;
                        if (!lookup.ContainsKey(header.EventFileID))
                            lookup[header.EventFileID] = (header.MatrixID, header.AreaDataID, header.ScriptFileID, h);
                    }
                    catch { /* skip bad header */ }
                }
            }
            catch (Exception ex) { AppLogger.Error("Event to header lookup failed: " + ex.Message); }
            return lookup;
        }

        private void ResolveMatrixForFile(int eventIndex)
        {
            _matrix = null; _areaDataId = 0; _matrixId = -1; _headerId = -1;
            _collisionCache.Clear();
            int pairedScriptFileId = -1;
            try
            {
                if (_eventToHeader != null && _eventToHeader.TryGetValue(eventIndex, out (ushort matrixId, byte areaId, ushort scriptFileId, ushort headerId) hdr))
                {
                    _matrixId = hdr.Item1;
                    _matrix = new GameMatrix(hdr.Item1);
                    _areaDataId = hdr.Item2;
                    pairedScriptFileId = hdr.Item3;
                    _headerId = hdr.Item4;
                }
            }
            catch (Exception ex) { AppLogger.Error("Matrix resolve failed: " + ex.Message); }
            // A file no header uses comes back on the matrix and textures picked for it last time.
            (int Matrix, int Area) chosen = (-1, 0);
            bool hasChoice = _headerId < 0 && _viewChoices.TryGetValue(eventIndex, out chosen);
            if (_headerId < 0) FillViewMatrixNames();
            _settingView = true;
            ViewMatrixIndex = hasChoice ? chosen.Matrix : -1;
            ViewArea = hasChoice ? chosen.Area : 0;
            _settingView = false;
            OnPropertyChanged(nameof(NeedsViewChoice));
            if (hasChoice) { ShowOnChosen(); PopulateAvailableScripts(pairedScriptFileId); return; }
            OnPropertyChanged(nameof(GridToggleEnabled));
            PopulateAvailableScripts(pairedScriptFileId);
            DisplayMap();
        }

        public bool NeedsViewChoice => _headerId < 0;
        private readonly Dictionary<int, (int Matrix, int Area)> _viewChoices = new Dictionary<int, (int Matrix, int Area)>();
        private bool _settingView;
        // A list, not a number box: "nothing picked yet" has to be a state the box keeps.
        public ObservableCollection<string> ViewMatrixNames { get; } = new ObservableCollection<string>();
        private int _viewMatrixIndex = -1;
        private decimal _viewArea;
        public int ViewMatrixIndex { get => _viewMatrixIndex; set { if (Set(ref _viewMatrixIndex, value) && !_settingView) ShowOnChosen(); } }
        public decimal ViewArea { get => _viewArea; set { if (Set(ref _viewArea, value) && !_settingView) ShowOnChosen(); } }

        private void FillViewMatrixNames()
        {
            int count = Filesystem.GetMatrixCount();
            if (ViewMatrixNames.Count == count) return;
            ViewMatrixNames.Clear();
            for (int i = 0; i < count; i++)
            {
                string name;
                try { name = new GameMatrix(i).ToString(); }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is EndOfStreamException) { name = "Matrix " + i; }
                ViewMatrixNames.Add(name);
            }
        }
        // Bound before any ROM is open, when there are no area data files to count.
        public decimal MaxViewArea => RomInfo.GameDirs?.ContainsKey(RomInfo.DirNames.AreaData) == true
            ? Math.Clamp(Filesystem.GetAreaDataCount() - 1, 0, byte.MaxValue) : 0;

        private void ShowOnChosen()
        {
            if (_headerId >= 0 || _viewMatrixIndex < 0) return;
            int matrixId = _viewMatrixIndex;
            try { _matrix = new GameMatrix(matrixId); }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is EndOfStreamException)
            {
                StatusText = $"Matrix {matrixId} couldn't be read: {ex.Message}";
                return;
            }
            _matrixId = matrixId;
            _areaDataId = (byte)_viewArea;
            if (_selectedIndex >= 0) _viewChoices[_selectedIndex] = (matrixId, _areaDataId);
            _collisionCache.Clear();
            OnPropertyChanged(nameof(GridToggleEnabled));
            DisplayMap();
        }

        public ObservableCollection<string> AvailableScripts { get; } = new ObservableCollection<string>();
        private readonly List<uint> _availableScriptIds = new List<uint>();

        private int _pairedScriptFileId = -1;
        private void PopulateAvailableScripts(int scriptFileId)
        {
            _pairedScriptFileId = scriptFileId;
            AvailableScripts.Clear();
            _availableScriptIds.Clear();
            _availableScriptIds.Add(EventFile.NoScript);
            AvailableScripts.Add("No script");
            if (scriptFileId >= 0)
            {
                try
                {
                    ScriptFile scriptFile = new ScriptFile(scriptFileId);
                    foreach (ScriptCommandContainer container in scriptFile.AllScripts)
                    {
                        _availableScriptIds.Add(container.ManualUserID);
                        AvailableScripts.Add($"Script {container.ManualUserID} ({Plural.Of(container.Commands?.Count ?? 0, "command")})");
                    }
                }
                catch (Exception ex) { AppLogger.Error("PopulateAvailableScripts: " + ex.Message); }
            }
            OnPropertyChanged(nameof(TrScriptIndex)); OnPropertyChanged(nameof(TrScriptIndexOutOfRange)); OnPropertyChanged(nameof(TrScriptCommonInfo)); OnPropertyChanged(nameof(TrScriptHasCommonInfo)); OnPropertyChanged(nameof(TrScriptCommonName)); OnPropertyChanged(nameof(TrScriptGenericWarningVisible));
            OnPropertyChanged(nameof(SpScriptIndex)); OnPropertyChanged(nameof(SpScriptIndexOutOfRange)); OnPropertyChanged(nameof(SpScriptCommonInfo)); OnPropertyChanged(nameof(SpScriptHasCommonInfo)); OnPropertyChanged(nameof(SpScriptCommonName)); OnPropertyChanged(nameof(SpScriptGenericWarningVisible)); RaiseSpHidden();
            OnPropertyChanged(nameof(OwScriptIndex)); OnPropertyChanged(nameof(OwScriptIndexOutOfRange));
            OnPropertyChanged(nameof(OwScriptCommonInfo));
            OnPropertyChanged(nameof(OwScriptHasCommonInfo)); OnPropertyChanged(nameof(OwScriptCommonName));
            OnPropertyChanged(nameof(OwScriptGenericWarningVisible));
        }

        private int IndexOfAvailableScript(decimal rawValue) => _availableScriptIds.IndexOf((uint)rawValue);

        private int _matrixId = -1;
        private int _headerId = -1;

        // Only this header's maps, so a world-sized matrix isn't loaded whole.
        private void DisplayMap()
        {
            Model3D = null;
            try
            {
                if (_matrix == null)
                {
                    MapInfo = "No header uses it";
                    MapLoaded?.Invoke(this, EventArgs.Empty); RefreshMarkers(); return;
                }

                // The cells this event file's header owns come first, as in the map editor's This header.
                ISet<(int x, int y)> include = HeaderCells();
                string scopeName = "this header";

                // The catch-all header owns hundreds of unwalked squares. Stitching them is a long wait.
                if (include != null && FieldCatchAllHeader.IsCatchAll(include.Count))
                {
                    Model3D = null;
                    IsCatchAllHeader = true;
                    StatusText = FieldCatchAllHeader.Explanation;
                    MapLoaded?.Invoke(this, EventArgs.Empty);
                    return;
                }
                IsCatchAllHeader = false;

                if (include == null)
                {
                    // With no headers section, small matrices render whole and big ones use the events' box.
                    int total = _matrix.Width * _matrix.Height;
                    include = total <= 256 ? null : EventCells();
                    scopeName = "events";
                }

                _builtModelObjects = ModelObjectsNow();
                Model3D = MatrixSceneBuilder.Build(_matrix, _areaDataId, GameFamily, areaForMap: null, includeCells: include, mode: StitchMode,
                                                   extrasForCell: ModelObjectsOn);
                string scope = include == null ? "full" : $"{include.Count} {(include.Count == 1 ? "map" : "maps")}, {scopeName}";
                MapInfo = Model3D != null
                    ? $"Matrix {_matrixId}  ·  {_matrix.Width}×{_matrix.Height} ({scope})  ·  area {_areaDataId}"
                    : $"Matrix {_matrixId}: no renderable maps.";
            }
            catch (Exception ex)
            {
                MapInfo = "Map render failed: " + ex.Message;
                AppLogger.Error("Event map render failed: " + ex.Message);
            }
            MapLoaded?.Invoke(this, EventArgs.Empty);
            RefreshMarkers();
        }

        private HashSet<(int x, int y)> HeaderCells() => _matrix?.CellsOfHeader(_headerId);

        // The box around every cell with an event, capped against a stray far-off event.
        private HashSet<(int x, int y)> EventCells()
        {
            HashSet<(int x, int y)> set = new HashSet<(int x, int y)>();
            if (_file == null || _matrix == null) return set;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            void Note(Event e)
            {
                int x = e.XMatrixPosition, y = e.YMatrixPosition;
                if (x < 0 || y < 0 || x >= _matrix.Width || y >= _matrix.Height) return;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
            foreach (Overworld e in _file.Overworlds) Note(e);
            foreach (Warp e in _file.Warps) Note(e);
            foreach (Trigger e in _file.Triggers) Note(e);
            foreach (Spawnable e in _file.Spawnables) Note(e);
            if (maxX < minX) return set;   // no events

            const int MaxSpan = 12;
            if (maxX - minX > MaxSpan) maxX = minX + MaxSpan;
            if (maxY - minY > MaxSpan) maxY = minY + MaxSpan;

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    set.Add((x, y));
            return set;
        }

        // Cell 0 is plain white so a quad keeps its colour.
        private const int IconWhite = 0, IconWarp = 1, IconSpawnable = 2, IconTrigger = 3, IconOverworld = 4;
        private static readonly string[] IconNames = { null, "warp", "sign", "trigger", "overworld" };
        private const int IconCell = 32;
        private static (byte[] Rgba, int Width, int Height, (float u0, float v0, float u1, float v1)[] Uv) _markerIcons;

        public static (byte[] Rgba, int Width, int Height) MarkerIconSheet
        {
            get { BuildMarkerIcons(); return (_markerIcons.Rgba, _markerIcons.Width, _markerIcons.Height); }
        }

        private static void BuildMarkerIcons()
        {
            if (_markerIcons.Rgba != null) return;
            int width = IconCell * IconNames.Length;
            byte[] rgba = new byte[width * IconCell * 4];
            (float, float, float, float)[] uv = new (float, float, float, float)[IconNames.Length];
            for (int i = 0; i < IconNames.Length; i++)
            {
                RawImage img = IconNames[i] == null ? null : ResourceImages.GetRaw(IconNames[i]);
                int w = img == null ? 1 : Math.Min(img.Width, IconCell), h = img == null ? 1 : Math.Min(img.Height, IconCell);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int to = (y * width + i * IconCell + x) * 4;
                        if (img == null) { rgba[to] = rgba[to + 1] = rgba[to + 2] = rgba[to + 3] = 255; continue; }
                        int from = (y * img.Width + x) * 4;
                        rgba[to] = img.Bgra[from + 2]; rgba[to + 1] = img.Bgra[from + 1]; rgba[to + 2] = img.Bgra[from]; rgba[to + 3] = img.Bgra[from + 3];
                    }
                // Half a texel in from each edge, so a neighbouring cell never bleeds in.
                uv[i] = ((i * IconCell + 0.5f) / width, 0.5f / IconCell, (i * IconCell + w - 0.5f) / width, (h - 0.5f) / IconCell);
            }
            _markerIcons = (rgba, width, IconCell, uv);
        }

        private const int MapTiles = 32;

        public void RefreshMarkers()
        {
            MarkerMesh = null; MarkerVertexCount = 0;
            List<NsbmdGlControl.SpriteInstance> sprites = new List<NsbmdGlControl.SpriteInstance>();
            NsbmdRenderModel m = Model3D;
            if (_file != null && m != null && m.CellStrideX != 0)
            {
                BuildMarkerIcons();
                List<float> v = new List<float>(256);
                if (_showGrid && _matrix != null && _matrix.Width * _matrix.Height <= 4) AddGridMarkers(v, m);
                if (_showWarp) foreach (Warp e in _file.Warps) AddIcon(v, m, e, IconWarp);
                if (_showTrig)
                    foreach (Trigger e in _file.Triggers)
                        foreach ((int dx, int dz) in TriggerTiles(e)) AddIcon(v, m, e, IconTrigger, dx, dz);
                if (_showSpawn) foreach (Spawnable e in _file.Spawnables) AddIcon(v, m, e, IconSpawnable);

                bool modelsAsBuilt = ModelObjectsAsBuilt;
                if (!modelsAsBuilt) RebuildForModelObjectsSoon();
                if (_showOw) AddOverworldMarkers(v, sprites, m, modelsAsBuilt);

                MarkerMesh = v.ToArray();
                MarkerVertexCount = v.Count / 8;
            }
            Sprites = sprites;
            MarkersChanged?.Invoke(this, EventArgs.Empty);
            SpritesChanged?.Invoke(this, EventArgs.Empty);
            GizmoTargetChanged?.Invoke(this, EventArgs.Empty);   // keep the move-gizmo on the selected event
        }

        private (float x, float y, float z) MarkerFoot(NsbmdRenderModel m, Event e)
        {
            (float rawX, float rawZ) = EventCellRaw(m, e);
            return m.ToNormalized(rawX, EventSurfaceY(m, rawX, rawZ, e), rawZ);
        }

        private void AddIcon(List<float> v, NsbmdRenderModel m, Event e, int icon, int dx = 0, int dz = 0)
        {
            float tileX = m.CellStrideX / MapTiles, tileZ = m.CellStrideZ / MapTiles;
            bool sel = ReferenceEquals(e, _current);
            (float rawX, float rawZ) = TileRaw(m, e, dx, dz);
            float y = EventSurfaceY(m, rawX, rawZ, e);
            if (sel) AddMarker(v, m, rawX, y, rawZ, 0.5f * tileX, 0.5f * tileZ, (1f, 1f, 1f), _markerIcons.Uv[IconWhite]);
            float half = sel ? 0.42f : 0.46f;
            AddMarker(v, m, rawX, y, rawZ, half * tileX, half * tileZ, (1f, 1f, 1f), _markerIcons.Uv[icon]);
        }

        private void AddGridMarkers(List<float> v, NsbmdRenderModel m)
        {
            float tileX = m.CellStrideX / MapTiles, tileZ = m.CellStrideZ / MapTiles;
            int n = MapFile.MapSize;
            float inset = tileX * 0.07f;
            for (int gcy = 0; gcy < _matrix.Height; gcy++)
                for (int gcx = 0; gcx < _matrix.Width; gcx++)
                {
                    if (!m.TryCellPlacement(gcx, gcy, out NsbmdRenderModel.CellPlacement gp)) continue;
                    long key = ((long)gcy << 32) | (uint)gcx;
                    if (!_collisionCache.TryGetValue(key, out byte[,] col))
                    {
                        col = null;
                        try
                        {
                            int map = _matrix.Maps[gcy, gcx];
                            if (map != GameMatrix.EMPTY) col = new MapFile(map, GameFamily, false, false).Collisions;
                        }
                        catch (Exception caught) when (AppLogger.Noted("EventEditorViewModel.RefreshMarkers", caught)) { }   // a map that can't be read has no collisions to show
                        _collisionCache[key] = col;
                    }
                    if (col == null) continue;
                    byte oob = col[n - 1, n - 1];
                    for (int ty = 0; ty < n; ty++)
                        for (int tx = 0; tx < n; tx++)
                        {
                            if (col[ty, tx] != oob) continue;
                            float x0 = gp.OriginX + tx * tileX;
                            float z0 = gp.OriginZ + ty * tileZ;
                            float cx = x0 + tileX * 0.5f, cz = z0 + tileZ * 0.5f;
                            if (!m.TryBdhcSurfaceY(gcx, gcy, cx, cz, 0f, out float yc)) yc = m.SurfaceY(cx, cz);
                            yc += 0.01f;
                            AddFlatQuad(v, m, x0 + inset, z0 + inset, x0 + tileX - inset, z0 + tileZ - inset, yc, (0.20f, 0.55f, 0.95f), _markerIcons.Uv[IconWhite]);
                        }
                }
        }

        private void AddOverworldMarkers(List<float> v, List<NsbmdGlControl.SpriteInstance> sprites, NsbmdRenderModel m, bool modelsAsBuilt)
        {
            float tileX = m.CellStrideX / MapTiles, tileZ = m.CellStrideZ / MapTiles;
            float unit = tileX * m.Scale / OverworldSprites.PixelsPerTile;
            foreach (Overworld ow in _file.Overworlds)
            {
                bool sel = ReferenceEquals(ow, _current);
                // Markers draw over sprites, so the selection is a frame that leaves a small sprite showing.
                if (sel)
                {
                    (float rawX, float rawZ) = EventCellRaw(m, ow);
                    float y = EventSurfaceY(m, rawX, rawZ, ow), hx = 0.5f * tileX, hz = 0.5f * tileZ, t = 0.08f * tileX;
                    (float, float, float, float) white = _markerIcons.Uv[IconWhite];
                    AddFlatQuad(v, m, rawX - hx, rawZ - hz, rawX + hx, rawZ - hz + t, y, (1f, 1f, 1f), white);
                    AddFlatQuad(v, m, rawX - hx, rawZ + hz - t, rawX + hx, rawZ + hz, y, (1f, 1f, 1f), white);
                    AddFlatQuad(v, m, rawX - hx, rawZ - hz, rawX - hx + t, rawZ + hz, y, (1f, 1f, 1f), white);
                    AddFlatQuad(v, m, rawX + hx - t, rawZ - hz, rawX + hx, rawZ + hz, y, (1f, 1f, 1f), white);
                }
                // Drawn as its model once the scene is rebuilt. Until then its sprite stands in.
                if (Overworld3DObjects.IsModel(ow.OverlayTableEntry) && modelsAsBuilt) continue;

                OverworldSprites.SpritePixels pix = OverworldSprites.Get(ow.OverlayTableEntry, (ushort)Math.Max((short)0, ow.Orientation));
                (float x, float y, float z) foot = MarkerFoot(m, ow);
                if (pix != null && pix.Width > 0 && pix.Height > 0)
                {
                    // A script picks a dynamic overworld's sprite while playing, so its picture is outlined.
                    if (IsVariableOverworld(ow.OverlayTableEntry)) pix = OverworldSprites.Outlined(pix);
                    float halfH = unit * pix.Height * 0.5f;
                    float halfW = unit * pix.Width * 0.5f;
                    sprites.Add(new NsbmdGlControl.SpriteInstance
                    {
                        Cx = foot.x,
                        Cy = foot.y + halfH,
                        Cz = foot.z,
                        HalfW = halfW,
                        HalfH = halfH,
                        Rgba = pix.Rgba,
                        Width = pix.Width,
                        Height = pix.Height,
                    });
                }
                else AddIcon(v, m, ow, IconOverworld);
            }
        }

        private (float x, float z) EventCellRaw(NsbmdRenderModel m, Event e) => TileRaw(m, e, 0, 0);

        private static (float x, float z) TileRaw(NsbmdRenderModel m, Event e, int dx, int dz)
        {
            int tx = e.XMapPosition + dx, tz = e.YMapPosition + dz;
            int mx = e.XMatrixPosition + FloorDiv(tx, MapTiles), mz = e.YMatrixPosition + FloorDiv(tz, MapTiles);
            tx -= FloorDiv(tx, MapTiles) * MapTiles; tz -= FloorDiv(tz, MapTiles) * MapTiles;
            if (mx >= 0 && mz >= 0 && m.TryCellPlacement(mx, mz, out NsbmdRenderModel.CellPlacement p))
                return (p.OriginX + (tx + 0.5f) / MapTiles * p.Width,
                        p.OriginZ + (tz + 0.5f) / MapTiles * p.Height);
            return (m.CellBaseX + (mx + (tx + 0.5f) / MapTiles) * m.CellStrideX,
                    m.CellBaseZ + (mz + (tz + 0.5f) / MapTiles) * m.CellStrideZ);
        }

        // A trigger covers width by height tiles. With either at 0 it never fires but still shows.
        private static IEnumerable<(int dx, int dz)> TriggerTiles(Trigger t)
        {
            if (t.WidthX == 0 || t.HeightY == 0) { yield return (0, 0); yield break; }
            for (int dz = 0; dz < t.HeightY; dz++)
                for (int dx = 0; dx < t.WidthX; dx++)
                    yield return (dx, dz);
        }

        // Events stand on the map surface at their tile. A height only picks the floor.
        private float EventSurfaceY(NsbmdRenderModel m, float rawX, float rawZ, Event e)
        {
            float yHint = HeightHint(e);
            if (m.TryBdhcSurfaceY(e.XMatrixPosition, e.YMatrixPosition, rawX, rawZ, yHint, out float bdhcY)) return bdhcY;
            return e.ZPosition == 0 ? m.SurfaceY(rawX, rawZ) : m.SurfaceY(rawX, rawZ, yHint);
        }
    }
}
