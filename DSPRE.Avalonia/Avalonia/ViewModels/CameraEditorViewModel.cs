using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    // ── Per-row camera data ───────────────────────────────────────────────────
    public class CameraRowVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void Notify([CallerMemberName] string p = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        public int Index { get; }

        // Hidden fields preserved for round-trip
        internal short Unk1 { get; private set; }
        internal byte  Unk2 { get; private set; }

        private uint   _distance; public uint   Distance { get => _distance; set { _distance = value; Notify(); } }
        private short  _vertRot;  public short  VertRot  { get => _vertRot;  set { _vertRot  = value; Notify(); } }
        private short  _horiRot;  public short  HoriRot  { get => _horiRot;  set { _horiRot  = value; Notify(); } }
        private short  _zRot;     public short  ZRot     { get => _zRot;     set { _zRot     = value; Notify(); } }
        private bool   _isOrtho;  public bool   IsOrtho  { get => _isOrtho;  set { _isOrtho  = value; Notify(); } }
        private ushort _fov;      public ushort Fov      { get => _fov;      set { _fov      = value; Notify(); } }
        private uint   _nearClip; public uint   NearClip { get => _nearClip; set { _nearClip = value; Notify(); } }
        private uint   _farClip;  public uint   FarClip  { get => _farClip;  set { _farClip  = value; Notify(); } }
        private int    _xOffset;  public int    XOffset  { get => _xOffset;  set { _xOffset  = value; Notify(); } }
        private int    _yOffset;  public int    YOffset  { get => _yOffset;  set { _yOffset  = value; Notify(); } }
        private int    _zOffset;  public int    ZOffset  { get => _zOffset;  set { _zOffset  = value; Notify(); } }

        public CameraRowVM(int index) { Index = index; }

        public void LoadFrom(GameCamera cam)
        {
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
            // Raise all at once
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
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
    public class CameraEditorViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void Notify([CallerMemberName] string p = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        // ── Public state ─────────────────────────────────────────────────────
        public ObservableCollection<CameraRowVM> Cameras { get; } = new ObservableCollection<CameraRowVM>();
        public bool IsHgss { get; private set; }

        private bool _isReady;
        public bool IsReady { get => _isReady; private set { _isReady = value; Notify(); } }

        private bool _isDirty;
        public bool IsDirty { get => _isDirty; private set { _isDirty = value; Notify(); } }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set { _statusText = value; Notify(); } }

        // ── Internal state ───────────────────────────────────────────────────
        private uint   _overlayCameraTblOffset;
        private Window _owner;

        // ── Commands ─────────────────────────────────────────────────────────
        public ICommand ExportCameraCommand { get; }
        public ICommand ImportCameraCommand { get; }

        // ── Design-time constructor ───────────────────────────────────────────
        public CameraEditorViewModel()
        {
            ExportCameraCommand = new AsyncRelayCommand<int>(_ => Task.CompletedTask);
            ImportCameraCommand = new AsyncRelayCommand<int>(_ => Task.CompletedTask);

            if (!global::Avalonia.Controls.Design.IsDesignMode) return;

            IsHgss = true;
            for (int i = 0; i < 3; i++)
            {
                var row = new CameraRowVM(i);
                row.LoadFrom(new GameCamera());
                Cameras.Add(row);
            }
            StatusText = "Design mode";
        }

        // ── Runtime constructor ───────────────────────────────────────────────
        public CameraEditorViewModel(bool _)
        {
            ExportCameraCommand = new AsyncRelayCommand<int>(ExportCameraAsync);
            ImportCameraCommand = new AsyncRelayCommand<int>(ImportCameraAsync);
        }

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

                // Read RAM addresses from overlay to find camera table offset
                uint[] ramAddresses = new uint[RomInfo.cameraTblOffsetsToRAMaddress.Length];
                string camOverlayPath = OverlayUtils.GetPath(RomInfo.cameraTblOverlayNumber);

                using (DSUtils.EasyReader br = new DSUtils.EasyReader(camOverlayPath))
                {
                    for (int i = 0; i < RomInfo.cameraTblOffsetsToRAMaddress.Length; i++)
                    {
                        br.BaseStream.Position = RomInfo.cameraTblOffsetsToRAMaddress[i];
                        ramAddresses[i] = br.ReadUInt32();
                    }
                }

                uint referenceAddr = ramAddresses[0];
                for (int i = 1; i < ramAddresses.Length; i++)
                {
                    if (ramAddresses[i] != referenceAddr)
                    {
                        await DialogHelper.ShowInfo(
                            $"RAM pointer mismatch between offset #1 and offset #{i + 1}.\n" +
                            "Camera values might be wrong.",
                            "Possible Errors");
                    }
                }

                _overlayCameraTblOffset = referenceAddr -
                    OverlayUtils.OverlayTable.GetRAMAddress(RomInfo.cameraTblOverlayNumber);

                // Load cameras
                Cameras.Clear();
                int camCount = IsHgss ? 17 : 16;

                using (DSUtils.EasyReader br = new DSUtils.EasyReader(camOverlayPath, _overlayCameraTblOffset))
                {
                    for (int i = 0; i < camCount; i++)
                    {
                        GameCamera cam;
                        if (IsHgss)
                            cam = new GameCamera(br.ReadUInt32(), br.ReadInt16(), br.ReadInt16(), br.ReadInt16(),
                                                 br.ReadInt16(), br.ReadByte(), br.ReadByte(),
                                                 br.ReadUInt16(), br.ReadUInt32(), br.ReadUInt32(),
                                                 br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
                        else
                            cam = new GameCamera(br.ReadUInt32(), br.ReadInt16(), br.ReadInt16(), br.ReadInt16(),
                                                 br.ReadInt16(), br.ReadByte(), br.ReadByte(),
                                                 br.ReadUInt16(), br.ReadUInt32(), br.ReadUInt32());

                        var row = new CameraRowVM(i);
                        row.LoadFrom(cam);
                        row.PropertyChanged += OnRowChanged;
                        Cameras.Add(row);
                    }
                }

                IsReady = true;
                IsDirty = false;
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
                IsDirty = false;
                StatusText = "Camera table saved.";
                await DialogHelper.ShowInfo("Camera table saved.", "Success");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Save failed:\n{ex.Message}", "Save Error");
            }
        }

        // ── Export / Import table ─────────────────────────────────────────────
        public async Task ExportTableAsync()
        {
            var filter = new FilePickerFileType("Camera Table File") { Patterns = new[] { "*.bin" } };
            string suggested = System.IO.Path.GetFileNameWithoutExtension(RomInfo.projectName) + " - CameraTable.bin";
            string path = await DialogHelper.SaveFile(_owner, "Export Camera Table", new[] { filter }, suggested);
            if (path == null) return;

            try
            {
                WriteCameraTable(path, 0);
                await DialogHelper.ShowInfo("Camera table exported.", "Success");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error");
            }
        }

        public async Task ImportTableAsync()
        {
            var filter = new FilePickerFileType("Camera Table File") { Patterns = new[] { "*.bin" } };
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
                    var cam = new GameCamera(data);
                    Cameras[i].LoadFrom(cam);
                }

                IsDirty = true;
                StatusText = $"Imported {nCameras} cameras from file.";
                await DialogHelper.ShowInfo("Camera table imported.", "Success");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error");
            }
        }

        // ── Per-camera Export / Import ────────────────────────────────────────
        private async Task ExportCameraAsync(int index)
        {
            if (index < 0 || index >= Cameras.Count) return;
            var filter = new FilePickerFileType("Camera File") { Patterns = new[] { "*.bin" } };
            string suggested = System.IO.Path.GetFileNameWithoutExtension(RomInfo.projectName) + $" - Camera {index}.bin";
            string path = await DialogHelper.SaveFile(_owner, $"Export Camera {index}", new[] { filter }, suggested);
            if (path == null) return;

            try
            {
                byte[] data = Cameras[index].ToGameCamera(IsHgss).ToByteArray();
                DSUtils.WriteToFile(path, data, fmode: FileMode.Create);
                await DialogHelper.ShowInfo($"Camera {index} exported.", "Success");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error");
            }
        }

        private async Task ImportCameraAsync(int index)
        {
            if (index < 0 || index >= Cameras.Count) return;
            var filter = new FilePickerFileType("Camera File") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.OpenFile(_owner, $"Import Camera {index}", new[] { filter });
            if (path == null) return;

            try
            {
                byte[] data = File.ReadAllBytes(path);
                var cam = new GameCamera(data);
                Cameras[index].LoadFrom(cam);
                IsDirty = true;
                StatusText = $"Camera {index} imported.";
                await DialogHelper.ShowInfo($"Camera {index} imported.", "Success");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void WriteCameraTable(string path, uint startOffset)
        {
            for (int i = 0; i < Cameras.Count; i++)
            {
                byte[] data = Cameras[i].ToGameCamera(IsHgss).ToByteArray();
                DSUtils.WriteToFile(path, data, (uint)(startOffset + i * RomInfo.cameraSize));
            }
        }

        private void OnRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (IsReady) IsDirty = true;
        }
    }

    // ── Minimal async command ─────────────────────────────────────────────────
    internal sealed class AsyncRelayCommand<T> : ICommand
    {
        private readonly Func<T, Task> _execute;
        private bool _running;
        public event EventHandler CanExecuteChanged;

        public AsyncRelayCommand(Func<T, Task> execute) { _execute = execute; }

        public bool CanExecute(object parameter) => !_running;

        public async void Execute(object parameter)
        {
            if (_running) return;
            _running = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            try
            {
                T arg = (T)Convert.ChangeType(parameter, typeof(T));
                await _execute(arg);
            }
            catch { /* swallow, errors handled inside execute delegates */ }
            finally
            {
                _running = false;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
