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
        // Held with the map's edits and written by the same Save.
        private AreaData _area;
        private byte _areaId;
        private bool _areaDirty;

        public bool HasArea => _area != null;

        public string NoHeaderNote => IsSingleMap && _map != null && _area == null && _selectedMapIndex >= 0
            ? $"No header uses map {_selectedMapIndex}, so the game never loads it. Pick area data to preview its textures."
            : null;

        private readonly Dictionary<int, int> _previewAreas = new Dictionary<int, int>();
        private decimal _previewArea;
        public decimal PreviewArea
        {
            get => _previewArea;
            set { if (Set(ref _previewArea, value) && !_suppress && _area == null && _map != null) { UsePreviewArea(); RebuildPreview(); } }
        }
        // Bound before any ROM is open, when there are no area data files to count.
        public decimal MaxPreviewArea => RomInfo.GameDirs?.ContainsKey(RomInfo.DirNames.AreaData) == true
            ? Math.Clamp(Filesystem.GetAreaDataCount() - 1, 0, byte.MaxValue) : 0;

        // Shows the preview area's packs. A map with no area saves none of them.
        private void UsePreviewArea()
        {
            if (_selectedMapIndex >= 0) _previewAreas[_selectedMapIndex] = (int)_previewArea;
            AreaData area;
            try { area = new AreaData((byte)_previewArea); }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException) { return; }
            bool was = _suppress;
            _suppress = true;
            try
            {
                if (MapTilesets.Count > 0) { _mapTilesetIndex = Math.Min(area.MapTileset, MapTilesets.Count - 1); OnPropertyChanged(nameof(MapTilesetIndex)); }
                int bld = area.BuildingsTileset + 1; // building combo has "None" at index 0
                _buildingTilesetIndex = bld >= 0 && bld < BuildingTilesets.Count ? bld : 0;
                OnPropertyChanged(nameof(BuildingTilesetIndex));
            }
            finally { _suppress = was; }
        }
        public bool IsHgssGame => GameFamily == GameFamilies.HGSS;
        public string AreaLabel
        {
            get
            {
                if (!HasArea) return "";
                int users = 0;
                try { for (int h = 0; h < GetHeaderCount(); h++) if (MapHeader.GetMapHeader((ushort)h)?.AreaDataID == _areaId) users++; } catch (Exception caught) when (AppLogger.Noted("MapEditorViewModel.AreaLabel", caught)) { }   // a header that can't be read isn't counted
                return users > 1 ? $"Area {_areaId} · used by {users} headers" : $"Area {_areaId}";
            }
        }
        public decimal AreaGroundAnimation { get => _area?.GroundAnimation ?? 0; set => EditArea(a => a.GroundAnimation = (ushort)value); }
        public decimal AreaLightType { get => _area?.LightType ?? 0; set { EditArea(a => a.LightType = (ushort)value); OnPropertyChanged(nameof(AreaLightNote)); } }
        public IReadOnlyList<string> AreaLightNames => LabelStore.Get(AreaLightTypes.LabelKey);
        public string AreaLightNote => _area == null ? "" : AreaLightTypes.NoteFor(_area.LightType);
        public bool AreaIndoor { get => _area?.AreaType == AreaData.TYPE_INDOOR; set => EditArea(a => a.AreaType = value ? AreaData.TYPE_INDOOR : AreaData.TYPE_OUTDOOR); }

        private void EditArea(Action<AreaData> change)
        {
            if (_area == null || _suppress) return;
            change(_area);
            _areaDirty = true;
            MarkDirty();
            RaiseArea();
        }

        private void LoadArea(byte id)
        {
            try { _area = new AreaData(id); _areaId = id; }
            catch (Exception ex) { _area = null; AppLogger.Error("Area data load failed: " + ex.Message); }
            _areaDirty = false;
            RaiseArea();
        }

        private void RaiseArea()
        {
            foreach (string n in new[] { nameof(HasArea), nameof(AreaLabel), nameof(AreaGroundAnimation), nameof(AreaLightType), nameof(AreaIndoor), nameof(AreaLightNames), nameof(AreaLightNote) })
                OnPropertyChanged(n);
        }

        private void SaveArea()
        {
            if (!_areaDirty || _area == null) return;
            _area.SaveToFileDefaultDir(_areaId, showSuccessMessage: false);
            _areaDirty = false;
            AppEvents.RaiseAreaDataSaved(this, _areaId);
        }

        // Another editor saved this area. Asks first only if area fields were edited here.
        private void OnAreaSavedElsewhere(object sender, int id)
        {
            if (ReferenceEquals(sender, this) || !HasArea || id != _areaId) return;
            _ = TakeAreaSavedElsewhereAsync((byte)id);
        }

        private async Task TakeAreaSavedElsewhereAsync(byte id)
        {
            if (_areaDirty && !await DialogHelper.AskYesNoAsync(
                    $"Area {id} was saved elsewhere. Load it and drop the changes here?", "Area data changed", _owner))
            {
                StatusText = $"Area {id} was saved elsewhere, and saving here overwrites it.";
                return;
            }
            if (id != _areaId) return;
            LoadArea(id);
            SyncTilesetsToArea();
            RebuildPreview();
            StatusText = "";
        }
        public ObservableCollection<PainterOption> CollisionPainters { get; } = new ObservableCollection<PainterOption>();
        public ObservableCollection<PainterOption> TypePainters { get; } = new ObservableCollection<PainterOption>();

        public byte[,] Collisions => _map?.Collisions;
        public byte[,] Types => _map?.Types;
        public NsbmdRenderModel Model3D { get; private set; }

        private int _selectedMapIndex = -1;
        public int SelectedMapIndex
        {
            get => _selectedMapIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value != _selectedMapIndex && value >= 0 && _dirty && !_suppress)
                {
                    RecordSwitchGuard.SnapBack(() => _selectedMapIndex, v => _selectedMapIndex = v, () => OnPropertyChanged(nameof(SelectedMapIndex)));
                    if (!_switchPending) _ = SwitchAfterGuardAsync("map", () => SelectedMapIndex = value);
                    return;
                }
                if (Set(ref _selectedMapIndex, value) && !_suppress && value >= 0) LoadMap(value);
            }
        }

        private bool _switchPending;

        private async Task SwitchAfterGuardAsync(string what, Action change)
        {
            _switchPending = true;
            try
            {
                if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, _owner, what)) return;
                SetClean();
                change();
            }
            finally { _switchPending = false; }
        }

        private int _collisionPainterIndex;
        public int CollisionPainterIndex
        {
            get => _collisionPainterIndex;
            set { if (Set(ref _collisionPainterIndex, value)) { OnPropertyChanged(nameof(CollisionPaintValue)); OnPropertyChanged(nameof(CollisionValue)); } }
        }
        public byte CollisionPaintValue => _useRawCollision ? (byte)_rawCollision :
            (_collisionPainterIndex >= 0 && _collisionPainterIndex < CollisionPainters.Count ? CollisionPainters[_collisionPainterIndex].Value : (byte)0);

        private int _typePainterIndex = -1;
        public int TypePainterIndex
        {
            get => _typePainterIndex;
            set { if (Set(ref _typePainterIndex, value)) { OnPropertyChanged(nameof(TypePaintValue)); OnPropertyChanged(nameof(TypeValue)); } }
        }
        public byte TypePaintValue => _useRawType ? (byte)_rawType :
            (_typePainterIndex >= 0 && _typePainterIndex < TypePainters.Count ? TypePainters[_typePainterIndex].Value : (byte)0);

        private bool _useRawCollision; private decimal _rawCollision;
        public bool UseRawCollision { get => _useRawCollision; set { if (Set(ref _useRawCollision, value)) { OnPropertyChanged(nameof(CollisionPaintValue)); OnPropertyChanged(nameof(CollisionValue)); } } }
        public decimal RawCollision { get => _rawCollision; set { if (Set(ref _rawCollision, value)) { OnPropertyChanged(nameof(CollisionPaintValue)); OnPropertyChanged(nameof(CollisionValue)); } } }
        private bool _useRawType; private decimal _rawType;
        public bool UseRawType { get => _useRawType; set { if (Set(ref _useRawType, value)) { OnPropertyChanged(nameof(TypePaintValue)); OnPropertyChanged(nameof(TypeValue)); } } }
        public decimal RawType { get => _rawType; set { if (Set(ref _rawType, value)) { OnPropertyChanged(nameof(TypePaintValue)); OnPropertyChanged(nameof(TypeValue)); } } }

        // Values with no name are painted raw.
        public ObservableCollection<string> CollisionNames { get; } = new();
        public List<int> CollisionKeys { get; } = new();
        public ObservableCollection<string> TypeNames { get; } = new();
        public List<int> TypeKeys { get; } = new();

        public decimal CollisionValue
        {
            get => CollisionPaintValue;
            set
            {
                int v = (int)value;
                int row = CollisionKeys.IndexOf(v);
                if (row >= 0) { UseRawCollision = false; CollisionPainterIndex = row; }
                else { RawCollision = v; UseRawCollision = true; }
                OnPropertyChanged();
            }
        }

        public decimal TypeValue
        {
            get => TypePaintValue;
            set
            {
                int v = (int)value;
                int row = TypeKeys.IndexOf(v);
                if (row >= 0) { UseRawType = false; TypePainterIndex = row; }
                else { RawType = v; UseRawType = true; }
                OnPropertyChanged();
            }
        }

        private bool _permissionShowsType;
        public bool PermissionShowsType
        {
            get => _permissionShowsType;
            set { if (Set(ref _permissionShowsType, value)) OnPropertyChanged(nameof(PermissionShowsCollision)); }
        }
        public bool PermissionShowsCollision { get => !_permissionShowsType; set => PermissionShowsType = !value; }

        private string _permissionHover = "";
        public string PermissionHover { get => _permissionHover; private set => Set(ref _permissionHover, value); }

        /// <summary>Names the collision and behaviour of the permission tile under the pointer.</summary>
        public void HoverPermission((int col, int row)? at)
        {
            if (_map == null || at is not (int col, int row)) { PermissionHover = ""; return; }
            PermissionHover = $"{col}, {row}  ·  {TilePermissions.CollisionLabel(_map.Collisions[row, col], GameFamily)}"
                            + $"  ·  {TilePermissions.BehaviourLabel(_map.Types[row, col], GameFamily)}";
        }

        // The view pushes these straight to the GL control, with no model rebuild.
        private bool _showTextures = true;
        public bool ShowTextures { get => _showTextures; set => Set(ref _showTextures, value); }

        private bool _showTileGrid = SettingsManager.Settings?.MapEditorTileGrid ?? false;
        public bool ShowTileGrid
        {
            get => _showTileGrid;
            set
            {
                if (!Set(ref _showTileGrid, value) || SettingsManager.Settings == null) return;
                SettingsManager.Settings.MapEditorTileGrid = value;
                SettingsManager.Save();
            }
        }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }
    }
}
