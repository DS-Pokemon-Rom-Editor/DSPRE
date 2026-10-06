using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.World
{
    // ── Per-row camera data ───────────────────────────────────────────────────
    public class CameraRowVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public int Index { get; }

        // Hidden fields preserved for round-trip
        internal short Unk1 { get; private set; }
        internal byte  Unk2 { get; private set; }

        private uint   _distance;
        private short  _vertRot, _horiRot, _zRot;
        private bool   _isOrtho;
        private ushort _fov;
        private uint   _nearClip, _farClip;
        private int    _xOffset, _yOffset, _zOffset;

        // The table in tiles and degrees. Each value is rounded to what its box shows, and a setter that gets
        // the shown value back leaves the raw one alone, so tabbing through never rewrites the table.
        private const decimal RawPerTile = 4096m * 16m;
        private const decimal RawPerDegree = 65536m / 360m;

        private static decimal Tiles(long raw) => Math.Round(raw / RawPerTile, 3, MidpointRounding.AwayFromZero);
        private static long FromTiles(decimal tiles) => (long)Math.Round(tiles * RawPerTile);
        private static decimal Degrees(int raw) => Math.Round(raw / RawPerDegree, 2, MidpointRounding.AwayFromZero);
        private static int FromDegrees(decimal deg) => (int)Math.Round(deg * RawPerDegree);
        private static short Angle(decimal deg) => (short)Math.Clamp(FromDegrees(deg), short.MinValue, short.MaxValue);

        private void Changed() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

        // The boxes show fewer digits than the table stores, so typing a camera's loaded number back must give
        // its loaded raw value rather than the nearest one to the rounded number.
        private GameCamera _loaded;
        private void Set<T>(ref T field, T fromBox, T loaded, decimal shown, Func<T, decimal> show)
        {
            field = _loaded != null && show(loaded) == shown ? loaded : fromBox;
            Changed();
        }

        public decimal Distance
        {
            get => Tiles(_distance);
            set { if (value != Distance) Set(ref _distance, (uint)Math.Clamp(FromTiles(value), 0, uint.MaxValue), _loaded?.distance ?? 0, value, v => Tiles(v)); }
        }

        /// <summary>How far the camera looks down. The table stores it negated.</summary>
        public decimal Tilt
        {
            get => -Degrees(_vertRot);
            set { if (value != Tilt) Set(ref _vertRot, Angle(-value), _loaded?.vertRot ?? 0, value, v => -Degrees(v)); }
        }

        public decimal Turn
        {
            get => Degrees(_horiRot);
            set { if (value != Turn) Set(ref _horiRot, Angle(value), _loaded?.horiRot ?? 0, value, v => Degrees(v)); }
        }

        /// <summary>How far the picture tilts, which is half the stored angle.</summary>
        public decimal Roll
        {
            get => Math.Round(Degrees(_zRot) / 2m, 2, MidpointRounding.AwayFromZero);
            set { if (value != Roll) Set(ref _zRot, Angle(value * 2m), _loaded?.zRot ?? 0, value, v => Math.Round(Degrees(v) / 2m, 2, MidpointRounding.AwayFromZero)); }
        }

        /// <summary>0 perspective, 1 flat.</summary>
        public int ViewIndex
        {
            get => _isOrtho ? 1 : 0;
            set { if (value >= 0 && value != ViewIndex) { _isOrtho = value == 1; Changed(); } }
        }

        /// <summary>The whole vertical view angle; the table stores half of it.</summary>
        public decimal FieldOfView
        {
            get => Math.Round(_fov * 2m / RawPerDegree, 2, MidpointRounding.AwayFromZero);
            set { if (value != FieldOfView) Set(ref _fov, (ushort)Math.Clamp((int)Math.Round(value / 2m * RawPerDegree), 0, ushort.MaxValue), _loaded?.fov ?? 0, value, v => Math.Round(v * 2m / RawPerDegree, 2, MidpointRounding.AwayFromZero)); }
        }

        public decimal NearClip
        {
            get => Tiles(_nearClip);
            set { if (value != NearClip) Set(ref _nearClip, (uint)Math.Clamp(FromTiles(value), 0, uint.MaxValue), _loaded?.nearClip ?? 0, value, v => Tiles(v)); }
        }

        public decimal FarClip
        {
            get => Tiles(_farClip);
            set { if (value != FarClip) Set(ref _farClip, (uint)Math.Clamp(FromTiles(value), 0, uint.MaxValue), _loaded?.farClip ?? 0, value, v => Tiles(v)); }
        }

        public decimal ShiftX
        {
            get => Tiles(_xOffset);
            set { if (value != ShiftX) Set(ref _xOffset, (int)Math.Clamp(FromTiles(value), int.MinValue, int.MaxValue), _loaded?.xOffset ?? 0, value, v => Tiles(v)); }
        }

        public decimal ShiftY
        {
            get => Tiles(_yOffset);
            set { if (value != ShiftY) Set(ref _yOffset, (int)Math.Clamp(FromTiles(value), int.MinValue, int.MaxValue), _loaded?.yOffset ?? 0, value, v => Tiles(v)); }
        }

        public decimal ShiftZ
        {
            get => Tiles(_zOffset);
            set { if (value != ShiftZ) Set(ref _zOffset, (int)Math.Clamp(FromTiles(value), int.MinValue, int.MaxValue), _loaded?.zOffset ?? 0, value, v => Tiles(v)); }
        }

        /// <summary>The camera's name, which the header editor shows beside the number.</summary>
        public string Name => DSPRE.Avalonia.Data.LabelStore.GetLabel(DSPRE.Avalonia.Data.LabelStore.CameraKey, Index);

        /// <summary>The number the header editor asks for, with the name it shows beside it.</summary>
        public string Label => Index.ToString("D2") + "  " + Name;

        public CameraRowVM(int index) { Index = index; }

        public void RefreshName()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
        }

        public void LoadFrom(GameCamera cam)
        {
            _loaded  = cam;
            Unk1     = cam.unk1;
            Unk2     = cam.unk2;
            _distance = cam.distance;
            _vertRot  = cam.vertRot;
            _horiRot  = cam.horiRot;
            _zRot     = cam.zRot;
            _isOrtho  = cam.perspMode == GameCamera.ORTHO;
            _fov      = cam.fov;
            _nearClip = cam.nearClip;
            _farClip  = cam.farClip;
            _xOffset  = cam.xOffset ?? 0;
            _yOffset  = cam.yOffset ?? 0;
            _zOffset  = cam.zOffset ?? 0;
            Changed();
        }

        /// <summary>Puts an undone or redone camera back without moving what counts as loaded.</summary>
        public void ApplyState(GameCamera cam)
        {
            GameCamera loaded = _loaded;
            LoadFrom(cam);
            _loaded = loaded;
        }

        public GameCamera ToGameCamera(bool isHgss) => new GameCamera(
            distance:  _distance,
            vertRot:   _vertRot,
            horiRot:   _horiRot,
            zRot:      _zRot,
            unk1:      Unk1,
            perspMode: _isOrtho ? GameCamera.ORTHO : GameCamera.PERSPECTIVE,
            unk2:      Unk2,
            fov:       _fov,
            nearClip:  _nearClip,
            farClip:   _farClip,
            xOffset:   isHgss ? _xOffset : (int?)null,
            yOffset:   isHgss ? _yOffset : (int?)null,
            zOffset:   isHgss ? _zOffset : (int?)null
        );
    }

    // ── Main ViewModel ────────────────────────────────────────────────────────
    public class CameraEditorViewModel : INotifyPropertyChanged, DSPRE.Editors.IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void Notify([CallerMemberName] string p = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        // ── Public state ─────────────────────────────────────────────────────
        public ObservableCollection<CameraRowVM> Cameras { get; } = new ObservableCollection<CameraRowVM>();
        private bool _isHgss;
        public bool IsHgss { get => _isHgss; private set { _isHgss = value; Notify(); Notify(nameof(ShowsRoll)); } }

        /// <summary>Diamond and Pearl never apply the roll.</summary>
        public bool ShowsRoll => RomInfo.gameFamily != GameFamilies.DP;

        private bool _isReady;
        public bool IsReady { get => _isReady; private set { _isReady = value; Notify(); } }

        private bool _isDirty;
        public bool IsDirty { get => _isDirty; private set { _isDirty = value; Notify(); Notify(nameof(HasUnsavedChanges)); } }

        public bool HasUnsavedChanges => IsDirty;
        public string UnsavedChangesDescription => "Camera table";
        public void SaveChanges() => _ = SaveChangesAsync();
        public async Task<bool> SaveChangesAsync() { await SaveAsync(); return !IsDirty; }
        public void DiscardChanges() => _ = SetupAsync(_owner);

        // ── Undo / redo: the whole table, as the game stores it ──────────────
        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { Notify(nameof(CanUndo)); Notify(nameof(CanRedo)); }

        private byte[] TakeState()
        {
            using MemoryStream ms = new MemoryStream();
            foreach (CameraRowVM row in Cameras) { byte[] b = row.ToGameCamera(IsHgss).ToByteArray(); ms.Write(b, 0, b.Length); }
            return ms.ToArray();
        }

        private void ApplyState(byte[] state)
        {
            int size = state.Length / Math.Max(1, Cameras.Count);
            for (int i = 0; i < Cameras.Count; i++)
                Cameras[i].ApplyState(new GameCamera(state.AsSpan(i * size, size).ToArray()));
            IsDirty = _undo.IsDirty;
        }

        private void ResetUndo() { _undo = new ByteStateUndo(TakeState, ApplyState, RaiseUndo); RaiseUndo(); }

        private void Edited()
        {
            if (_undo == null) { IsDirty = true; return; }
            _undo.Record();
            IsDirty = _undo.IsDirty;
        }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set { _statusText = value; Notify(); } }

        // ── Preview ──────────────────────────────────────────────────────────
        private CameraRowVM _selectedCamera;
        public CameraRowVM SelectedCamera
        {
            get => _selectedCamera;
            set { if (_selectedCamera != value) { _selectedCamera = value; Notify(); } }
        }

        // The towns you can fly to, starting town first; the preview looks from the chosen one's fly spot.
        private System.Collections.Generic.List<FlyTable.Spot> _spots = new();
        public ObservableCollection<string> PreviewPlaces { get; } = new();

        private int _previewPlaceIndex;
        public int PreviewPlaceIndex
        {
            get => _previewPlaceIndex;
            set { if (_previewPlaceIndex != value && value >= 0) { _previewPlaceIndex = value; Notify(); Notify(nameof(PreviewSpot)); } }
        }

        public FlyTable.Spot? PreviewSpot =>
            _previewPlaceIndex >= 0 && _previewPlaceIndex < _spots.Count ? _spots[_previewPlaceIndex] : null;

        private void LoadPreviewPlaces()
        {
            _spots = FlyTable.Spots();
            PreviewPlaces.Clear();
            foreach (FlyTable.Spot spot in _spots)
            {
                string name;
                try { name = HeaderLabels.LocationNameOf(DSPRE.ROMFiles.MapHeader.GetMapHeader((ushort)spot.HeaderId)); }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is IndexOutOfRangeException)
                {
                    AppLogger.Warn($"Fly spot header {spot.HeaderId} has no readable name: {ex.Message}");
                    name = "";
                }
                PreviewPlaces.Add(string.IsNullOrEmpty(name) ? $"Header {spot.HeaderId}" : name);
            }
            // A ComboBox drops its selection while its items fill without telling the binding, so the
            // starting town is picked once the list has settled, through -1 so the value really changes.
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _previewPlaceIndex = -1;
                Notify(nameof(PreviewPlaceIndex));
                _previewPlaceIndex = 0;
                Notify(nameof(PreviewPlaceIndex));
                Notify(nameof(PreviewSpot));
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        /// <summary>The weather the preview plays, from the header weather list.</summary>
        public MappedCombo PreviewWeather { get; } = new MappedCombo();

        private int _previewWeatherIndex;
        public int PreviewWeatherIndex
        {
            get => _previewWeatherIndex;
            set { if (_previewWeatherIndex != value && value >= 0) { _previewWeatherIndex = value; Notify(); Notify(nameof(PreviewWeatherValue)); } }
        }

        public int PreviewWeatherValue => Math.Max(0, PreviewWeather.KeyAt(_previewWeatherIndex));

        private void LoadPreviewWeathers()
        {
            PreviewWeather.LoadLabels(DSPRE.Avalonia.Data.LabelStore.WeatherKey);
            AppEvents.LabelsChanged -= OnLabelsChanged;
            AppEvents.LabelsChanged += OnLabelsChanged;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _previewWeatherIndex = -1;
                Notify(nameof(PreviewWeatherIndex));
                _previewWeatherIndex = Math.Max(0, PreviewWeather.IndexOf(0));
                Notify(nameof(PreviewWeatherIndex));
                Notify(nameof(PreviewWeatherValue));
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        /// <summary>The selected row as the game would read it, unsaved values included.</summary>
        public DSPRE.ROMFiles.FieldCameraEntry SelectedEntry =>
            _selectedCamera == null ? null
            : DSPRE.ROMFiles.FieldCamera.FromGameCamera(_selectedCamera.Index, _selectedCamera.Name,
                _selectedCamera.ToGameCamera(IsHgss), RomInfo.gameFamily);

        // ── Internal state ───────────────────────────────────────────────────
        private uint   _overlayCameraTblOffset;
        private Window _owner;

        // ── Design-time constructor ───────────────────────────────────────────
        public CameraEditorViewModel()
        {
            if (!global::Avalonia.Controls.Design.IsDesignMode) return;

            IsHgss = true;
            for (int i = 0; i < 3; i++)
            {
                CameraRowVM row = new CameraRowVM(i);
                row.LoadFrom(new GameCamera());
                Cameras.Add(row);
            }
            SelectedCamera = Cameras[0];
            StatusText = "Design mode";
        }

        // ── Runtime constructor ───────────────────────────────────────────────
        public CameraEditorViewModel(bool _) { }

        // ── Setup ─────────────────────────────────────────────────────────────
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            IsReady = false;
            StatusText = "Loading camera data…";

            try
            {
                RomInfo.PrepareCameraData();
                IsHgss = RomInfo.gameFamily == GameFamilies.HGSS;

                // A legacy ndstool project can't reliably track overlay compression state (see
                // RomInfo.IsDsRomProject); ds-rom handles it automatically during unpack/build.
                if (RomInfo.gameFamily == GameFamilies.HGSS && !RomInfo.IsDsRomProject && RomInfo.cameraTblOverlayNumber == 1)
                {
                    StatusText = "Convert this project to ds-rom format before using the Camera Editor for this ROM.";
                    await DialogHelper.ShowInfo(StatusText, "ds-rom project required");
                    IsReady = false;
                    return;
                }

                GameCameraTable.Location location = GameCameraTable.Locate();
                if (!location.PointersAgree)
                {
                    await DialogHelper.ShowInfo(
                        "The game keeps more than one pointer to the camera table and they disagree.\n" +
                        "Camera values might be wrong.",
                        "Possible Errors");
                }
                _overlayCameraTblOffset = location.Offset;

                Cameras.Clear();
                List<GameCamera> cameras = GameCameraTable.Read(location);
                for (int i = 0; i < cameras.Count; i++)
                {
                    CameraRowVM row = new CameraRowVM(i);
                    row.LoadFrom(cameras[i]);
                    row.PropertyChanged += OnRowChanged;
                    Cameras.Add(row);
                }
                int camCount = cameras.Count;

                IsReady = true;
                IsDirty = false;
                ResetUndo();
                SelectedCamera = Cameras.Count > 0 ? Cameras[0] : null;
                LoadPreviewPlaces();
                LoadPreviewWeathers();
                StatusText = $"Loaded {camCount} cameras ({(IsHgss ? "HGSS" : "DP/Plat")})";
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading camera data: {ex.Message}";
                await DialogHelper.ShowError($"Failed to load camera data:\n{ex.Message}", "Camera Editor Error");
            }
        }

        // ── Save ──────────────────────────────────────────────────────────────
        public async Task SaveAsync()
        {
            try
            {
                string overlayPath = OverlayUtils.GetPath(RomInfo.cameraTblOverlayNumber);
                WriteCameraTable(overlayPath, _overlayCameraTblOffset);
                _undo?.MarkSaved();
                IsDirty = false;
                GameCameraTable.RaiseSaved();
                StatusText = "Camera table saved.";
                SaveNotice.Saved(UnsavedChangesDescription);
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Save failed:\n{ex.Message}", "Save Error");
            }
        }

        // ── Export / Import table ─────────────────────────────────────────────
        public async Task ExportTableAsync()
        {
            FilePickerFileType filter = new FilePickerFileType("Camera Table File") { Patterns = new[] { "*.bin" } };
            string suggested = System.IO.Path.GetFileNameWithoutExtension(RomInfo.projectName) + " - CameraTable.bin";
            string path = await DialogHelper.SaveFile(_owner, "Export Camera Table", new[] { filter }, suggested);
            if (path == null) return;

            try
            {
                WriteCameraTable(path, 0);
                StatusText = "Camera table exported.";
                SaveNotice.Show(StatusText);
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error");
            }
        }

        public async Task ImportTableAsync()
        {
            FilePickerFileType filter = new FilePickerFileType("Camera Table File") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.OpenFile(_owner, "Import Camera Table", new[] { filter });
            if (path == null) return;

            try
            {
                long len = new FileInfo(path).Length;
                if (len % RomInfo.cameraSize != 0)
                {
                    await DialogHelper.ShowError(
                        $"Not a {RomInfo.gameFamily} camera table file.\n" +
                        $"File length must be a multiple of {RomInfo.cameraSize}.", "Wrong File");
                    return;
                }

                int nCameras = (int)(len / RomInfo.cameraSize);
                for (int i = 0; i < nCameras && i < Cameras.Count; i++)
                {
                    byte[] data = DSUtils.ReadFromFile(path, i * RomInfo.cameraSize, RomInfo.cameraSize);
                    GameCamera cam = new GameCamera(data);
                    Cameras[i].LoadFrom(cam);
                }

                Edited();
                StatusText = $"Imported {nCameras} cameras from file.";
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error");
            }
        }

        // ── Per-camera Export / Import ────────────────────────────────────────
        public async Task ExportCameraAsync()
        {
            int index = SelectedCamera?.Index ?? -1;
            if (index < 0 || index >= Cameras.Count) return;
            FilePickerFileType filter = new FilePickerFileType("Camera File") { Patterns = new[] { "*.bin" } };
            string suggested = System.IO.Path.GetFileNameWithoutExtension(RomInfo.projectName) + $" - Camera {index}.bin";
            string path = await DialogHelper.SaveFile(_owner, $"Export Camera {index}", new[] { filter }, suggested);
            if (path == null) return;

            try
            {
                byte[] data = Cameras[index].ToGameCamera(IsHgss).ToByteArray();
                DSUtils.WriteToFile(path, data, fmode: FileMode.Create);
                StatusText = $"Camera {index} exported.";
                SaveNotice.Show(StatusText);
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error");
            }
        }

        public async Task ImportCameraAsync()
        {
            int index = SelectedCamera?.Index ?? -1;
            if (index < 0 || index >= Cameras.Count) return;
            FilePickerFileType filter = new FilePickerFileType("Camera File") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.OpenFile(_owner, $"Import Camera {index}", new[] { filter });
            if (path == null) return;

            try
            {
                byte[] data = File.ReadAllBytes(path);
                GameCamera cam = new GameCamera(data);
                Cameras[index].LoadFrom(cam);
                Edited();
                StatusText = $"Camera {index} imported.";
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void WriteCameraTable(string path, uint startOffset) =>
            GameCameraTable.Write(System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(Cameras, c => c.ToGameCamera(IsHgss))), path, startOffset);

        private void OnRowChanged(object sender, PropertyChangedEventArgs e)
        {
            // A name lives with the project's labels, not in the table.
            if (e.PropertyName is nameof(CameraRowVM.Name) or nameof(CameraRowVM.Label)) return;
            if (IsReady) Edited();
        }

        // ── Names ─────────────────────────────────────────────────────────────
        /// <summary>Renames the selected camera everywhere its name shows; a blank name restores the original.</summary>
        public async Task RenameSelectedAsync()
        {
            CameraRowVM row = SelectedCamera;
            if (row == null) return;
            string name = await DialogHelper.PromptText($"Camera {row.Index}", "Rename Camera", row.Name, _owner);
            if (name == null || name.Trim() == row.Name) return;
            DSPRE.Avalonia.Data.LabelStore.SetLabel(DSPRE.Avalonia.Data.LabelStore.CameraKey, row.Index, name, global: false);
            DSPRE.Avalonia.Data.LabelStore.Save(global: false);
            AppEvents.RaiseLabelsChanged();
        }

        private void OnLabelsChanged(object sender, EventArgs e)
        {
            foreach (CameraRowVM row in Cameras) row.RefreshName();
            PreviewWeather.LoadLabels(DSPRE.Avalonia.Data.LabelStore.WeatherKey);
            int keep = _previewWeatherIndex;
            _previewWeatherIndex = -1;
            Notify(nameof(PreviewWeatherIndex));
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _previewWeatherIndex = keep;
                Notify(nameof(PreviewWeatherIndex));
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        public void Detach() => AppEvents.LabelsChanged -= OnLabelsChanged;

        public void Reattach()
        {
            AppEvents.LabelsChanged -= OnLabelsChanged;
            AppEvents.LabelsChanged += OnLabelsChanged;
            OnLabelsChanged(this, EventArgs.Empty);
        }
    }
}
