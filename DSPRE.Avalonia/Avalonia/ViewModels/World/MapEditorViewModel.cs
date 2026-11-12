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
    public sealed class PainterOption
    {
        public byte Value { get; }
        public string Name { get; }
        public PainterOption(byte v, string n) { Value = v; Name = n; }
        public override string ToString() => Name;
    }

    /// <summary>One map belonging to the currently viewed header, kept loaded (not discarded after
    /// render) so painting/building edits can target it and it can be saved individually.</summary>
    internal sealed class HeaderMapCell
    {
        public int CellX, CellY, MapIndex;
        public byte AreaId;
        public float AltitudeY;
        public MapFile Map;
        public bool Dirty;
    }

    /// <summary>Edits map permissions, buildings and models, one map or a whole header.</summary>
    public partial class MapEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
    {
        /// <summary>Walking, the animated preview and drag gizmos are unfinished, so beta only.</summary>
        public bool ShowBetaFeatures => BetaEditors.Enabled;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private MapFile _map;

        public MapModelEditorViewModel MapModel { get; } = new MapModelEditorViewModel();

        private bool _watchingGeometry;

        private void WatchGeometry()
        {
            if (_watchingGeometry) return;
            _watchingGeometry = true;
            MapModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MapModelEditorViewModel.Changed) && MapModel.Changed)
                    MarkDirty();
            };
        }
        private Dictionary<int, byte> _mapToArea;   // map index → areaDataID (for the correct tileset)

        private readonly List<HeaderMapCell> _headerCells = new List<HeaderMapCell>();
        // Flat Buildings-list index → (cell index in _headerCells, building index within that map).
        private readonly List<(int CellIndex, int BuildingIndex)> _headerBuildingIndex = new List<(int, int)>();

        /// <summary>Raised after each preview rebuild, so the view refreshes GL and grids.</summary>
        public event EventHandler MapLoaded;

        public ObservableCollection<string> MapNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Buildings { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> MapTilesets { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> BuildingTilesets { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> ViewModes { get; } = new ObservableCollection<string> { "Single map", "Full matrix", "This header" };
        public ObservableCollection<string> Matrices { get; } = new ObservableCollection<string>();

        private int _viewModeIndex;
        public int ViewModeIndex
        {
            get => _viewModeIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value != _viewModeIndex && _dirty && !_suppress)
                {
                    // Every view reloads from disk, so ask first and leave the picker where it was.
                    RecordSwitchGuard.SnapBack(() => _viewModeIndex, v => _viewModeIndex = v, () => OnPropertyChanged(nameof(ViewModeIndex)));
                    if (!_switchPending) _ = SwitchAfterGuardAsync("view", () => ViewModeIndex = value);
                    return;
                }
                if (Set(ref _viewModeIndex, value))
                {
                    OnPropertyChanged(nameof(IsSingleMap)); OnPropertyChanged(nameof(IsMatrixView)); OnPropertyChanged(nameof(NoHeaderNote));
                    OnPropertyChanged(nameof(IsHeaderView)); OnPropertyChanged(nameof(CanEdit));
                    OnPropertyChanged(nameof(IsStitchedView));
                    OnPropertyChanged(nameof(MeshToggleEnabled));
                    RefreshView();
                    OnPropertyChanged(nameof(CanEditModel));
                    OnPropertyChanged(nameof(FocusedMapIndex));
                }
            }
        }
        public bool IsSingleMap => _viewModeIndex == 0;
        public bool IsMatrixView => _viewModeIndex == 1;
        public bool IsHeaderView => _viewModeIndex == 2;
        public bool CanEdit => IsSingleMap || IsHeaderView;
        public bool IsStitchedView => IsMatrixView || IsHeaderView;

        /// <summary>Headers for a standalone window's This header picker, by header ID.</summary>
        public ObservableCollection<string> HeaderNames { get; } = new ObservableCollection<string>();

        // Header that This header shows, from the Maps workspace or its picker.
        private int _headerId = -1;
        private bool _headerNavigationPending;
        public int HeaderId
        {
            get => _headerId;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack || _headerId == value) return;
                if (!IsValidHeaderId(value))
                {
                    OnPropertyChanged(nameof(SelectedHeaderIndex));
                    return;
                }

                if (HasUnsavedChanges)
                {
                    RecordSwitchGuard.SnapBack(() => _headerId, v => _headerId = v, () => OnPropertyChanged(nameof(SelectedHeaderIndex)));
                    if (!_headerNavigationPending)
                    {
                        _ = ConfirmHeaderNavigationAsync(value);
                    }
                    return;
                }

                ApplyHeaderId(value);
            }
        }

        /// <summary>
        /// The camera number this header asks for, so the preview frames the map the way the games do.
        /// </summary>
        public int CameraId
        {
            get
            {
                try { return _headerId < 0 ? 0 : MapHeader.GetMapHeader((ushort)_headerId).CameraAngleID; }
                catch (Exception ex) { AppLogger.WarnOnce("MapEditorViewModel.CameraId", ex); return 0; }
            }
        }

        /// <summary>The two music numbers this header carries, day and night.</summary>
        public int MusicDayId => HeaderMusic(true);
        public int MusicNightId => HeaderMusic(false);

        private int HeaderMusic(bool day)
        {
            try
            {
                if (_headerId < 0) return 0;
                MapHeader h = MapHeader.GetMapHeader((ushort)_headerId);
                return day ? h.MusicDayID : h.MusicNightID;
            }
            catch (Exception ex) { AppLogger.WarnOnce("MapEditorViewModel.HeaderMusic", ex); return 0; }
        }

        /// <summary>Alias for HeaderId, so the standalone picker and the sidebar share one path.</summary>
        public int SelectedHeaderIndex { get => _headerId; set => HeaderId = value; }

        private void ApplyHeaderId(int value)
        {
            _headerId = value;
            OnPropertyChanged(nameof(SelectedHeaderIndex));
            if (IsHeaderView) BuildHeaderPreview();
        }

        private bool IsValidHeaderId(int value)
            => value >= -1 && (HeaderNames.Count == 0 || value < HeaderNames.Count);

        // Grid gives every map a 32-tile span, as the DS does. Continuous sizes cells by geometry.
        private bool _stitchGrid = true;
        public bool StitchGrid
        {
            get => _stitchGrid;
            set
            {
                if (!Set(ref _stitchGrid, value)) return;
                if (IsMatrixView && _selectedMatrix >= 0) BuildMatrixPreview();
                else if (IsHeaderView) BuildHeaderPreview();
            }
        }
        private NsbmdGeometry.MatrixStitchMode StitchMode => _stitchGrid ? NsbmdGeometry.MatrixStitchMode.Grid : NsbmdGeometry.MatrixStitchMode.Continuous;

        private int _selectedMatrix = -1;
        public int SelectedMatrixIndex { get => _selectedMatrix; set { if (Set(ref _selectedMatrix, value) && !_suppress && value >= 0 && IsMatrixView) BuildMatrixPreview(); } }

        private string _matrixInfo = "";
        public string MatrixInfo { get => _matrixInfo; set => Set(ref _matrixInfo, value); }

        private int _mapTilesetIndex = -1;
        public int MapTilesetIndex { get => _mapTilesetIndex; set { if (Set(ref _mapTilesetIndex, value) && !_suppress && _map != null) { EditArea(a => a.MapTileset = (ushort)value); RebuildPreview(); } } }

        private int _buildingTilesetIndex;
        public int BuildingTilesetIndex { get => _buildingTilesetIndex; set { if (Set(ref _buildingTilesetIndex, value) && !_suppress && _map != null) { EditArea(a => a.BuildingsTileset = (ushort)(value - 1)); RebuildPreview(); } } }

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty || _archive.IsDirty;

        // Added and removed map files and kept-plate clears wait here, so Discard undoes them.
        private readonly StagedArchive _archive = new(DirNames.Maps);
        private readonly HashSet<int> _platesToClear = new();

        private void ListMaps()
        {
            int count = _archive.Count;
            MapNames.Clear();
            for (int i = 0; i < count; i++) MapNames.Add("Map " + i);
        }

        private void SaveStagedMaps()
        {
            _archive.Save();
            foreach (int id in _platesToClear) KeptPlatesFile.Save(id, null);
            _platesToClear.Clear();
        }
        public string UnsavedChangesDescription
            => StagedArchive.Join(IsHeaderView ? $"Header {_headerId} maps" : $"Map {_selectedMapIndex}", _dirty, _archive.Describe("map", "maps"));
        public void SaveChanges() => Save();
        public void DiscardChanges()
        {
            _dirty = false;
            _archive.Discard();
            _platesToClear.Clear();
            _suppress = true;
            ListMaps();
            if (_selectedMapIndex >= MapNames.Count) _selectedMapIndex = MapNames.Count - 1;
            _suppress = false;
            OnPropertyChanged(nameof(SelectedMapIndex));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            ClearPendingEvents();
            _areaDirty = false;

            if (IsHeaderView) BuildHeaderPreview();               // reloads every cell's map fresh from disk
            else if (_selectedMapIndex >= 0) LoadMap(_selectedMapIndex);
            else MapModel.Open(null, 0, GameFamily, null);
        }
        /// <summary>Every header shown over a matrix cell that holds the map, with that cell.</summary>
        public List<(ushort header, int x, int y)> HeadersUsingMap(int mapIndex)
        {
            List<(ushort, int, int)> found = new List<(ushort, int, int)>();
            if (mapIndex < 0) return found;
            try
            {
                int headerCount = GetHeaderCount();
                Dictionary<int, List<ushort>> byMatrix = new Dictionary<int, List<ushort>>();
                for (ushort h = 0; h < headerCount; h++)
                {
                    try
                    {
                        MapHeader header = MapHeader.GetMapHeader(h);
                        if (header == null) continue;
                        if (!byMatrix.TryGetValue(header.MatrixID, out List<ushort> list)) byMatrix[header.MatrixID] = list = new List<ushort>();
                        list.Add(h);
                    }
                    catch (Exception caught) when (AppLogger.Noted("MapEditorViewModel.HeadersUsingMap.2", caught)) { }   // a header that can't be read is skipped
                }
                for (int mid = 0; mid < Filesystem.GetMatrixCount(); mid++)
                {
                    GameMatrix mtx;
                    try { mtx = new GameMatrix(mid); } catch (Exception ex) { AppLogger.WarnOnce("MapEditorViewModel.HeadersUsingMap", ex); continue; }
                    for (int y = 0; y < mtx.Height; y++)
                        for (int x = 0; x < mtx.Width; x++)
                        {
                            if (mtx.Maps[y, x] != mapIndex) continue;
                            if (mtx.HasHeadersSection) found.Add((mtx.Headers[y, x], x, y));
                            else if (byMatrix.TryGetValue(mid, out List<ushort> l)) found.AddRange(l.Select(h => (h, x, y)));
                        }
                }
            }
            catch (Exception ex) { AppLogger.Error("Header scan failed: " + ex.Message); }
            return found;
        }

        public void AfterMapModelEdited()
        {
            if (MapModel.Changed)
            {
                if (IsHeaderView && _selectedHeaderMap >= 0 && _selectedHeaderMap < _headerCells.Count) _headerCells[_selectedHeaderMap].Dirty = true;
                MarkDirty();
            }
            RefreshBuildings();
            // The tiles editor numbers buildings within its one map; the header view lists every map's.
            if (MapModel.Tiles.SelectedBuilding is int picked && picked >= 0)
            {
                int shown = IsHeaderView
                    ? _headerBuildingIndex.FindIndex(t => t.CellIndex == _selectedHeaderMap && t.BuildingIndex == picked)
                    : picked;
                if (shown >= 0 && shown < Buildings.Count) SelectedBuildingIndex = shown;
            }

            // Adding textures makes a new pack and points the area at it. Rereading the area would drop
            // area edits still pending here.
            if (IsSingleMap && !_areaDirty) ResolveTilesetForMap(_selectedMapIndex);
            else SyncTilesetsToArea();

            RebuildPreview();
            OnPropertyChanged(nameof(Collisions));
            OnPropertyChanged(nameof(Types));
            OnPropertyChanged(nameof(UnsavedChangesDescription));
        }

        public void MarkDirty() { RecordUndo(); if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // Notifies even when only staged members were waiting, so the title mark clears after a save.
        private void SetClean() { _history.MarkSaved(); _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private async Task ConfirmHeaderNavigationAsync(int newHeaderId)
        {
            _headerNavigationPending = true;
            try
            {
                if (!await global::DSPRE.Avalonia.UnsavedChangesDialog.ShowIfNeededAsync(
                    _owner, this, "Map header"))
                {
                    return;
                }

                if (!IsValidHeaderId(newHeaderId))
                {
                    return;
                }

                ApplyHeaderId(newHeaderId);
            }
            catch (Exception ex) when (AppLogger.Noted("MapEditorViewModel.ConfirmHeaderNavigationAsync", ex))
            {
                AppLogger.Error("Map header navigation failed: " + ex);
                await DialogHelper.ShowErrorAsync($"Couldn't switch headers:\n{ex.Message}", "Map editor");
            }
            finally
            {
                _headerNavigationPending = false;
                OnPropertyChanged(nameof(SelectedHeaderIndex));
            }
        }

        public MapEditorViewModel() { if (Design.IsDesignMode) MapNames.Add("Map 0"); }
        public MapEditorViewModel(bool _) { }

        /// <summary>Map a standalone window opens on; -1 opens on the first.</summary>
        public int InitialMapIndex { get; set; } = -1;

        // SetupAsync runs on every ROM switch, so subscribe only once.
        private bool _romPatchHandlerSubscribed;

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> {
                    DirNames.Maps, DirNames.ExteriorBuildingModels, DirNames.BuildingTextures, DirNames.MapTextures,
                    DirNames.Matrices, DirNames.AreaData, DirNames.DynamicHeaders, DirNames.SynthOverlay });
                if (GameFamily == GameFamilies.HGSS)
                    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.InteriorBuildingModels });
                _mapToArea = BuildMapAreaLookup();
                RefreshBuildingRotationPatchState();
                if (!_romPatchHandlerSubscribed)
                {
                    _romPatchHandlerSubscribed = true;
                    AppEvents.RomPatchStateChanged += OnRomPatchStateChanged;
                    AppEvents.MapSaved += OnMapSavedElsewhere;
                    AppEvents.AreaDataSaved += OnAreaSavedElsewhere;
                    AppEvents.EventFileSaved += OnEventFileSavedElsewhere;
                }

                // Rebuilt on every ROM load, so cleared first.
                CollisionPainters.Clear();
                TypePainters.Clear();
                foreach (TileCollision c in TilePermissions.CollisionsFor(GameFamily)) CollisionPainters.Add(new PainterOption(c.Value, c.Label));
                foreach (TileBehaviour b in TilePermissions.BehavioursFor(GameFamily)) TypePainters.Add(new PainterOption(b.Value, b.Label));
                CollisionNames.Clear(); CollisionKeys.Clear(); TypeNames.Clear(); TypeKeys.Clear();
                foreach (PainterOption o in CollisionPainters) { CollisionNames.Add(o.Name); CollisionKeys.Add(o.Value); }
                foreach (PainterOption o in TypePainters) { TypeNames.Add(o.Name); TypeKeys.Add(o.Value); }
                CollisionPainterIndex = Math.Max(0, CollisionPainters.ToList().FindIndex(p => p.Value == TilePermissions.BlockedBit));
                if (TypePainters.Count > 0) TypePainterIndex = 0;
                OnPropertyChanged(nameof(CollisionValue));
                OnPropertyChanged(nameof(TypeValue));

                _suppress = true;
                MapTilesets.Clear();
                BuildingTilesets.Clear();
                int mapTexCount = Filesystem.GetMapTexturesCount();
                for (int i = 0; i < mapTexCount; i++) MapTilesets.Add("Map textures " + i);
                BuildingTilesets.Add("None");
                int bldTexCount = Filesystem.GetBuildingTexturesCount();
                for (int i = 0; i < bldTexCount; i++) BuildingTilesets.Add("Building textures " + i);
                // The first pack until the area data is read, so the map shows textured.
                if (MapTilesets.Count > 0) MapTilesetIndex = 0;
                _suppress = false;

                ListMaps();
                int count = MapNames.Count;

                Matrices.Clear();
                int matrixCount = Filesystem.GetMatrixCount();
                for (int i = 0; i < matrixCount; i++) Matrices.Add("Matrix " + i);
                _suppress = true; if (matrixCount > 0) { _selectedMatrix = 0; OnPropertyChanged(nameof(SelectedMatrixIndex)); } _suppress = false;

                HeaderNames.Clear();
                IReadOnlyList<string> headerNames = HeaderLabels.Friendly();
                if (headerNames != null) foreach (string n in headerNames) HeaderNames.Add(n);
                // A standalone window has no sidebar to set HeaderId, so default to the first.
                if (_headerId < 0 && HeaderNames.Count > 0) HeaderId = 0;

                StatusText = $"{count} maps.";
                if (count > 0) SelectedMapIndex = InitialMapIndex >= 0 && InitialMapIndex < count ? InitialMapIndex : 0;
            }
            catch (Exception ex) when (AppLogger.Noted("MapEditorViewModel.SetupAsync", ex))
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowErrorAsync($"Couldn't set up the Map editor:\n{ex.Message}", "Map editor");
            }
        }
    }
}
